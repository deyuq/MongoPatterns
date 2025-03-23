using MongoDB.Driver;
using MongoPatterns.Outbox.Models;
using MongoPatterns.Outbox.Settings;

namespace MongoPatterns.Outbox.StartupTasks;

/// <summary>
///     Task to create the outbox message collection
/// </summary>
internal class CreateOutboxCollectionTask : IOutboxStartupTask
{
    private readonly IMongoDatabase _database;
    private readonly OutboxSettings _outboxSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CreateOutboxCollectionTask" /> class
    /// </summary>
    /// <param name="database">The MongoDB database</param>
    /// <param name="outboxSettings">The outbox settings</param>
    public CreateOutboxCollectionTask(IMongoDatabase database, OutboxSettings outboxSettings)
    {
        _database = database;
        _outboxSettings = outboxSettings;
    }

    /// <inheritdoc />
    public void Execute()
    {
        var collections = _database.ListCollectionNames().ToList();
        var collectionName = typeof(OutboxMessage).Name.ToLower();

        // Apply the collection prefix if configured
        if (!string.IsNullOrEmpty(_outboxSettings.CollectionPrefix))
            collectionName = $"{_outboxSettings.CollectionPrefix}_{collectionName}";

        if (!collections.Contains(collectionName))
        {
            _database.CreateCollection(collectionName);

            // Create indexes
            var collection = _database.GetCollection<OutboxMessage>(collectionName);
            var statusIndexBuilder = Builders<OutboxMessage>.IndexKeys.Ascending(m => m.Status);
            var createdAtIndexBuilder = Builders<OutboxMessage>.IndexKeys.Ascending(m => m.CreatedAt);
            var messageTypeIndexBuilder = Builders<OutboxMessage>.IndexKeys.Ascending(m => m.MessageType);

            collection.Indexes.CreateOne(new CreateIndexModel<OutboxMessage>(statusIndexBuilder));
            collection.Indexes.CreateOne(new CreateIndexModel<OutboxMessage>(createdAtIndexBuilder));
            collection.Indexes.CreateOne(new CreateIndexModel<OutboxMessage>(messageTypeIndexBuilder));
        }
    }
}