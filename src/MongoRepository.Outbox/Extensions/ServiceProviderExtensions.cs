using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoRepository.Outbox.Infrastructure;

namespace MongoRepository.Outbox.Extensions;

/// <summary>
/// Extension methods for IServiceProvider
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Initializes the outbox pattern collections and indexes
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The service provider</returns>
    public static IServiceProvider UseOutboxPattern(this IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<IStartupTask>>();
        var startupTasks = serviceProvider.GetServices<IStartupTask>().ToList();

        if (startupTasks.Any())
        {
            logger.LogInformation("Executing {Count} startup tasks", startupTasks.Count);

            foreach (var task in startupTasks)
            {
                try
                {
                    task.Execute();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error executing startup task {TaskName}", task.GetType().Name);
                    throw;
                }
            }

            logger.LogInformation("All startup tasks completed successfully");
        }

        return serviceProvider;
    }
}