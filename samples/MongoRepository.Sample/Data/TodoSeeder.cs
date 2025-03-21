using Microsoft.Extensions.Logging;
using MongoRepository.Core.Repositories;
using MongoRepository.Sample.Models;

namespace MongoRepository.Sample.Data;

public class TodoSeeder
{
    private readonly IRepository<TodoItem> _repository;
    private readonly ILogger<TodoSeeder> _logger;

    public TodoSeeder(IRepository<TodoItem> repository, ILogger<TodoSeeder> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting to seed todo data...");

            // Check if we already have any todos
            var count = await _repository.CountAsync();
            _logger.LogInformation("Current todo count: {Count}", count);

            if (count > 0)
            {
                _logger.LogInformation("Database already has data, skipping seeding");
                return;
            }

            _logger.LogInformation("Seeding initial todo data...");

            // Seed initial data
            var todos = new List<TodoItem>
            {
                new TodoItem
                {
                    Title = "Learn MongoDB Repository Pattern",
                    Description = "Study the implementation of the MongoDB Repository pattern",
                    IsCompleted = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    CompletedAt = DateTime.UtcNow.AddDays(-5)
                },
                new TodoItem
                {
                    Title = "Implement API endpoints",
                    Description = "Create REST API endpoints for the todo application",
                    IsCompleted = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-8),
                    CompletedAt = DateTime.UtcNow.AddDays(-3)
                },
                new TodoItem
                {
                    Title = "Test transaction support",
                    Description = "Verify that MongoDB transactions are working correctly",
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new TodoItem
                {
                    Title = "Deploy application to production",
                    Description = "Prepare Docker images and deploy the application",
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new TodoItem
                {
                    Title = "Write documentation",
                    Description = "Document the MongoDB Repository pattern implementation",
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            await _repository.AddRangeAsync(todos);

            _logger.LogInformation("Successfully seeded {Count} todo items", todos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding todo data: {Message}", ex.Message);
            throw; // Re-throw to ensure the error is visible
        }
    }
}