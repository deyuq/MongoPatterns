using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoRepository.Core.Outbox.Models;
using MongoRepository.Core.Outbox.Settings;
using MongoRepository.Core.Repositories;

namespace MongoRepository.Core.Outbox.Implementation;

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
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.ProcessingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Outbox processor stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAdvancedRepository<OutboxMessage>>();

        // Get a batch of pending messages
        var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Pending);
        var sort = Builders<OutboxMessage>.Sort.Ascending(m => m.CreatedAt);

        _logger.LogDebug("Getting batch of {BatchSize} pending messages", _settings.BatchSize);
        var pendingMessages = await repository.GetWithDefinitionAsync<OutboxMessage>(
            filter,
            null, // No projection needed
            sort,
            _settings.BatchSize);

        _logger.LogInformation("Processing {Count} pending messages", pendingMessages.Count());

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

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IRepository<OutboxMessage> repository,
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken)
    {
        try
        {
            // Update message status to Processing
            message.Status = OutboxMessageStatus.Processing;
            message.ProcessingAttempts++;
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
            message.ProcessedAt = DateTime.UtcNow;
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

                await repository.UpdateAsync(message);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error updating message status after processing failure");
            }
        }
    }
}