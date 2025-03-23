# MongoRepository.Outbox

A robust implementation of the Outbox Pattern for MongoDB in .NET applications. This package provides a reliable way to handle distributed transactions and event-driven architectures when using MongoDB.

## What is the Outbox Pattern?

The Outbox Pattern is a design pattern that solves the problem of atomically updating a database and publishing messages/events in distributed systems. Instead of directly publishing messages to a message broker, the pattern stores them in an "outbox" collection within the same database transaction as the business data. A separate process then reads from this outbox and publishes the messages to their actual destinations.

This approach ensures that:
1. Data updates and message creation are atomic (they succeed or fail together)
2. Messages are guaranteed to be published eventually (even if the system crashes)
3. No message is lost or published more than once (with proper implementation)

## Features

- **Standalone package**: Can be used independently or with MongoRepository.Core
- **Transactional integration**: Works seamlessly with MongoDB transactions
- **Reliable message processing**: Ensures at-least-once delivery with configurable retry mechanisms
- **Claim-based processing**: Supports multiple service instances processing messages concurrently
- **Extensible message handlers**: Easy registration of custom message handlers
- **Background processing**: Automatic background processing of outbox messages
- **Configurable**: Customize processing intervals, batch sizes, and other settings
- **Production-ready**: Includes comprehensive logging, monitoring capabilities, and graceful shutdown

## Getting Started

### 1. Installation

Install the package from NuGet:

```bash
dotnet add package MongoRepository.Outbox
```

### 2. Configuration

Add outbox settings to your `appsettings.json`:

```json
{
  "OutboxSettings": {
    "ProcessingIntervalSeconds": 10,
    "ProcessingDelayMilliseconds": 1000,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 60,
    "BatchSize": 10,
    "AutoStartProcessor": true,
    "ProcessingTtlMinutes": 15,
    "CollectionPrefix": "yourservice",
    "ClaimTimeoutMinutes": 2
  }
}
```

### 3. Register Services

In your `Program.cs` or `Startup.cs`:

```csharp
// Register outbox pattern services
builder.Services.AddOutboxPattern(builder.Configuration);

// Register your message handlers
builder.Services.AddOutboxMessageHandler<OrderCreatedHandler, OrderCreatedMessage>();
builder.Services.AddOutboxMessageHandler<PaymentProcessedHandler, PaymentProcessedMessage>();
```

### 4. Define Messages

Create message classes for your events:

```csharp
public class OrderCreatedMessage
{
    public string OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public string CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 5. Create Message Handlers

Implement handlers for your messages:

```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreatedMessage>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    
    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }
    
    public string MessageType => typeof(OrderCreatedMessage).FullName ?? nameof(OrderCreatedMessage);
    
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing order created event for OrderId: {OrderId}, Amount: {Amount}", 
            message.OrderId, message.TotalAmount);
            
        // Implement your event handling logic here
        // For example: send an email, notify other systems, etc.
        
        return Task.CompletedTask;
    }
}
```

### 6. Usage Examples

#### Basic Usage

```csharp
public class OrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IOutboxService _outboxService;
    
    public OrderService(IRepository<Order> orderRepository, IOutboxService outboxService)
    {
        _orderRepository = orderRepository;
        _outboxService = outboxService;
    }
    
    public async Task CreateOrderAsync(Order order)
    {
        // Save the order
        await _orderRepository.AddAsync(order);
        
        // Add message to outbox
        var message = new OrderCreatedMessage
        {
            OrderId = order.Id,
            TotalAmount = order.TotalAmount,
            CustomerId = order.CustomerId,
            CreatedAt = DateTime.UtcNow
        };
        
        await _outboxService.AddMessageAsync(message);
    }
}
```

#### Transactional Usage

```csharp
public class OrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxService _outboxService;
    
    public OrderService(IUnitOfWork unitOfWork, IOutboxService outboxService)
    {
        _unitOfWork = unitOfWork;
        _outboxService = outboxService;
    }
    
    public async Task CreateOrderWithItemsAsync(Order order, List<OrderItem> items)
    {
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            var orderRepository = _unitOfWork.GetRepository<Order>();
            var itemRepository = _unitOfWork.GetRepository<OrderItem>();
            
            // Save the order
            await orderRepository.AddAsync(order);
            
            // Save order items
            foreach (var item in items)
            {
                item.OrderId = order.Id;
                await itemRepository.AddAsync(item);
            }
            
            // Add message to outbox (will use the same transaction)
            var message = new OrderCreatedMessage
            {
                OrderId = order.Id,
                TotalAmount = order.TotalAmount,
                CustomerId = order.CustomerId,
                CreatedAt = DateTime.UtcNow
            };
            
            await _outboxService.AddMessageAsync(message, _unitOfWork.Session);
            
            // Commit the transaction - everything succeeds or fails together
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            // Rollback on error
            await _unitOfWork.AbortTransactionAsync();
            throw;
        }
    }
}
```

### 7. Monitoring Outbox Messages

You can add endpoints in your API to check the status of outbox messages:

```csharp
app.MapGet("/outbox/messages", async (IOutboxRepository outboxRepository) =>
{
    var pendingMessages = await outboxRepository.GetMessagesAsync(MessageStatus.Pending, 100);
    var processingMessages = await outboxRepository.GetMessagesAsync(MessageStatus.Processing, 100);
    var processedMessages = await outboxRepository.GetMessagesAsync(MessageStatus.Processed, 100);
    var failedMessages = await outboxRepository.GetMessagesAsync(MessageStatus.Failed, 100);
    var abandonedMessages = await outboxRepository.GetMessagesAsync(MessageStatus.Abandoned, 100);
    
    return Results.Ok(new
    {
        Pending = pendingMessages.Count(),
        Processing = processingMessages.Count(),
        Processed = processedMessages.Count(),
        Failed = failedMessages.Count(),
        Abandoned = abandonedMessages.Count(),
        
        RecentPendingMessages = pendingMessages.Take(10),
        RecentFailedMessages = failedMessages.Take(10),
        RecentAbandonedMessages = abandonedMessages.Take(10)
    });
});
```

## Configuration Options

The outbox pattern can be configured using the following settings:

| Setting                       | Description                                                                                     | Default |
| ----------------------------- | ----------------------------------------------------------------------------------------------- | ------- |
| `ProcessingIntervalSeconds`   | How often the processor checks for new messages to process                                      | 10      |
| `ProcessingDelayMilliseconds` | Delay in milliseconds between MongoDB operations to reduce database load                        | 1000    |
| `MaxRetryAttempts`            | Maximum number of retries for failed messages                                                   | 3       |
| `RetryDelaySeconds`           | Delay between retry attempts in seconds                                                         | 60      |
| `BatchSize`                   | Number of messages to process in each batch                                                     | 10      |
| `AutoStartProcessor`          | Whether to automatically start the background processor                                         | true    |
| `ProcessingTtlMinutes`        | Time (in minutes) after which a message stuck in "Processing" status will be reset to "Pending" | 15      |
| `CollectionPrefix`            | Prefix for outbox collection name to support microservice-specific outbox collections           | null    |
| `ClaimTimeoutMinutes`         | Time (in minutes) after which a message claim expires                                           | 2       |

## Processing TTL Feature

The `ProcessingTtlMinutes` setting provides an automatic recovery mechanism for messages that get stuck in the "Processing" status. This can happen if:

- The application crashes during message processing
- A processor instance gets terminated unexpectedly
- A message handler encounters an unhandled exception

By setting `ProcessingTtlMinutes`, any message that has been in the "Processing" state for longer than the specified time will automatically be reset to "Pending" status when the processor runs. This ensures that no message gets stuck in processing indefinitely, improving the reliability of your system.

For example, if `ProcessingTtlMinutes` is set to 15, any message that has been "Processing" for more than 15 minutes will be reset and reprocessed.

## Microservice-Specific Collections

The `CollectionPrefix` setting allows you to configure a unique prefix for outbox collections when using the library across multiple microservices. This ensures each service uses its own isolated outbox collection while maintaining the same implementation pattern.

By setting a unique `CollectionPrefix` for each service, you can:

- Keep outbox messages from different services isolated
- Deploy multiple services accessing the same MongoDB instance
- Avoid conflicts between different service's messages
- Simplify monitoring and debugging by service

For example, if your CollectionPrefix is set to "orderservice", the outbox collection will be named "orderservice_outboxmessage" instead of just "outboxmessage".

## Concurrent Message Processing with Claims

The outbox processor implements a claim-based approach to enable multiple service instances to process messages concurrently without race conditions:

### How It Works

1. **Instance Identification**: Each processor instance generates a unique ID at startup
2. **Atomic Message Claiming**: Messages are claimed atomically using MongoDB's `FindOneAndUpdate` operation
3. **Claim Expiration**: Claims have a configurable timeout (`ClaimTimeoutMinutes`) to handle instance failures
4. **Claim Release**: Claims are automatically released when a message is processed or the service shuts down

### Configuration

Configure the claim timeout in your `appsettings.json`:

```json
{
  "OutboxSettings": {
    "ClaimTimeoutMinutes": 2
  }
}
```

### Benefits

- **No race conditions**: Multiple instances can process messages from the same collection without conflicts
- **Automatic failover**: If an instance crashes, its claimed messages will be reprocessed after the claim timeout
- **Load distribution**: Processing is naturally distributed across all available instances
- **Resilient processing**: Handles instance restarts, scaling events, and network issues gracefully

### Implementation Details

The claim-based processing uses two additional fields in the outbox message:

- `ClaimedBy`: The ID of the service instance that has claimed the message
- `ClaimExpiresAt`: When the claim expires and can be taken by another processor

This mechanism handles common failure scenarios:

- **Instance crash**: Expired claims are automatically released
- **Process handling failure**: Claims are released and the message status is updated appropriately
- **Long-running processes**: Claims can be renewed during processing
- **Stalled instances**: The `ProcessingTtlMinutes` setting handles stuck messages

This approach combines the benefits of the outbox pattern's reliability with the scalability of distributed processing, making it suitable for high-throughput, mission-critical applications.

## Graceful Shutdown

The outbox processor supports graceful shutdown to ensure:

1. Currently processing messages are completed
2. No new messages are picked up
3. Resources are properly released

The processor registers with the .NET host's `IHostApplicationLifetime` to respond to application shutdown signals, ensuring clean termination.

## Monitoring and Troubleshooting

- **Logging**: The outbox processor logs detailed information about its operations, including message processing attempts, successes, and failures.
- **Message Status**: The `MessageStatus` enum provides clear visibility into the state of each message.
- **Error Details**: Failed messages include detailed error information to help with troubleshooting.

## Best Practices

1. **Idempotent Handlers**: Design message handlers to be idempotent, as messages may be processed more than once.
2. **Appropriate TTL Settings**: Configure `ProcessingTtlMinutes` based on your expected message processing time.
3. **Regular Monitoring**: Set up monitoring for abandoned messages and failed messages that have reached their retry limit.
4. **Transaction Usage**: Use transactions when updating multiple collections to ensure consistency.

## Advanced Usage

### Custom Message Serialization

You can customize how messages are serialized by implementing the `IMessageSerializer` interface:

```csharp
public class CustomMessageSerializer : IMessageSerializer
{
    public string Serialize(object message)
    {
        // Custom serialization logic
        return JsonConvert.SerializeObject(message, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            Formatting = Formatting.Indented
        });
    }

    public object Deserialize(string serializedMessage, Type messageType)
    {
        // Custom deserialization logic
        return JsonConvert.DeserializeObject(serializedMessage, messageType, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
    }
}

// Register your custom serializer
services.AddSingleton<IMessageSerializer, CustomMessageSerializer>();
```

### Manual Outbox Processing

If you need more control over the processing, you can disable automatic processing and handle it manually:

```json
{
  "OutboxSettings": {
    "AutoStartProcessor": false
  }
}
```

Then manually process messages when needed:

```csharp
public class ManualProcessingService
{
    private readonly IOutboxProcessor _outboxProcessor;
    
    public ManualProcessingService(IOutboxProcessor outboxProcessor)
    {
        _outboxProcessor = outboxProcessor;
    }
    
    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        await _outboxProcessor.ProcessMessagesAsync(cancellationToken);
    }
}
```

## License

This project is licensed under the MIT License. 