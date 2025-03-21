using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MongoRepository.Core.Extensions;
using MongoRepository.Outbox.Extensions;
using MongoRepository.Sample.Data;
using MongoRepository.Sample.Handlers;
using MongoRepository.Sample.Messages;

var builder = WebApplication.CreateBuilder(args);

// Add MongoDB repository services
builder.Services.AddMongoRepository(builder.Configuration);

// Add outbox pattern services
builder.Services.AddOutboxPattern(builder.Configuration);

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
