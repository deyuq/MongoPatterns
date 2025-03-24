# MongoPatterns.Repository

A lightweight, flexible MongoDB repository implementation for .NET applications. This package provides a clean, strongly-typed abstraction over MongoDB operations with support for the repository pattern, unit of work, and CRUD operations.

## Features

- **Repository Pattern**: Clean interface-based repository abstractions
- **Strongly Typed**: Generic repositories for your entity types
- **Unit of Work**: Support for transactions and atomic operations
- **Advanced Queries**: Access to MongoDB-native filter definitions and projections
- **Pagination**: Built-in support for paged results
- **Extensible**: Easily customizable for your specific needs
- **Minimal Dependencies**: Lightweight wrapper around the official MongoDB driver

## Getting Started

### 1. Install the Package

```bash
dotnet add package MongoPatterns.Repository
```

### 2. Configure MongoDB Settings

In your `appsettings.json` file:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "YourDatabase"
  }
}
```

### 3. Register Services

In your `Program.cs` or `Startup.cs`:

```csharp
// Register MongoDB repository services
services.AddMongoRepository(Configuration);
```

### 4. Define Your Entity Classes

Create entity classes that inherit from `Entity` or implement `IEntity`:

```csharp
public class Product : Entity
{
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public bool IsAvailable { get; set; }
}
```

### 5. Use the Repository in Your Code

```csharp
// Basic repository usage
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
    
    public async Task<IEnumerable<Product>> GetAvailableProductsAsync()
    {
        return await _repository.GetAsync(p => p.IsAvailable);
    }
    
    public async Task AddProductAsync(Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
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
    
    public async Task<long> CountProductsAsync()
    {
        return await _repository.CountAsync();
    }
    
    public async Task<bool> ProductExistsAsync(string name)
    {
        return await _repository.ExistsAsync(p => p.Name == name);
    }
}
```

### 6. Using Advanced Repository Features

The `IAdvancedRepository<T>` interface provides access to more advanced MongoDB features:

```csharp
public class ProductAnalyticsService
{
    private readonly IAdvancedRepository<Product> _repository;
    
    public ProductAnalyticsService(IAdvancedRepository<Product> repository)
    {
        _repository = repository;
    }
    
    public async Task<PagedResult<Product>> GetPagedProductsAsync(int page, int pageSize)
    {
        return await _repository.GetPagedAsync(
            filter: p => true,
            sortBy: p => p.CreatedAt,
            sortAscending: false,
            page: page,
            pageSize: pageSize);
    }
    
    public async Task<IEnumerable<ProductSummary>> GetProductSummariesAsync()
    {
        var filter = Builders<Product>.Filter.Gt(p => p.Price, 100);
        var projection = Builders<Product>.Projection.Expression(p => new ProductSummary
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price
        });
        var sort = Builders<Product>.Sort.Descending(p => p.Price);
        
        return await _repository.GetWithDefinitionAsync<ProductSummary>(filter, projection, sort);
    }
    
    public async Task UpdateProductsInPriceRangeAsync(decimal minPrice, decimal maxPrice, decimal percentage)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Gte(p => p.Price, minPrice),
            Builders<Product>.Filter.Lte(p => p.Price, maxPrice)
        );
        
        var update = Builders<Product>.Update.Mul(p => p.Price, 1 + (percentage / 100));
        
        await _repository.BulkUpdateAsync(filter, update);
    }
}
```

### 7. Using the Unit of Work for Transactions

The `IUnitOfWork` interface provides transaction support:

```csharp
public class OrderService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public OrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task CreateOrderWithItemsAsync(Order order, List<OrderItem> items)
    {
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            var orderRepository = _unitOfWork.GetRepository<Order>();
            var itemRepository = _unitOfWork.GetRepository<OrderItem>();
            var inventoryRepository = _unitOfWork.GetRepository<Inventory>();
            
            // Save the order
            await orderRepository.AddAsync(order);
            
            // Save order items
            foreach (var item in items)
            {
                item.OrderId = order.Id;
                await itemRepository.AddAsync(item);
                
                // Update inventory
                var inventory = await inventoryRepository.GetFirstAsync(i => i.ProductId == item.ProductId);
                if (inventory != null)
                {
                    inventory.QuantityOnHand -= item.Quantity;
                    await inventoryRepository.UpdateAsync(inventory);
                }
            }
            
            // Commit the transaction
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

## Advanced Use Cases

### Custom Repository Implementation

You can create custom repositories by inheriting from `MongoRepository<T>`:

```csharp
public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetProductsByCategory(string category);
    Task<decimal> GetAveragePrice();
}

public class ProductRepository : MongoRepository<Product>, IProductRepository
{
    public ProductRepository(MongoDbSettings settings, IClientSessionHandle? session = null)
        : base(settings, session)
    {
    }
    
    public async Task<IEnumerable<Product>> GetProductsByCategory(string category)
    {
        return await GetAsync(p => p.Category == category);
    }
    
    public async Task<decimal> GetAveragePrice()
    {
        var products = await GetAllAsync();
        if (!products.Any())
            return 0;
            
        return products.Average(p => p.Price);
    }
}
```

### Text Search

You can perform MongoDB text search operations:

```csharp
public async Task<IEnumerable<Product>> SearchProductsAsync(string searchText)
{
    var filter = Builders<Product>.Filter.Text(searchText);
    return await _repository.GetWithDefinitionAsync(filter);
}
```

## License

This project is licensed under the MIT License. 