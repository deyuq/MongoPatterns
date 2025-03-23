using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoPatterns.Outbox.Models;
using MongoPatterns.Outbox.Settings;
using MongoPatterns.Repository.Repositories;

namespace MongoPatterns.Outbox.Implementation;

/// <summary>
/// Background service that processes outbox messages
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly OutboxSettings _settings;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly Dictionary<string, Type> _handlerTypes = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private Task? _processingTask;
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxProcessor"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">The service scope factory</param>
    /// <param name="settings">The outbox settings</param>
    /// <param name="logger">The logger</param>
    /// <param name="messageHandlers">The registered message handlers</param>
    public OutboxProcessor(
        IServiceScopeFactory serviceScopeFactory,
        OutboxSettings settings,
        ILogger<OutboxProcessor> logger,
        IEnumerable<IMessageHandler> messageHandlers)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _settings = settings;
        _logger = logger;

        // Register message handlers by their type
        foreach (var handler in messageHandlers)
        {
            var interfaceType = handler.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>));

            if (interfaceType != null)
            {
                var messageType = handler.MessageType;
                var genericArgType = interfaceType.GetGenericArguments()[0];

                _handlerTypes[messageType] = genericArgType;
                _logger.LogInformation("Registered message handler for type {MessageType}", messageType);
            }
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Executes the background service
    /// </summary>
    /// <param name="stoppingToken">The cancellation token</param>
    /// <returns>A task representing the execution</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Start a new processing task
                _processingTask = Task.Run(async () =>
                {
                    try
                    {
                        // First, check for stuck messages that have been in Processing state for too long
                        await ResetStuckMessagesAsync(stoppingToken);

                        // Then process pending messages
                        await ProcessPendingMessagesAsync(stoppingToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger.LogError(ex, "Error in outbox processing cycle");
                    }
                }, stoppingToken);

                // Wait for the processing task to complete or for the delay
                await Task.WhenAny(_processingTask, Task.Delay(TimeSpan.FromSeconds(_settings.ProcessingIntervalSeconds), stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // This is expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");

                // Add a small delay to prevent tight loops in error conditions
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Outbox processor stopping, waiting for in-progress work to complete...");

        // Wait for any in-progress processing to complete
        if (_processingTask != null && !_processingTask.IsCompleted)
        {
            try
            {
                // Wait for a reasonable amount of time for processing to complete
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var completedTask = await Task.WhenAny(_processingTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("Graceful shutdown timeout reached, some message processing may have been interrupted");
                }
                else
                {
                    _logger.LogInformation("All in-progress outbox processing completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during graceful shutdown of outbox processor");
            }
        }

        _logger.LogInformation("Outbox processor stopped");
    }

    /// <summary>
    /// Resets messages that have been stuck in Processing state for too long
    /// </summary>
    private async Task ResetStuckMessagesAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
            return;

        await _processingLock.WaitAsync(stoppingToken);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAdvancedRepository<OutboxMessage>>();

            // Calculate the cutoff time for stuck messages
            var cutoffTime = DateTime.UtcNow.AddMinutes(-_settings.ProcessingTtlMinutes);

            // Find messages in Processing state that haven't been updated in a while
            var filterBuilder = Builders<OutboxMessage>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq(m => m.Status, OutboxMessageStatus.Processing),
                filterBuilder.Lt(m => m.ProcessedAt, cutoffTime)
            );

            var stuckMessages = await repository.GetWithDefinitionAsync(filter);
            var messages = stuckMessages.ToList();

            if (messages.Any())
            {
                _logger.LogWarning("Found {Count} stuck messages in Processing state", messages.Count);

                foreach (var message in messages)
                {
                    // Reset message to Pending state for retry
                    message.Status = OutboxMessageStatus.Pending;
                    message.Error = $"Reset from Processing state after TTL of {_settings.ProcessingTtlMinutes} minutes exceeded";

                    await repository.UpdateAsync(message);
                    _logger.LogInformation("Reset stuck message {MessageId} to Pending state", message.Id);
                }
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Error checking for stuck messages");
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
            return;

        await _processingLock.WaitAsync(stoppingToken);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAdvancedRepository<OutboxMessage>>();

            // Get pending messages with a limit
            var filterBuilder = Builders<OutboxMessage>.Filter;
            var filter = filterBuilder.Eq(m => m.Status, OutboxMessageStatus.Pending);
            var sort = Builders<OutboxMessage>.Sort.Ascending(m => m.CreatedAt);

            _logger.LogDebug("Getting batch of {BatchSize} pending messages", _settings.BatchSize);

            // Use the new overload with sort and limit
            var pendingMessages = await repository.GetWithDefinitionAsync(filter, sort, _settings.BatchSize);

            var messages = pendingMessages.ToList();
            if (!messages.Any())
            {
                return;
            }

            _logger.LogInformation("Processing {Count} pending outbox messages", messages.Count);

            foreach (var message in messages)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await ProcessMessageAsync(message, repository, scope.ServiceProvider, stoppingToken);
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IRepository<OutboxMessage> repository,
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            // Update message status to Processing
            message.Status = OutboxMessageStatus.Processing;
            message.ProcessingAttempts++;
            message.ProcessedAt = DateTime.UtcNow; // Set the timestamp when processing started
            await repository.UpdateAsync(message);

            _logger.LogDebug("Processing message {MessageId} ({MessageType}), attempt {Attempt}",
                message.Id, message.MessageType, message.ProcessingAttempts);

            var success = false;

            // Find a handler for this message type
            if (_handlerTypes.TryGetValue(message.MessageType, out var messageType))
            {
                var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
                var handler = serviceProvider.GetService(handlerType);

                if (handler != null)
                {
                    // Deserialize the message content
                    var messageObj = JsonSerializer.Deserialize(message.Content, messageType, _jsonOptions);

                    // Call the handler
                    var method = handlerType.GetMethod("HandleAsync");
                    if (method != null && messageObj != null)
                    {
                        await (Task)method.Invoke(handler, new[] { messageObj })!;
                        success = true;
                    }
                    else
                    {
                        message.Error = "Handler method not found or message couldn't be deserialized";
                    }
                }
                else
                {
                    message.Error = $"No registered handler found for message type {message.MessageType}";
                }
            }
            else
            {
                message.Error = $"No handler type registered for message type {message.MessageType}";
            }

            // Update message status
            message.ProcessedAt = DateTime.UtcNow; // Update the timestamp when processing completed
            message.Status = success ? OutboxMessageStatus.Processed : OutboxMessageStatus.Failed;

            // If message processing failed but we haven't reached max retries, set it back to pending
            if (!success && message.ProcessingAttempts < _settings.MaxRetryAttempts)
            {
                // Use exponential backoff for retry delay
                var delayMultiplier = Math.Pow(2, message.ProcessingAttempts - 1);
                var delaySeconds = _settings.RetryDelaySeconds * delayMultiplier;

                _logger.LogWarning(
                    "Message processing failed. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts}): {Error}",
                    delaySeconds, message.ProcessingAttempts, _settings.MaxRetryAttempts, message.Error);

                message.Status = OutboxMessageStatus.Pending;
            }
            else if (!success)
            {
                _logger.LogError(
                    "Message processing failed and max retry attempts reached. Marking as abandoned: {Error}",
                    message.Error);

                message.Status = OutboxMessageStatus.Abandoned;
            }
            else
            {
                _logger.LogInformation("Successfully processed message {MessageId} ({MessageType})",
                    message.Id, message.MessageType);
            }

            await repository.UpdateAsync(message);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // If we're shutting down, don't update the message status
            // It will be picked up in the next processing cycle
            _logger.LogWarning("Message processing for {MessageId} interrupted due to shutdown", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", message.Id);

            try
            {
                // Try to update the message with the error
                message.Error = ex.Message;
                message.Status = message.ProcessingAttempts < _settings.MaxRetryAttempts
                    ? OutboxMessageStatus.Pending
                    : OutboxMessageStatus.Abandoned;
                message.ProcessedAt = DateTime.UtcNow;

                await repository.UpdateAsync(message);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error updating message status after processing failure");
            }
        }
    }
}