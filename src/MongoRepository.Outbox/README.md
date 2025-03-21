# MongoRepository.Outbox

A standalone implementation of the outbox pattern for MongoDB. This package provides a reliable way to implement event-driven systems with MongoDB by using the outbox pattern to ensure messages are delivered reliably, even in the face of failures.

## Features

- **Standalone Package**: Can be used independently of other MongoDB repositories
- **Transactional Integration**: Works with MongoDB transactions for atomic operations
- **Reliable Message Processing**: Includes retry mechanisms with exponential backoff
- **Message Handlers**: Extensible message handler system for processing different message types
- **Background Processing**: Automatic processing of outbox messages using a BackgroundService
- **Configurable**: Customizable retry attempts, processing intervals, and batch sizes
- **Production-Ready**: Includes comprehensive logging, error handling, and monitoring

## Getting Started

### 1. Install the Package

```bash
dotnet add package MongoRepository.Outbox
```

### 2. Configure Outbox Settings

In your `appsettings.json` file:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "YourDatabase"
  },
  "OutboxSettings": {
    "ProcessingIntervalSeconds": 10,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 60,
    "BatchSize": 10,
    "AutoStartProcessor": true
  }
}
```

### 3. Register Services

In your `Program.cs` or `Startup.cs`:

```csharp
// Add outbox pattern services with MongoDB configuration
services.AddOutboxPattern(Configuration);

// Register your message handlers
services.AddOutboxMessageHandler<YourMessageHandler, YourMessage>();
```

### 4. Initialize the Outbox Collections

In your `Program.cs` or `Startup.cs`:

```csharp
app.UseOutboxPattern();
```

### 5. Define Messages

Create message classes to represent events in your system:

```csharp
public class OrderCreatedMessage
{
    public string OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 6. Implement Message Handlers

Create handler classes that process your messages:

```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreatedMessage>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    
    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }
    
    public string MessageType => typeof(OrderCreatedMessage).FullName;
    
    public Task HandleAsync(OrderCreatedMessage message)
    {
        _logger.LogInformation("Processing order {OrderId} for {TotalAmount}", 
            message.OrderId, message.TotalAmount);
            
        // Perform your message handling logic here
        // e.g., call an external API, update a read model, etc.
        
        return Task.CompletedTask;
    }
}
```

### 7. Use the Outbox Pattern in Your Code

```csharp
// Basic usage without transactions
public async Task CreateOrder(Order order, IOutboxService outboxService)
{
    // Save the order to your database using your preferred method
    await _orderRepository.SaveAsync(order);
    
    var message = new OrderCreatedMessage
    {
        OrderId = order.Id,
        TotalAmount = order.TotalAmount,
        CreatedAt = DateTime.UtcNow
    };
    
    await outboxService.AddMessageAsync(message);
}

// Usage with transactions
public async Task CreateOrderWithItems(
    Order order, 
    List<OrderItem> items, 
    IUnitOfWork unitOfWork,
    IOutboxService outboxService)
{
    await unitOfWork.BeginTransactionAsync();
    
    try
    {
        // Use the unit of work to get repositories and save entities
        var orderRepository = unitOfWork.GetRepository<Order>();
        var itemRepository = unitOfWork.GetRepository<OrderItem>();
        
        await orderRepository.AddAsync(order);
        
        foreach (var item in items)
        {
            item.OrderId = order.Id;
            await itemRepository.AddAsync(item);
        }
        
        var message = new OrderCreatedMessage
        {
            OrderId = order.Id,
            TotalAmount = order.TotalAmount,
            CreatedAt = DateTime.UtcNow
        };
        
        await outboxService.AddMessageToTransactionAsync(message);
        
        await unitOfWork.CommitTransactionAsync();
    }
    catch
    {
        await unitOfWork.AbortTransactionAsync();
        throw;
    }
}
```

## Monitoring Outbox Messages

You can implement an endpoint to monitor the status of outbox messages:

```csharp
app.MapGet("/outbox/status", async (IRepository<OutboxMessage> repository) =>
{
    var pendingFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Pending);
    var processingFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Processing);
    var processedFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Processed);
    var failedFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Failed);
    var abandonedFilter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Abandoned);

    var pendingCount = await repository.CountAsync(pendingFilter);
    var processingCount = await repository.CountAsync(processingFilter);
    var processedCount = await repository.CountAsync(processedFilter);
    var failedCount = await repository.CountAsync(failedFilter);
    var abandonedCount = await repository.CountAsync(abandonedFilter);

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
```

## How It Works

1. When you call `AddMessageAsync` or `AddMessageToTransactionAsync`, the message is stored in the outbox collection in MongoDB
2. The outbox processor runs in the background, checking periodically for new messages
3. When it finds messages with status "Pending", it tries to process them by:
   - Looking up a registered handler for the message type
   - Deserializing the message content
   - Calling the handler's `HandleAsync` method
4. If processing succeeds, the message is marked as "Processed"
5. If processing fails, it attempts retries with exponential backoff
6. After exceeding max retry attempts, the message is marked as "Abandoned"

## Configuration Options

| Setting                   | Description                                            | Default |
| ------------------------- | ------------------------------------------------------ | ------- |
| ProcessingIntervalSeconds | How often the processor checks for new messages        | 10      |
| MaxRetryAttempts          | Maximum number of processing attempts                  | 3       |
| RetryDelaySeconds         | Delay between retry attempts (multiplied by 2^attempt) | 60      |
| BatchSize                 | Number of messages to process in each batch            | 10      |
| AutoStartProcessor        | Whether to automatically start the processor           | true    |

## License

This project is licensed under the MIT License. 