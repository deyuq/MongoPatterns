using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoPatterns.Repository.Models;

/// <summary>
/// Base class for all entities
/// </summary>
public abstract class Entity : IEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
}