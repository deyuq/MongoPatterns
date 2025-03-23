using MongoDB.Driver;
using MongoPatterns.Outbox.Models;
using MongoPatterns.Outbox.Settings;
using MongoPatterns.Repository.Repositories;
using MongoPatterns.Repository.Settings;

namespace MongoPatterns.Outbox.Repositories;

/// <summary>
///     Specialized advanced repository for outbox messages that supports microservice-specific collection prefixes
/// </summary>
public class OutboxAdvancedRepository : MongoAdvancedRepository<OutboxMessage>
{
    private readonly OutboxSettings _outboxSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OutboxAdvancedRepository" /> class.
    /// </summary>
    /// <param name="mongoSettings">MongoDB connection settings</param>
    /// <param name="outboxSettings">Outbox pattern settings</param>
    /// <param name="session">Optional session for transaction support</param>
    public OutboxAdvancedRepository(
        MongoDbSettings mongoSettings,
        OutboxSettings outboxSettings,
        IClientSessionHandle? session = null)
        : base(mongoSettings, session)
    {
        _outboxSettings = outboxSettings;
    }

    /// <summary>
    ///     Gets the MongoDB collection for outbox messages with proper prefix support
    /// </summary>
    /// <param name="database">The MongoDB database</param>
    /// <returns>The MongoDB collection for outbox messages</returns>
    protected override IMongoCollection<OutboxMessage> GetCollection(IMongoDatabase database)
    {
        var collectionName = nameof(OutboxMessage).ToLower();

        // Apply the outbox-specific prefix if configured
        if (!string.IsNullOrEmpty(_outboxSettings.CollectionPrefix))
            collectionName = $"{_outboxSettings.CollectionPrefix}_{collectionName}";

        return database.GetCollection<OutboxMessage>(collectionName);
    }

    /// <summary>
    ///     Atomically finds a document and updates it in a single operation
    /// </summary>
    /// <param name="filter">The filter to find the document</param>
    /// <param name="update">The update to apply</param>
    /// <param name="options">The options for the operation</param>
    /// <returns>The document before or after the update as specified in the options</returns>
    public async Task<OutboxMessage?> FindOneAndUpdateAsync(
        FilterDefinition<OutboxMessage> filter,
        UpdateDefinition<OutboxMessage> update,
        FindOneAndUpdateOptions<OutboxMessage>? options = null)
    {
        return await Collection.FindOneAndUpdateAsync(filter, update, options);
    }
}