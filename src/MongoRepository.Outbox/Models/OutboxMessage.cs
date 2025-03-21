using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoRepository.Core.Models;

namespace MongoRepository.Outbox.Models;

/// <summary>
/// Represents a message to be published through the outbox pattern
/// </summary>
public class OutboxMessage : Entity
{
    /// <summary>
    /// Gets or sets the type/name of the message
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the message was last processed (or null if not processed)
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of processing attempts made
    /// </summary>
    public int ProcessingAttempts { get; set; }

    /// <summary>
    /// Gets or sets the error message from the last processing attempt (if any)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the status of the message
    /// </summary>
    public OutboxMessageStatus Status { get; set; }
}