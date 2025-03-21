using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MongoRepository.Core.Extensions;
using MongoRepository.Outbox.Extensions;
using MongoRepository.Sample.Data;
using MongoRepository.Sample.Extensions;
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

app.Run();
