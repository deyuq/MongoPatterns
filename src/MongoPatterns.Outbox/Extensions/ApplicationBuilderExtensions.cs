using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoPatterns.Outbox.StartupTasks;

namespace MongoPatterns.Outbox.Extensions;

/// <summary>
///     Extension methods for IApplicationBuilder
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    ///     Initializes the outbox pattern collections and indexes
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder</returns>
    public static IApplicationBuilder UseOutboxPattern(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<IOutboxStartupTask>>();
        var startupTasks = app.ApplicationServices.GetServices<IOutboxStartupTask>().ToList();

        if (startupTasks.Any())
        {
            logger.LogInformation("Executing {Count} startup tasks", startupTasks.Count);

            foreach (var task in startupTasks)
                try
                {
                    task.Execute();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error executing startup task {TaskName}", task.GetType().Name);
                    throw;
                }

            logger.LogInformation("All startup tasks completed successfully");
        }

        return app;
    }
}