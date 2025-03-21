namespace MongoRepository.Core.Models;

/// <summary>
/// Base interface for all entities
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the entity
    /// </summary>
    string Id { get; set; }
}