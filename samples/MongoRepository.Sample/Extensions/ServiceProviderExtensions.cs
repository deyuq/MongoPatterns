using Microsoft.Extensions.Logging;
using MongoRepository.Sample.Data;

namespace MongoRepository.Sample.Extensions;

/// <summary>
/// Extension methods for IServiceProvider
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Initializes the database with retry logic
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="retryCount">Number of retry attempts</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider, int retryCount = 5)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Initializing database...");

        var retryDelay = TimeSpan.FromSeconds(5);
        var maxRetryDelay = TimeSpan.FromSeconds(30);

        for (int retry = 0; retry < retryCount; retry++)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<TodoSeeder>();
                await seeder.SeedAsync();
                logger.LogInformation("Database initialization completed successfully");
                return;
            }
            catch (Exception ex)
            {
                if (retry < retryCount - 1)
                {
                    logger.LogWarning(ex, "Database initialization failed (Attempt {Retry}/{RetryCount}). Retrying in {Delay}...",
                        retry + 1, retryCount, retryDelay);
                    await Task.Delay(retryDelay);

                    // Exponential backoff with cap
                    retryDelay = TimeSpan.FromSeconds(Math.Min(
                        retryDelay.TotalSeconds * 1.5,
                        maxRetryDelay.TotalSeconds));
                }
                else
                {
                    logger.LogError(ex, "Database initialization failed after {RetryCount} attempts", retryCount);
                    throw;
                }
            }
        }
    }
}