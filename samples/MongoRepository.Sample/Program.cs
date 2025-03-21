using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MongoRepository.Core.Extensions;
using MongoRepository.Core.Models;
using MongoRepository.Core.Repositories;
using MongoRepository.Core.UnitOfWork;
using MongoRepository.Outbox;
using MongoRepository.Outbox.Extensions;
using MongoRepository.Outbox.Models;
using MongoRepository.Sample.Data;
using MongoRepository.Sample.Handlers;
using MongoRepository.Sample.Messages;
using MongoRepository.Sample.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add MongoDB repository services
builder.Services.AddMongoRepository(builder.Configuration);

// Add outbox pattern services
builder.Services.AddOutboxPattern(
    builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017",
    builder.Configuration.GetValue<string>("MongoDbSettings:DatabaseName") ?? "TodoApp");

// Register message handlers
builder.Services.AddOutboxMessageHandler<TodoCreatedHandler, TodoCreatedMessage>();

// Add data seeder
builder.Services.AddTransient<TodoSeeder>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Initialize database with retry logic
await InitializeDatabaseAsync(app.Services, retryCount: 5);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map controllers
app.MapControllers();

// Define routes for Todo API
app.MapGet("/todos", async (IAdvancedRepository<TodoItem> repository) =>
{
    var todos = await repository.GetAllAsync();
    return Results.Ok(todos);
})
.WithName("GetAllTodos")
.WithOpenApi();

app.MapGet("/todos/{id}", async (string id, IRepository<TodoItem> repository) =>
{
    var todo = await repository.GetByIdAsync(id);
    if (todo == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(todo);
})
.WithName("GetTodoById")
.WithOpenApi();

// Create todo using outbox pattern
app.MapPost("/todos", async (
    TodoItem todo,
    IRepository<TodoItem> repository,
    IOutboxService outboxService) =>
{
    await repository.AddAsync(todo);

    // Publish a message to the outbox
    var message = new TodoCreatedMessage
    {
        TodoId = todo.Id,
        Title = todo.Title,
        CreatedAt = todo.CreatedAt
    };

    await outboxService.AddMessageAsync(message);

    return Results.Created($"/todos/{todo.Id}", todo);
})
.WithName("CreateTodo")
.WithOpenApi();

app.MapPut("/todos/{id}", async (string id, TodoItem updatedTodoItem, IRepository<TodoItem> repository) =>
{
    var todo = await repository.GetByIdAsync(id);
    if (todo == null)
    {
        return Results.NotFound();
    }

    updatedTodoItem.Id = id;
    await repository.UpdateAsync(updatedTodoItem);

    return Results.NoContent();
})
.WithName("UpdateTodo")
.WithOpenApi();

app.MapDelete("/todos/{id}", async (string id, IRepository<TodoItem> repository) =>
{
    var todo = await repository.GetByIdAsync(id);
    if (todo == null)
    {
        return Results.NotFound();
    }

    await repository.DeleteAsync(id);

    return Results.NoContent();
})
.WithName("DeleteTodo")
.WithOpenApi();

// Transaction example - create multiple todos in a transaction
app.MapPost("/todos/batch", async (
    IEnumerable<TodoItem> todos,
    IUnitOfWork unitOfWork,
    IOutboxService outboxService) =>
{
    try
    {
        await unitOfWork.BeginTransactionAsync();

        var repository = unitOfWork.GetRepository<TodoItem>();

        foreach (var todo in todos)
        {
            await repository.AddAsync(todo);

            // Add a message to the transaction
            var message = new TodoCreatedMessage
            {
                TodoId = todo.Id,
                Title = todo.Title,
                CreatedAt = todo.CreatedAt
            };

            await outboxService.AddMessageToTransactionAsync(message);
        }

        await unitOfWork.CommitTransactionAsync();

        return Results.Created("/todos", todos);
    }
    catch (Exception ex)
    {
        await unitOfWork.AbortTransactionAsync();
        return Results.Problem(ex.Message);
    }
})
.WithName("CreateTodosBatch")
.WithOpenApi();

// Advanced query example - paging
app.MapGet("/todos/paged", async (
    int page,
    int pageSize,
    IAdvancedRepository<TodoItem> repository) =>
{
    var pagedResult = await repository.GetPagedAsync(
        _ => true,
        t => t.CreatedAt,
        false,
        page,
        pageSize);

    return Results.Ok(pagedResult);
})
.WithName("GetPagedTodos")
.WithOpenApi();

// Advanced query example - using native MongoDB filter definitions
app.MapGet("/todos/advanced", async (
    int page,
    int pageSize,
    IAdvancedRepository<TodoItem> repository) =>
{
    // Example of using MongoDB filter builder
    var filterBuilder = Builders<TodoItem>.Filter;

    // Creating complex filters that may not translate well with LINQ expressions
    var filter = filterBuilder.And(
        filterBuilder.Regex(t => t.Title, new MongoDB.Bson.BsonRegularExpression("^T", "i")), // Starts with "T", case insensitive
        filterBuilder.Or(
            filterBuilder.Eq(t => t.IsCompleted, true),
            filterBuilder.Gt(t => t.CreatedAt, DateTime.UtcNow.AddDays(-7)) // Created in the last week
        )
    );

    // Using MongoDB sort builder
    var sort = Builders<TodoItem>.Sort.Descending(t => t.CreatedAt);

    // Get paged results with native MongoDB definitions
    var pagedResult = await repository.GetPagedWithDefinitionAsync(
        filter,
        sort,
        page,
        pageSize);

    return Results.Ok(pagedResult);
})
.WithName("GetAdvancedFilteredTodos")
.WithOpenApi();

// Advanced query example - using native MongoDB projection
app.MapGet("/todos/projected", async (
    IAdvancedRepository<TodoItem> repository) =>
{
    // Example of using MongoDB filter and projection builders
    var filterBuilder = Builders<TodoItem>.Filter;
    var projectionBuilder = Builders<TodoItem>.Projection;

    // Complex filter
    var filter = filterBuilder.And(
        filterBuilder.Exists(t => t.CompletedAt),
        filterBuilder.Ne(t => t.CompletedAt, null)
    );

    // Define projection to TodoItemSummary
    var projection = projectionBuilder.Expression(t => new TodoItemSummary
    {
        Id = t.Id,
        Title = t.Title,
        IsCompleted = t.IsCompleted,
        CompletedAt = t.CompletedAt
    });

    // Sort by completion date
    var sort = Builders<TodoItem>.Sort.Descending(t => t.CompletedAt!);

    // Get projected results with limit
    var results = await repository.GetWithDefinitionAsync<TodoItemSummary>(
        filter,
        projection,
        sort,
        10);

    return Results.Ok(results);
})
.WithName("GetProjectedTodos")
.WithOpenApi();

// Example of using filter definitions
app.MapGet("/todos/filter", async (
    [FromQuery] string? titleContains,
    [FromQuery] bool? isCompleted,
    IAdvancedRepository<TodoItem> repository) =>
{
    var filterBuilder = Builders<TodoItem>.Filter.Empty;

    if (!string.IsNullOrEmpty(titleContains))
    {
        filterBuilder &= Builders<TodoItem>.Filter.Regex(t => t.Title, new BsonRegularExpression(titleContains, "i"));
    }

    if (isCompleted.HasValue)
    {
        filterBuilder &= Builders<TodoItem>.Filter.Eq(t => t.IsCompleted, isCompleted.Value);
    }

    var todos = await repository.GetWithDefinitionAsync(filterBuilder);
    return Results.Ok(todos);
});

// Example of using projections
app.MapGet("/todos/summary", async (IAdvancedRepository<TodoItem> repository) =>
{
    // Simplified to use expressions instead of projection definitions
    var todos = await repository.GetAllAsync();
    var summaries = todos.Select(t => new TodoItemSummary
    {
        Id = t.Id,
        Title = t.Title,
        IsCompleted = t.IsCompleted,
        CompletedAt = t.CompletedAt
    }).ToList();

    return Results.Ok(summaries);
});

// Example of advanced querying with pagination and sorting
app.MapGet("/todos/advanced-query", async (
    [FromQuery] string? titleContains,
    [FromQuery] bool? isCompleted,
    IAdvancedRepository<TodoItem> repository,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string sortBy = "CreatedAt",
    [FromQuery] bool sortAscending = false) =>
{
    var filterBuilder = Builders<TodoItem>.Filter.Empty;

    if (!string.IsNullOrEmpty(titleContains))
    {
        filterBuilder &= Builders<TodoItem>.Filter.Regex(t => t.Title, new BsonRegularExpression(titleContains, "i"));
    }

    if (isCompleted.HasValue)
    {
        filterBuilder &= Builders<TodoItem>.Filter.Eq(t => t.IsCompleted, isCompleted.Value);
    }

    var sortDefinition = sortAscending
        ? Builders<TodoItem>.Sort.Ascending(sortBy)
        : Builders<TodoItem>.Sort.Descending(sortBy);

    var todos = await repository.GetPagedWithDefinitionAsync(
        filterBuilder,
        sortDefinition,
        page,
        pageSize);

    return Results.Ok(new
    {
        Page = page,
        PageSize = pageSize,
        Total = await repository.CountAsync(expr => true),
        Items = todos
    });
});

// Example of full-text search
app.MapGet("/todos/search", async (
    [FromQuery] string searchText,
    IAdvancedRepository<TodoItem> repository) =>
{
    var textSearchFilter = Builders<TodoItem>.Filter.Text(searchText);
    var todos = await repository.GetWithDefinitionAsync(textSearchFilter);
    return Results.Ok(todos);
});

// Monitoring endpoint for outbox messages
app.MapGet("/outbox/status", async (MongoRepository.Core.Repositories.IRepository<OutboxMessage> repository) =>
{
    var pendingFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Pending);
    var processingFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Processing);
    var processedFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Processed);
    var failedFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Failed);
    var abandonedFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Abandoned);

    var pendingCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Pending);
    var processingCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Processing);
    var processedCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Processed);
    var failedCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Failed);
    var abandonedCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Abandoned);

    return Results.Ok(new
    {
        Pending = pendingCount,
        Processing = processingCount,
        Processed = processedCount,
        Failed = failedCount,
        Abandoned = abandonedCount,
        Total = pendingCount + processingCount + processedCount + failedCount + abandonedCount
    });
});

app.Run();

// Helper method to seed database with retry logic
async Task InitializeDatabaseAsync(IServiceProvider services, int retryCount = 5)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Initializing database...");

    var retryDelay = TimeSpan.FromSeconds(5);
    var maxRetryDelay = TimeSpan.FromSeconds(30);

    for (int retry = 0; retry < retryCount; retry++)
    {
        try
        {
            using var scope = services.CreateScope();
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
