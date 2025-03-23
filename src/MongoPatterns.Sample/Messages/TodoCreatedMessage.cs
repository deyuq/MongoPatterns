using System;

namespace MongoPatterns.Sample.Messages;

/// <summary>
/// Message that is sent when a todo item is created
/// </summary>
public class TodoCreatedMessage
{
    /// <summary>
    /// Gets or sets the ID of the todo item
    /// </summary>
    public string TodoId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the todo item
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the todo item was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}