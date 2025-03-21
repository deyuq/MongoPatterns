using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoRepository.Core.Repositories;
using MongoRepository.Core.Settings;
using MongoRepository.Core.UnitOfWork;
using MongoRepository.Outbox.Infrastructure;
using MongoRepository.Outbox.Models;

namespace MongoRepository.Outbox.Extensions;

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
        services.AddScoped<IRepository<OutboxMessage>>(provider =>
        {
            var settings = provider.GetRequiredService<MongoDbSettings>();
            return new MongoRepository<OutboxMessage>(settings);
        });

        // Register advanced repository for OutboxMessage
        services.AddScoped<IAdvancedRepository<OutboxMessage>>(provider =>
        {
            var settings = provider.GetRequiredService<MongoDbSettings>();
            return new MongoAdvancedRepository<OutboxMessage>(settings);
        });

        // Create the outbox message collection if it doesn't exist
        services.AddSingleton<IStartupTask>(provider =>
        {
            var database = provider.GetRequiredService<IMongoDatabase>();
            return new CreateOutboxCollectionTask(database);
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

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateOutboxCollectionTask"/> class
        /// </summary>
        /// <param name="database">The MongoDB database</param>
        public CreateOutboxCollectionTask(IMongoDatabase database)
        {
            _database = database;
        }

        /// <inheritdoc/>
        public void Execute()
        {
            var collections = _database.ListCollectionNames().ToList();
            if (!collections.Contains("OutboxMessage"))
            {
                _database.CreateCollection("OutboxMessage");

                // Create indexes
                var collection = _database.GetCollection<OutboxMessage>("OutboxMessage");
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