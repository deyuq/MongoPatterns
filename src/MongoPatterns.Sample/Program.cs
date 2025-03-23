using MongoPatterns.Repository.Extensions;
using MongoPatterns.Outbox.Extensions;
using MongoPatterns.Sample.Data;
using MongoPatterns.Sample.Extensions;
using MongoPatterns.Sample.Handlers;
using MongoPatterns.Sample.Messages;
using MongoDB.Driver;

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

var mongoSettings = builder.Configuration.GetSection("MongoDbSettings");
var connectionString = mongoSettings["ConnectionString"];
builder.Services
   .AddSingleton(sp => new MongoClient(connectionString))
   .AddHealthChecks()
   .AddMongoDb();

var app = builder.Build();

// Initialize database with retry logic
await app.Services.InitializeDatabaseAsync(retryCount: 5);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseOutboxPattern();

app.UseHttpsRedirection();

app.MapHealthChecks("/healthz");

// Map controllers
app.MapControllers();

app.Run();
