using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoPatterns.Outbox.Models;
using MongoPatterns.Outbox.Repositories;
using MongoPatterns.Outbox.Settings;
using MongoPatterns.Repository.Repositories;
using System.Linq.Expressions;

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

    // Unique ID for this service instance
    private readonly string _serviceInstanceId = Guid.NewGuid().ToString();

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

        _logger.LogInformation("Outbox processor initialized with instance ID: {ServiceInstanceId}", _serviceInstanceId);
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

            // Add delay between MongoDB operations to reduce load
            if (_settings.ProcessingDelayMilliseconds > 0)
            {
                await Task.Delay(_settings.ProcessingDelayMilliseconds, stoppingToken);
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
    /// Resets messages that have been stuck in Processing state for too long or have expired claims
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

            // Calculate the cutoff times
            var processingCutoffTime = DateTime.UtcNow.AddMinutes(-_settings.ProcessingTtlMinutes);
            var claimCutoffTime = DateTime.UtcNow;

            // Find messages that are stuck in Processing state
            var filterBuilder = Builders<OutboxMessage>.Filter;
            var processingFilter = filterBuilder.And(
                filterBuilder.Eq(m => m.Status, OutboxMessageStatus.Processing),
                filterBuilder.Lt(m => m.ProcessedAt, processingCutoffTime)
            );

            var stuckMessages = await repository.GetWithDefinitionAsync(processingFilter);
            var messages = stuckMessages.ToList();

            if (messages.Any())
            {
                _logger.LogWarning("Found {Count} stuck messages in Processing state", messages.Count);

                foreach (var message in messages)
                {
                    // Reset message to Pending state for retry
                    message.Status = OutboxMessageStatus.Pending;
                    message.Error = $"Reset from Processing state after TTL of {_settings.ProcessingTtlMinutes} minutes exceeded";
                    // Clear claim information
                    message.ClaimedBy = null;
                    message.ClaimExpiresAt = null;

                    await repository.UpdateAsync(message);
                    _logger.LogInformation("Reset stuck message {MessageId} to Pending state", message.Id);
                }
            }

            // Find messages with expired claims
            var claimFilter = filterBuilder.And(
                filterBuilder.Ne(m => m.ClaimedBy, null),
                filterBuilder.Lt(m => m.ClaimExpiresAt, claimCutoffTime),
                filterBuilder.Eq(m => m.Status, OutboxMessageStatus.Pending)
            );

            var expiredClaimMessages = await repository.GetWithDefinitionAsync(claimFilter);
            var expiredMessages = expiredClaimMessages.ToList();

            if (expiredMessages.Any())
            {
                _logger.LogWarning("Found {Count} messages with expired claims", expiredMessages.Count);

                // Bulk update to release all expired claims at once
                var bulkUpdate = Builders<OutboxMessage>.Update
                    .Set(m => m.ClaimedBy, null)
                    .Set(m => m.ClaimExpiresAt, null)
                    .Set(m => m.Error, "Claim expired and released for processing by another instance");

                // Create expression from filter
                Expression<Func<OutboxMessage, bool>> claimExpression = m =>
                    m.ClaimedBy != null &&
                    m.ClaimExpiresAt < claimCutoffTime &&
                    m.Status == OutboxMessageStatus.Pending;

                await repository.BulkUpdateAsync(claimExpression, bulkUpdate);
                _logger.LogInformation("Released {Count} expired message claims", expiredMessages.Count);
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
            var advancedRepository = scope.ServiceProvider.GetRequiredService<OutboxAdvancedRepository>();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<OutboxMessage>>();

            _logger.LogDebug("Attempting to claim batch of {BatchSize} pending messages", _settings.BatchSize);

            // Process messages one by one to avoid potential race conditions
            for (var i = 0; i < _settings.BatchSize; i++)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                // Try to claim a message
                var claimedMessage = await ClaimNextPendingMessageAsync(advancedRepository, stoppingToken);

                if (claimedMessage == null)
                {
                    // No more messages to process
                    break;
                }

                // Process the claimed message
                await ProcessMessageAsync(claimedMessage, repository, scope.ServiceProvider, stoppingToken);
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    /// <summary>
    /// Claims the next pending message using atomic operations
    /// </summary>
    private async Task<OutboxMessage?> ClaimNextPendingMessageAsync(
        OutboxAdvancedRepository repository,
        CancellationToken stoppingToken)
    {
        // Set claim expiration time based on settings
        var claimExpiresAt = DateTime.UtcNow.AddMinutes(_settings.ClaimTimeoutMinutes);

        // Create filter for unclaimed pending messages
        var filterBuilder = Builders<OutboxMessage>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(m => m.Status, OutboxMessageStatus.Pending),
            filterBuilder.Or(
                filterBuilder.Eq(m => m.ClaimedBy, null),
                filterBuilder.Lt(m => m.ClaimExpiresAt, DateTime.UtcNow)
            )
        );

        // Create update to claim the message
        var update = Builders<OutboxMessage>.Update
            .Set(m => m.ClaimedBy, _serviceInstanceId)
            .Set(m => m.ClaimExpiresAt, claimExpiresAt);

        // Use FindOneAndUpdate to atomically claim the message
        var options = new FindOneAndUpdateOptions<OutboxMessage>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<OutboxMessage>.Sort.Ascending(m => m.CreatedAt)
        };

        try
        {
            var claimedMessage = await repository.FindOneAndUpdateAsync(filter, update, options);

            if (claimedMessage != null)
            {
                _logger.LogDebug("Successfully claimed message {MessageId} for processing by instance {InstanceId}",
                    claimedMessage.Id, _serviceInstanceId);
            }

            return claimedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming next pending message");
            return null;
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

            // Always clear claim when done processing
            message.ClaimedBy = null;
            message.ClaimExpiresAt = null;

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
                message.ClaimedBy = null;
                message.ClaimExpiresAt = null;

                await repository.UpdateAsync(message);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error updating message status after processing failure");
            }
        }
    }
}