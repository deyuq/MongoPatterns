namespace MongoPatterns.Sample.Models;

/// <summary>
/// Represents a summary view of a TodoItem for projection operations
/// </summary>
public class TodoItemSummary
{
    /// <summary>
    /// Gets or sets the ID of the todo item
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the todo item
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the todo item is completed
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets when the todo item was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}