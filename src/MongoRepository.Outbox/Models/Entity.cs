using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoRepository.Outbox.Models;

/// <summary>
/// Base class for entities with MongoDB ID
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
}