using System.Threading;
using Microsoft.Extensions.Logging;
using MongoRepository.Core.Extensions;
using MongoRepository.Core.Models;
using MongoRepository.Core.Repositories;
using MongoRepository.Core.UnitOfWork;
using MongoRepository.Sample.Data;
using MongoRepository.Sample.Models;

var builder = WebApplication.CreateBuilder(args);

// Add MongoDB repository services
builder.Services.AddMongoRepository(builder.Configuration);

// Add data seeder
builder.Services.AddTransient<TodoSeeder>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.MapPost("/todos", async (TodoItem todoItem, IRepository<TodoItem> repository) =>
{
    await repository.AddAsync(todoItem);
    return Results.Created($"/todos/{todoItem.Id}", todoItem);
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

// Transaction example
app.MapPost("/todos/batch", async (List<TodoItem> todoItems, IUnitOfWork unitOfWork) =>
{
    try
    {
        await unitOfWork.BeginTransactionAsync();

        var repository = unitOfWork.GetRepository<TodoItem>();

        foreach (var todoItem in todoItems)
        {
            await repository.AddAsync(todoItem);
        }

        await unitOfWork.CommitTransactionAsync();

        return Results.Created("/todos", todoItems);
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
