# MongoRepository

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
dotnet add package MongoRepository.Core
```

### Basic Usage

#### Configure Services

```csharp
// In Program.cs or Startup.cs
builder.Services.AddMongoRepository(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "YourDatabaseName";
});
```

Or using configuration:

```csharp
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
public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    public string Name { get; set; }
    
    public decimal Price { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
```

#### Basic Repository Operations

```csharp
public class ProductService
{
    private readonly IRepository<Product> _repository;
    
    public ProductService(IRepository<Product> repository)
    {
        _repository = repository;
    }
    
    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await _repository.GetAllAsync();
    }
    
    public async Task<Product> GetProductByIdAsync(string id)
    {
        return await _repository.GetByIdAsync(id);
    }
    
    public async Task CreateProductAsync(Product product)
    {
        await _repository.AddAsync(product);
    }
    
    public async Task UpdateProductAsync(Product product)
    {
        await _repository.UpdateAsync(product);
    }
    
    public async Task DeleteProductAsync(string id)
    {
        await _repository.DeleteAsync(id);
    }
}
```

## Advanced Features

### Using MongoDB Filter Definitions

```csharp
public async Task<IEnumerable<Product>> GetExpensiveProductsAsync(decimal minPrice)
{
    var filter = Builders<Product>.Filter.Gte(p => p.Price, minPrice);
    return await _repository.GetWithDefinitionAsync(filter);
}
```

### Paging and Sorting

```csharp
public async Task<IEnumerable<Product>> GetPagedProductsAsync(int page, int pageSize)
{
    return await _repository.GetPagedAsync(
        p => true,
        p => p.CreatedAt,
        false,
        page,
        pageSize);
}
```

### Using Projections

```csharp
public async Task<IEnumerable<ProductSummary>> GetProductSummariesAsync()
{
    var projection = Builders<Product>.Projection
        .Include(p => p.Id)
        .Include(p => p.Name)
        .Include(p => p.Price);
        
    return await _repository.GetWithProjectionAsync<ProductSummary>(
        Builders<Product>.Filter.Empty,
        projection);
}
```

### Transactions with Unit of Work

```csharp
public async Task TransferFundsAsync(string fromAccountId, string toAccountId, decimal amount)
{
    await _unitOfWork.BeginTransactionAsync();
    
    try
    {
        var accountRepo = _unitOfWork.GetRepository<Account>();
        
        var fromAccount = await accountRepo.GetByIdAsync(fromAccountId);
        var toAccount = await accountRepo.GetByIdAsync(toAccountId);
        
        fromAccount.Balance -= amount;
        toAccount.Balance += amount;
        
        await accountRepo.UpdateAsync(fromAccount);
        await accountRepo.UpdateAsync(toAccount);
        
        // Record the transaction
        var transactionRepo = _unitOfWork.GetRepository<Transaction>();
        await transactionRepo.AddAsync(new Transaction
        {
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
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
```

## Outbox Pattern Integration

The outbox pattern ensures reliable message delivery between services by storing messages in a database before processing them.

### Configure Outbox Services

```csharp
// In Program.cs or Startup.cs
builder.Services.AddOutboxPattern(builder.Configuration);

// Register message handlers
builder.Services.AddOutboxMessageHandler<OrderCreatedHandler, OrderCreatedMessage>();
```

With the following in appsettings.json:

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

### Define Messages

```csharp
public class OrderCreatedMessage
{
    public string OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Implement Message Handlers

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
            
        // Process the message (e.g., send email, update analytics, etc.)
        
        return Task.CompletedTask;
    }
}
```

### Using the Outbox in Your Code

```csharp
public async Task CreateOrderAsync(Order order, List<OrderItem> items)
{
    await _unitOfWork.BeginTransactionAsync();
    
    try
    {
        var orderRepo = _unitOfWork.GetRepository<Order>();
        var itemRepo = _unitOfWork.GetRepository<OrderItem>();
        
        await orderRepo.AddAsync(order);
        
        foreach (var item in items)
        {
            item.OrderId = order.Id;
            await itemRepo.AddAsync(item);
        }
        
        // Add message to outbox as part of the transaction
        var message = new OrderCreatedMessage
        {
            OrderId = order.Id,
            TotalAmount = order.TotalAmount,
            CreatedAt = DateTime.UtcNow
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
```

## Monitoring Outbox Messages

```csharp
public async Task<OutboxStatus> GetOutboxStatusAsync()
{
    var repository = _serviceProvider.GetRequiredService<IRepository<OutboxMessage>>();
    
    var pendingCount = await repository.CountAsync(
        Builders<OutboxMessage>.Filter.Eq(m => m.Status, MessageStatus.Pending));
        
    var processingCount = await repository.CountAsync(
        Builders<OutboxMessage>.Filter.Eq(m => m.Status, MessageStatus.Processing));
        
    var processedCount = await repository.CountAsync(
        Builders<OutboxMessage>.Filter.Eq(m => m.Status, MessageStatus.Processed));
        
    var failedCount = await repository.CountAsync(
        Builders<OutboxMessage>.Filter.Eq(m => m.Status, MessageStatus.Failed));
        
    var abandonedCount = await repository.CountAsync(
        Builders<OutboxMessage>.Filter.Eq(m => m.Status, MessageStatus.Abandoned));
    
    return new OutboxStatus
    {
        Pending = pendingCount,
        Processing = processingCount,
        Processed = processedCount,
        Failed = failedCount,
        Abandoned = abandonedCount,
        Total = pendingCount + processingCount + processedCount + failedCount + abandonedCount
    };
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details. 