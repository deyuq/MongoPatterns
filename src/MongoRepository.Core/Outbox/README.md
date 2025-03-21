# MongoRepository Outbox Pattern

The outbox pattern is a reliable messaging pattern used to ensure message delivery in distributed systems. It solves the problem of atomically updating a database and publishing a message to a message broker by:

1. Storing the message in a database table (the "outbox") as part of the same transaction as other application data changes
2. Having a separate process read from the outbox table and publish the messages

This implementation provides a complete outbox pattern solution for .NET applications using MongoDB.

## Features

- **Transactional Integration**: Works with MongoDB transactions through the Unit of Work pattern
- **Reliable Message Processing**: Includes retry mechanisms with exponential backoff
- **Message Handlers**: Extensible message handler system for processing different message types
- **Background Processing**: Automatic processing of outbox messages using a background service
- **Configurable**: Customizable retry attempts, processing intervals, and batch sizes
- **Production-Ready**: Includes comprehensive logging, error handling, and monitoring

## Getting Started

### 1. Install Required Dependencies

The outbox pattern integration is included in the MongoRepository.Core package.

### 2. Configure Outbox Settings

In your `appsettings.json` file:

```json
{
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
// Add outbox pattern services
services.AddOutboxPattern(Configuration);

// Register your message handlers
services.AddOutboxMessageHandler<YourMessageHandler, YourMessage>();
```

### 4. Define Messages

Create message classes to represent events in your system:

```csharp
public class OrderCreatedMessage
{
    public string OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 5. Implement Message Handlers

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

### 6. Use the Outbox Pattern in Your Code

```csharp
// Basic usage without transactions
public async Task CreateOrder(Order order, IRepository<Order> repository, IOutboxService outboxService)
{
    await repository.AddAsync(order);
    
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

## Monitoring and Troubleshooting

All outbox operations are logged with appropriate log levels:
- Information: Regular processing, message registrations
- Debug: Detailed message processing information
- Warning: Temporary processing failures, retries
- Error: Processing failures, abandoned messages

You can monitor the health of the outbox by checking:
1. The count of pending messages
2. The count of abandoned messages
3. The processing time for messages

## Best Practices

1. Keep message handlers lightweight and focused on a single responsibility
2. Avoid long-running operations in message handlers
3. Use message handlers to propagate events, not to perform critical business logic
4. Monitor the outbox collection to detect processing issues early
5. Consider implementing a dead-letter mechanism for abandoned messages

## Advanced Usage

### Custom Message Serialization

The outbox service uses System.Text.Json by default, but you can extend the implementation to use custom serialization if needed.

### Manual Outbox Processing

If you need more control over when outbox messages are processed, you can set `AutoStartProcessor` to false and inject `IMongoIndexManager` to manually trigger processing.

### Custom Message Retry Logic

The retry logic can be customized by extending the `OutboxProcessor` class and providing your own implementation. 