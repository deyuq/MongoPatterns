using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoRepository.Core.Settings;
using MongoRepository.Sample.Data;
using System;
using System.Threading.Tasks;

namespace MongoRepository.Sample.Extensions;

/// <summary>
/// Extension methods for IServiceProvider
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Initialize the database with retry logic
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="retryCount">Number of retry attempts</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider, int retryCount = 5)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var random = new Random();

        logger.LogInformation("Initializing database...");

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                // Ensure MongoDB connection is established
                await EnsureMongoDbConnectionAsync(serviceProvider);

                // Seed the database with sample data
                using var scope = serviceProvider.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<TodoSeeder>();
                await seeder.SeedAsync();

                logger.LogInformation("Database initialization completed successfully");
                return;
            }
            catch (Exception ex)
            {
                if (i == retryCount - 1)
                {
                    logger.LogError(ex, "Failed to initialize database after {RetryCount} attempts", retryCount);
                    throw;
                }

                // Calculate delay with exponential backoff
                var delay = (int)Math.Pow(2, i) * 1000 + random.Next(100, 1000);
                logger.LogWarning(ex, "Database initialization attempt {Attempt} failed. Retrying in {Delay}ms...", i + 1, delay);
                await Task.Delay(delay);
            }
        }
    }

    private static async Task EnsureMongoDbConnectionAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var mongoClient = serviceProvider.GetRequiredService<IMongoClient>();
        var settings = serviceProvider.GetRequiredService<MongoDbSettings>();

        logger.LogInformation("Checking MongoDB connection...");

        try
        {
            // First try to ping the server
            var database = mongoClient.GetDatabase(settings.DatabaseName);
            var pingResult = await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            logger.LogInformation("MongoDB ping successful: {PingResult}", pingResult.ToString());

            // Check replica set status
            var admin = mongoClient.GetDatabase("admin");
            var replicaSetStatus = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetGetStatus", 1));

            // Check if there's a primary node
            if (replicaSetStatus.Contains("members") && replicaSetStatus["members"].IsBsonArray)
            {
                var members = replicaSetStatus["members"].AsBsonArray;
                var primaryFound = false;

                foreach (BsonDocument member in members)
                {
                    if (member.Contains("state") && member["state"] == 1) // state 1 = PRIMARY
                    {
                        logger.LogInformation("Found primary node: {NodeName}", member["name"].AsString);
                        primaryFound = true;
                        break;
                    }
                }

                if (primaryFound)
                {
                    logger.LogInformation("MongoDB replica set is ready with a primary node");
                }
                else
                {
                    logger.LogWarning("No primary node found in the replica set");
                }
            }
            else
            {
                logger.LogWarning("Could not verify replica set members");
            }

            logger.LogInformation("MongoDB connection successfully established to database: {DatabaseName}", settings.DatabaseName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to MongoDB");
            throw;
        }
    }
}