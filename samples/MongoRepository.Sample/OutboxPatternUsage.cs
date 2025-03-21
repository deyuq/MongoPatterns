using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using MongoRepository.Core.Extensions;
using MongoRepository.Outbox.Extensions;
using MongoRepository.Outbox;
using MongoRepository.Sample.Handlers;
using MongoRepository.Sample.Messages;
using MongoRepository.Core.Repositories;
using MongoRepository.Core.UnitOfWork;
using MongoRepository.Sample.Models;

namespace MongoRepository.Sample;

/// <summary>
/// Sample showing how to use MongoRepository.Core with MongoRepository.Outbox
/// </summary>
public static class OutboxPatternUsage
{
    /// <summary>
    /// Configures services for both MongoRepository.Core and MongoRepository.Outbox
    /// </summary>
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register MongoRepository.Core services
        services.AddMongoRepository(configuration);

        // Register MongoRepository.Outbox services
        services.AddOutboxPattern(
            configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017",
            configuration.GetValue<string>("MongoDbSettings:DatabaseName") ?? "TodoApp");

        // Register message handlers
        services.AddOutboxMessageHandler<TodoCreatedHandler, TodoCreatedMessage>();
    }

    /// <summary>
    /// Configures the application for both MongoRepository.Core and MongoRepository.Outbox
    /// </summary>
    public static void ConfigureApp(WebApplication app)
    {
        // Initialize outbox collections and indexes
        app.Services.UseOutboxPattern();
    }

    /// <summary>
    /// Example of sending a message from Core repository to Outbox
    /// </summary>
    public static async Task SendMessageExample(
        IRepository<TodoItem> repository,
        IOutboxService outboxService)
    {
        // Create a new TodoItem
        var todo = new TodoItem
        {
            Title = "Learn Outbox Pattern",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        // Add the entity to MongoDB
        await repository.AddAsync(todo);

        // Send a message to the outbox
        var message = new TodoCreatedMessage
        {
            TodoId = todo.Id,
            Title = todo.Title,
            CreatedAt = todo.CreatedAt
        };

        await outboxService.AddMessageAsync(message);
    }

    /// <summary>
    /// Example of using Unit of Work with Outbox for transactions
    /// </summary>
    public static async Task TransactionExample(
        IUnitOfWork unitOfWork,
        IOutboxService outboxService)
    {
        try
        {
            // Begin a transaction that will coordinate both entity changes
            // and outbox message creation
            await unitOfWork.BeginTransactionAsync();

            // Get a repository for the TodoItem entity
            var repository = unitOfWork.GetRepository<TodoItem>();

            // Create and add multiple entities in the transaction
            for (int i = 0; i < 5; i++)
            {
                var todo = new TodoItem
                {
                    Title = $"Transaction task #{i}",
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await repository.AddAsync(todo);

                // Add a message to the transaction for each entity
                var message = new TodoCreatedMessage
                {
                    TodoId = todo.Id,
                    Title = todo.Title,
                    CreatedAt = todo.CreatedAt
                };

                // This ensures the message is stored within the same transaction
                await outboxService.AddMessageToTransactionAsync(message);
            }

            // Commit the transaction to persist both the entities and messages
            await unitOfWork.CommitTransactionAsync();
        }
        catch (Exception)
        {
            // If anything fails, rollback the entire transaction
            await unitOfWork.AbortTransactionAsync();
            throw;
        }
    }
}