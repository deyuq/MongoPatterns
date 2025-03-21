using System;
using System.Threading.Tasks;

namespace MongoRepository.Outbox;

/// <summary>
/// Interface for the outbox service, which handles storing and retrieving messages
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Adds a message to the outbox
    /// </summary>
    /// <typeparam name="T">The type of the message</typeparam>
    /// <param name="message">The message to add</param>
    /// <param name="messageType">Optional custom message type name (if not provided, the type name will be used)</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task AddMessageAsync<T>(T message, string? messageType = null);

    /// <summary>
    /// Adds a message to the outbox using the current transaction
    /// </summary>
    /// <typeparam name="T">The type of the message</typeparam>
    /// <param name="message">The message to add</param>
    /// <param name="messageType">Optional custom message type name (if not provided, the type name will be used)</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task AddMessageToTransactionAsync<T>(T message, string? messageType = null);
}