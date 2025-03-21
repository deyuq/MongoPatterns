using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoRepository.Outbox.Models;
using MongoRepository.Outbox.Repositories;
using MongoRepository.Outbox.UnitOfWork;

namespace MongoRepository.Outbox.Implementation;

/// <summary>
/// Implementation of the outbox service using MongoDB
/// </summary>
public class OutboxService : IOutboxService
{
    private readonly IRepository<OutboxMessage> _repository;
    private readonly IUnitOfWork? _currentUnitOfWork;
    private readonly ILogger<OutboxService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxService"/> class.
    /// </summary>
    /// <param name="repository">The outbox message repository</param>
    /// <param name="unitOfWork">The optional current unit of work (for transaction support)</param>
    /// <param name="logger">The logger</param>
    public OutboxService(
        IRepository<OutboxMessage> repository,
        ILogger<OutboxService> logger,
        IUnitOfWork? unitOfWork = null)
    {
        _repository = repository;
        _currentUnitOfWork = unitOfWork;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Adds a message to the outbox
    /// </summary>
    /// <typeparam name="T">The type of the message</typeparam>
    /// <param name="message">The message to add</param>
    /// <param name="messageType">Optional custom message type name (if not provided, the type name will be used)</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task AddMessageAsync<T>(T message, string? messageType = null)
    {
        try
        {
            var outboxMessage = CreateOutboxMessage(message, messageType);
            await _repository.AddAsync(outboxMessage);

            _logger.LogDebug("Added message to outbox: {MessageType} ({MessageId})",
                outboxMessage.MessageType, outboxMessage.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message to outbox: {MessageType}",
                messageType ?? typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Adds a message to the outbox using the current transaction
    /// </summary>
    /// <typeparam name="T">The type of the message</typeparam>
    /// <param name="message">The message to add</param>
    /// <param name="messageType">Optional custom message type name (if not provided, the type name will be used)</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task AddMessageToTransactionAsync<T>(T message, string? messageType = null)
    {
        if (_currentUnitOfWork == null)
        {
            throw new InvalidOperationException("No active transaction found. Call BeginTransactionAsync first.");
        }

        try
        {
            var outboxMessage = CreateOutboxMessage(message, messageType);
            var transactionalRepository = _currentUnitOfWork.GetRepository<OutboxMessage>();
            await transactionalRepository.AddAsync(outboxMessage);

            _logger.LogDebug("Added message to outbox transaction: {MessageType} ({MessageId})",
                outboxMessage.MessageType, outboxMessage.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message to outbox transaction: {MessageType}",
                messageType ?? typeof(T).Name);
            throw;
        }
    }

    private OutboxMessage CreateOutboxMessage<T>(T message, string? messageType)
    {
        var serializedContent = JsonSerializer.Serialize(message, _jsonOptions);
        var actualMessageType = messageType ?? typeof(T).FullName ?? typeof(T).Name;

        return new OutboxMessage
        {
            MessageType = actualMessageType,
            Content = serializedContent,
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            ProcessingAttempts = 0
        };
    }
}