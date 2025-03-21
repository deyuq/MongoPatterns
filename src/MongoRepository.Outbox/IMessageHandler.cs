namespace MongoRepository.Outbox;

/// <summary>
/// Marker interface for message handlers
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Gets the message type this handler can process
    /// </summary>
    string MessageType { get; }
}

/// <summary>
/// Interface for handling outbox messages of a specific type
/// </summary>
/// <typeparam name="T">The type of message this handler can process</typeparam>
public interface IMessageHandler<T> : IMessageHandler
{
    /// <summary>
    /// Handles a message of the specified type
    /// </summary>
    /// <param name="message">The message to handle</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task HandleAsync(T message);
}