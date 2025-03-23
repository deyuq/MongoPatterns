namespace MongoPatterns.Outbox.Models;

/// <summary>
/// Status of an outbox message
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// Message is pending to be processed
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Message is currently being processed
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Message was processed successfully
    /// </summary>
    Processed = 2,

    /// <summary>
    /// Processing the message failed
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Message processing has been abandoned after multiple retries
    /// </summary>
    Abandoned = 4
}