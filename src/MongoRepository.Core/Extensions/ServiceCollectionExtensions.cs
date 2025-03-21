using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoRepository.Core.Repositories;
using MongoRepository.Core.Settings;
using MongoRepository.Core.UnitOfWork;

namespace MongoRepository.Core.Extensions;

/// <summary>
/// Extension methods for IServiceCollection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MongoDB repository services to the specified <see cref="IServiceCollection" />
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configuration">The configuration being bound.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddMongoRepository(this IServiceCollection services, IConfiguration configuration)
    {
        // Register MongoDB settings
        services.Configure<MongoDbSettings>(options =>
            configuration.GetSection(nameof(MongoDbSettings)).Bind(options));

        services.AddSingleton<IMongoDbSettings>(sp =>
            sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);

        // Register repositories and unit of work
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
        services.AddScoped(typeof(IAdvancedRepository<>), typeof(MongoAdvancedRepository<>));
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        return services;
    }

    /// <summary>
    /// Adds MongoDB repository services to the specified <see cref="IServiceCollection" />
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="settings">The MongoDB settings.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddMongoRepository(this IServiceCollection services, IMongoDbSettings settings)
    {
        // Register MongoDB settings
        services.AddSingleton(settings);

        // Register repositories and unit of work
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
        services.AddScoped(typeof(IAdvancedRepository<>), typeof(MongoAdvancedRepository<>));
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        return services;
    }
}