using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoPatterns.Outbox.Models;
using MongoPatterns.Outbox.Repositories;
using MongoPatterns.Outbox.Settings;
using MongoPatterns.Repository.Repositories;
using MongoPatterns.Repository.Settings;
using MongoPatterns.Repository.UnitOfWork;
using MongoPatterns.Outbox.Infrastructure;

namespace MongoPatterns.Outbox.Extensions;

/// <summary>
/// Extension methods for MongoDB configuration
/// </summary>
public static class MongoDbExtensions
{
    /// <summary>
    /// Adds MongoDB repositories and unit of work for the outbox pattern
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">The MongoDB connection string</param>
    /// <param name="databaseName">The MongoDB database name</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddMongoDbOutbox(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        // Register MongoDB client and database
        services.AddSingleton<IMongoClient>(provider => new MongoClient(connectionString));
        services.AddSingleton<IMongoDatabase>(provider =>
        {
            var client = provider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(databaseName);
        });

        // Register MongoDB settings
        services.AddSingleton<MongoDbSettings>(new MongoDbSettings
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName
        });

        // Register unit of work and repositories
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        // Register specialized outbox repositories
        services.AddScoped<IRepository<OutboxMessage>>(provider =>
        {
            var mongoSettings = provider.GetRequiredService<MongoDbSettings>();
            var outboxSettings = provider.GetRequiredService<OutboxSettings>();
            return new OutboxRepository(mongoSettings, outboxSettings);
        });

        // Register specialized advanced repository for OutboxMessage
        services.AddScoped<IAdvancedRepository<OutboxMessage>>(provider =>
        {
            var mongoSettings = provider.GetRequiredService<MongoDbSettings>();
            var outboxSettings = provider.GetRequiredService<OutboxSettings>();
            return new OutboxAdvancedRepository(mongoSettings, outboxSettings);
        });

        // Create the outbox message collection if it doesn't exist
        services.AddSingleton<IStartupTask>(provider =>
        {
            var database = provider.GetRequiredService<IMongoDatabase>();
            var outboxSettings = provider.GetRequiredService<OutboxSettings>();
            return new CreateOutboxCollectionTask(database, outboxSettings);
        });

        return services;
    }

    /// <summary>
    /// Adds MongoDB repositories and unit of work for the outbox pattern from configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="sectionName">The configuration section name (default: "MongoDbSettings")</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddMongoDbOutbox(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "MongoDbSettings")
    {
        var settings = configuration.GetSection(sectionName).Get<MongoDbConfigSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"MongoDB settings not found in configuration section '{sectionName}'");
        }

        return services.AddMongoDbOutbox(settings.ConnectionString, settings.DatabaseName);
    }

    /// <summary>
    /// Interface for tasks that should run at application startup
    /// </summary>
    public interface IStartupTask
    {
        /// <summary>
        /// Executes the startup task
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Task to create the outbox message collection
    /// </summary>
    private class CreateOutboxCollectionTask : IStartupTask
    {
        private readonly IMongoDatabase _database;
        private readonly OutboxSettings _outboxSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateOutboxCollectionTask"/> class
        /// </summary>
        /// <param name="database">The MongoDB database</param>
        /// <param name="outboxSettings">The outbox settings</param>
        public CreateOutboxCollectionTask(IMongoDatabase database, OutboxSettings outboxSettings)
        {
            _database = database;
            _outboxSettings = outboxSettings;
        }

        /// <inheritdoc/>
        public void Execute()
        {
            var collections = _database.ListCollectionNames().ToList();
            string collectionName = typeof(OutboxMessage).Name.ToLower();

            // Apply the collection prefix if configured
            if (!string.IsNullOrEmpty(_outboxSettings.CollectionPrefix))
            {
                collectionName = $"{_outboxSettings.CollectionPrefix}_{collectionName}";
            }

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

    /// <summary>
    /// MongoDB settings for configuration
    /// </summary>
    public class MongoDbConfigSettings
    {
        /// <summary>
        /// Gets or sets the MongoDB connection string
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MongoDB database name
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;
    }
}