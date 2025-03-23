using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoPatterns.Outbox.Implementation;
using MongoPatterns.Outbox.Settings;

namespace MongoPatterns.Outbox.Extensions;

/// <summary>
/// Extension methods for registering outbox services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds outbox pattern services with default settings.
    /// This method assumes MongoDB has already been configured elsewhere.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddOutboxPattern(this IServiceCollection services)
    {
        return services.AddOutboxPattern(new OutboxSettings());
    }

    /// <summary>
    /// Adds outbox pattern services with default settings
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">MongoDB connection string</param>
    /// <param name="databaseName">MongoDB database name</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddOutboxPattern(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        services.AddMongoDbOutbox(connectionString, databaseName);
        return services.AddOutboxPattern(new OutboxSettings());
    }

    /// <summary>
    /// Adds outbox pattern services with settings from configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="mongoSectionName">MongoDB settings section name (default: "MongoDbSettings")</param>
    /// <param name="outboxSectionName">Outbox settings section name (default: "OutboxSettings")</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddOutboxPattern(
        this IServiceCollection services,
        IConfiguration configuration,
        string mongoSectionName = "MongoDbSettings",
        string outboxSectionName = "OutboxSettings")
    {
        services.AddMongoDbOutbox(configuration, mongoSectionName);

        var settings = configuration.GetSection(outboxSectionName).Get<OutboxSettings>() ?? new OutboxSettings();
        return services.AddOutboxPattern(settings);
    }

    /// <summary>
    /// Adds outbox pattern services with the specified settings
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="settings">The outbox settings</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddOutboxPattern(
        this IServiceCollection services,
        OutboxSettings settings)
    {
        // Register settings
        services.AddSingleton(settings);

        // Register outbox service
        services.AddScoped<IOutboxService, OutboxService>();

        // Register outbox processor (background service)
        if (settings.AutoStartProcessor)
        {
            services.AddHostedService<OutboxProcessor>();
        }

        return services;
    }

    /// <summary>
    /// Registers a message handler for the outbox pattern
    /// </summary>
    /// <typeparam name="THandler">The type of the message handler</typeparam>
    /// <typeparam name="TMessage">The type of message this handler can process</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="messageType">Optional custom message type name (if not provided, the type name will be used)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddOutboxMessageHandler<THandler, TMessage>(
        this IServiceCollection services,
        string? messageType = null)
        where THandler : class, IMessageHandler<TMessage>
    {
        // Register the handler implementation
        services.AddTransient<THandler>();

        // Create and register a provider that supplies the actual message type name
        services.AddTransient<IMessageHandler>(sp =>
        {
            var handler = sp.GetRequiredService<THandler>();
            return handler;
        });

        // Register the generic interface
        services.AddTransient<IMessageHandler<TMessage>>(sp =>
        {
            var handler = sp.GetRequiredService<THandler>();
            return handler;
        });

        return services;
    }
}