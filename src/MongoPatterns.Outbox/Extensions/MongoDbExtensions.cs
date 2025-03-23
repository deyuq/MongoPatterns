using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoPatterns.Outbox.Models;
using MongoPatterns.Outbox.Repositories;
using MongoPatterns.Outbox.Settings;
using MongoPatterns.Outbox.StartupTasks;
using MongoPatterns.Repository.Repositories;
using MongoPatterns.Repository.Settings;
using MongoPatterns.Repository.UnitOfWork;

namespace MongoPatterns.Outbox.Extensions;

/// <summary>
///     Extension methods for MongoDB configuration
/// </summary>
public static class MongoDbExtensions
{
    /// <summary>
    ///     Adds MongoDB repositories and unit of work for the outbox pattern
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">The MongoDB connection string</param>
    /// <param name="databaseName">The MongoDB database name</param>
    /// <returns>The service collection</returns>
    private static IServiceCollection AddMongoDbOutbox(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        // Register MongoDB client and database
        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddSingleton<IMongoDatabase>(provider =>
        {
            var client = provider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(databaseName);
        });

        // Register MongoDB settings
        services.AddSingleton(new MongoDbSettings
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName
        });

        // Register unit of work and repositories
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        // Create the OutboxAdvancedRepository instance once per scope
        services.AddScoped<OutboxAdvancedRepository>(provider =>
        {
            var mongoSettings = provider.GetRequiredService<MongoDbSettings>();
            var outboxSettings = provider.GetRequiredService<OutboxSettings>();
            return new OutboxAdvancedRepository(mongoSettings, outboxSettings);
        });

        // Register the OutboxAdvancedRepository for both repository interfaces
        services.AddScoped<IRepository<OutboxMessage>>(provider =>
            provider.GetRequiredService<OutboxAdvancedRepository>());

        services.AddScoped<IAdvancedRepository<OutboxMessage>>(provider =>
            provider.GetRequiredService<OutboxAdvancedRepository>());

        // Create the outbox message collection if it doesn't exist
        services.AddSingleton<IOutboxStartupTask>(provider =>
        {
            var database = provider.GetRequiredService<IMongoDatabase>();
            var outboxSettings = provider.GetRequiredService<OutboxSettings>();
            return new CreateOutboxCollectionTask(database, outboxSettings);
        });

        return services;
    }

    /// <summary>
    ///     Adds MongoDB repositories and unit of work for the outbox pattern from configuration
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
        var settings = configuration.GetSection(sectionName).Get<MongoDbSettings>();
        if (settings == null)
            throw new InvalidOperationException($"MongoDB settings not found in configuration section '{sectionName}'");

        return services.AddMongoDbOutbox(settings.ConnectionString, settings.DatabaseName);
    }
}