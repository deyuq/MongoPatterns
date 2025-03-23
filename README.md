# MongoPatterns

A comprehensive and flexible MongoDB repository pattern implementation for .NET projects with transaction support, advanced querying capabilities, and outbox pattern for reliable messaging.

## Features

- **Generic Repository Pattern**: Simplifies data access with strongly-typed repositories
- **Advanced Querying**: Support for complex MongoDB filter definitions, projections, and aggregations
- **Transaction Support**: Full MongoDB transaction support through a Unit of Work pattern
- **Outbox Pattern**: Reliable messaging implementation for distributed systems
- **Flexible Configuration**: Easy setup with options for customization
- **Background Service Integration**: Built-in support for processing outbox messages
- **Retry Logic**: Robust error handling with configurable retry mechanisms

## Getting Started

### Installation

```bash
dotnet add package MongoPatterns.Repository
dotnet add package MongoPatterns.Outbox
```

### Basic Usage

#### Configure Services

```csharp
// In Program.cs or Startup.cs
builder.Services.AddMongoRepository(builder.Configuration);
```

With the following in appsettings.json:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "YourDatabaseName"
  }
}
```

#### Define Your Models

```csharp
public class TodoItem : Entity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
```

#### Basic Repository Operations

```csharp
public class TodoService
{
    private readonly IRepository<TodoItem> _repository;
    
    public TodoService(IRepository<TodoItem> repository)
    {
        _repository = repository;
    }
    
    public async Task<IEnumerable<TodoItem>> GetAllTodosAsync()
    {
        return await _repository.GetAllAsync();
    }
    
    public async Task<TodoItem> GetTodoByIdAsync(string id)
    {
        return await _repository.GetByIdAsync(id);
    }
    
    public async Task CreateTodoAsync(TodoItem todo)
    {
        await _repository.AddAsync(todo);
    }
    
    public async Task UpdateTodoAsync(TodoItem todo)
    {
        await _repository.UpdateAsync(todo);
    }
    
    public async Task DeleteTodoAsync(string id)
    {
        await _repository.DeleteAsync(id);
    }
}
```

## Advanced Features

### Using MongoDB Filter Definitions

```csharp
public async Task<IEnumerable<TodoItem>> GetCompletedTodosAsync()
{
    var filter = Builders<TodoItem>.Filter.Eq(t => t.IsCompleted, true);
    return await _repository.GetAsync(filter);
}
```

### Filtering and Simple Queries

```csharp
public async Task<IEnumerable<TodoItem>> GetActiveTodosAsync()
{
    return await _repository.GetAsync(t => !t.IsCompleted);
}
```

### Using the Advanced Repository

```csharp
public class AdvancedTodoService
{
    private readonly IAdvancedRepository<TodoItem> _repository;
    
    public AdvancedTodoService(IAdvancedRepository<TodoItem> repository)
    {
        _repository = repository;
    }
    
    public async Task<IEnumerable<TodoItem>> GetRecentTodosAsync(int page, int pageSize)
    {
        return await _repository.GetPagedAsync(
            t => true,
            t => t.CreatedAt,
            false,
            page,
            pageSize);
    }
    
    public async Task MarkAllAsCompletedAsync()
    {
        var filter = Builders<TodoItem>.Filter.Eq(t => t.IsCompleted, false);
        var update = Builders<TodoItem>.Update
            .Set(t => t.IsCompleted, true)
            .Set(t => t.CompletedAt, DateTime.UtcNow);
            
        await _repository.BulkUpdateAsync(filter, update);
    }
}
```

### Transactions with Unit of Work

```csharp
public class TransactionalTodoService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public TransactionalTodoService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task MoveAllItemsAsync(string sourceListId, string targetListId)
    {
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            var todoRepo = _unitOfWork.GetRepository<TodoItem>();
            
            // Get items from source list
            var items = await todoRepo.GetAsync(t => t.ListId == sourceListId);
            
            // Update each item's list ID
            foreach (var item in items)
            {
                item.ListId = targetListId;
                await todoRepo.UpdateAsync(item);
            }
            
            // Add an audit record
            var auditRepo = _unitOfWork.GetRepository<AuditRecord>();
            await auditRepo.AddAsync(new AuditRecord
            {
                Action = "MoveTodoItems",
                Description = $"Moved items from list {sourceListId} to {targetListId}",
                Timestamp = DateTime.UtcNow
            });
            
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.AbortTransactionAsync();
            throw;
        }
    }
}
```

## Outbox Pattern Integration

The outbox pattern ensures reliable message delivery between services by storing messages in a database before processing them.

### Configure Outbox Services

```csharp
// In Program.cs or Startup.cs
builder.Services.AddOutboxPattern(builder.Configuration);

// Register message handlers
builder.Services.AddOutboxMessageHandler<TodoCreatedHandler, TodoCreatedMessage>();
```

With the following in appsettings.json:

```json
{
  "OutboxSettings": {
    "ProcessingIntervalSeconds": 10,
    "ProcessingDelayMilliseconds": 1000,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 60,
    "BatchSize": 10,
    "AutoStartProcessor": true,
    "ProcessingTtlMinutes": 5,
    "CollectionPrefix": "service1"
  }
}
```

### Define Messages

```csharp
public class TodoCreatedMessage
{
    public string Id { get; set; }
    public string Title { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Implement Message Handlers

```csharp
public class TodoCreatedHandler : IMessageHandler<TodoCreatedMessage>
{
    private readonly ILogger<TodoCreatedHandler> _logger;
    
    public TodoCreatedHandler(ILogger<TodoCreatedHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(TodoCreatedMessage message)
    {
        _logger.LogInformation("Processing new todo item: {Title}", message.Title);
            
        // Process the message (e.g., send email, update analytics, etc.)
        
        return Task.CompletedTask;
    }
}
```

### Using the Outbox in Your Code

```csharp
public class TodoOutboxService
{
    private readonly IRepository<TodoItem> _repository;
    private readonly IOutboxService _outboxService;
    private readonly IUnitOfWork _unitOfWork;
    
    public TodoOutboxService(
        IRepository<TodoItem> repository,
        IOutboxService outboxService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxService = outboxService;
        _unitOfWork = unitOfWork;
    }
    
    public async Task CreateTodoWithNotificationAsync(TodoItem todo)
    {
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            // Add the todo item
            var todoRepo = _unitOfWork.GetRepository<TodoItem>();
            await todoRepo.AddAsync(todo);
            
            // Create and add the message to the outbox within the same transaction
            var message = new TodoCreatedMessage
            {
                Id = todo.Id,
                Title = todo.Title,
                CreatedAt = todo.CreatedAt
            };
            
            await _outboxService.AddMessageToTransactionAsync(message);
            
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.AbortTransactionAsync();
            throw;
        }
    }
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details. 