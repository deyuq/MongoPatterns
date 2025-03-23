using System.ComponentModel.DataAnnotations;

namespace MongoPatterns.Sample.Models;

/// <summary>
/// Model for creating a new todo item
/// </summary>
public class TodoCreateModel
{
    /// <summary>
    /// Gets or sets the title of the todo item
    /// </summary>
    [Required]
    public string Title { get; set; } = string.Empty;
}