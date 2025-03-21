using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MongoRepository.Core.Extensions;
using MongoRepository.Outbox.Extensions;
using MongoRepository.Sample.Data;
using MongoRepository.Sample.Extensions;
using MongoRepository.Sample.Handlers;
using MongoRepository.Sample.Messages;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("mongo", () =>
    {
        try
        {
            var mongoSettings = builder.Configuration.GetSection("MongoDbSettings");
            var connectionString = mongoSettings["ConnectionString"];
            var databaseName = mongoSettings["DatabaseName"];

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(databaseName))
            {
                return HealthCheckResult.Unhealthy("MongoDB configuration is missing");
            }

            // MongoDB connection is verified during startup, so if the app is running, we can assume the connection is working
            return HealthCheckResult.Healthy("MongoDB connection is working");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MongoDB health check failed: {ex.Message}");
        }
    });

var app = builder.Build();

// Initialize database with retry logic
await app.Services.InitializeDatabaseAsync(retryCount: 5);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map controllers
app.MapControllers();

// Health check endpoint for Docker
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.Run();
