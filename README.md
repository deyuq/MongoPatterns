# MongoRepository

A generic MongoDB repository pattern implementation for .NET applications with transaction support via UOW pattern.

## Features

- Generic repository pattern implementation
- Strongly typed entities with ID mapping
- Transaction support using Unit of Work pattern
- Advanced querying capabilities
- Microsoft Dependency Injection integration
- CRUD operations for MongoDB
- Pagination support
- Asynchronous operations

## Getting Started

### Installation

#### 1. Clone the repository

```bash
git clone https://github.com/yourusername/MongoRepository.git
cd MongoRepository
```

#### 2. Build the solution

```bash
dotnet build
```

### Configuration

Configure MongoDB connection in `appsettings.json`:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "YourDatabaseName"
  }
}
```

## Docker Support

You can run both MongoDB and the sample application using Docker Compose:

```bash
docker-compose up -d
```

This will start:
- MongoDB container accessible at `mongodb://localhost:27017`
- Sample API application accessible at `http://localhost:5000` and `https://localhost:5001`

To stop the containers:

```bash
docker-compose down
```

To remove the containers and volumes:

```bash
docker-compose down -v
```

### Usage

#### 1. Define your entity models

Create a class that inherits from `Entity` or implements `IEntity`:

```csharp
using MongoRepository.Core.Models;

public class Product : Entity
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}
```

#### 2. Register the repository services

In your `Program.cs` or `Startup.cs`:

```csharp
using MongoRepository.Core.Extensions;

// Using configuration from appsettings.json
builder.Services.AddMongoRepository(builder.Configuration);

// Or using explicit settings
var settings = new MongoDbSettings 
{
    ConnectionString = "mongodb://localhost:27017",
    DatabaseName = "YourDatabaseName"
};
builder.Services.AddMongoRepository(settings);
```

#### 3. Use the repository in your services or controllers

```csharp
public class ProductService
{
    private readonly IRepository<Product> _productRepository;

    public ProductService(IRepository<Product> productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product> GetProductByIdAsync(string id)
    {
        return await _productRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await _productRepository.GetAllAsync();
    }

    public async Task CreateProductAsync(Product product)
    {
        await _productRepository.AddAsync(product);
    }

    public async Task UpdateProductAsync(Product product)
    {
        await _productRepository.UpdateAsync(product);
    }

    public async Task DeleteProductAsync(string id)
    {
        await _productRepository.DeleteAsync(id);
    }
}
```

#### 4. Using transactions with Unit of Work

```csharp
public class OrderService
{
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task CreateOrderWithItemsAsync(Order order, List<OrderItem> orderItems)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var orderRepository = _unitOfWork.GetRepository<Order>();
            var orderItemRepository = _unitOfWork.GetRepository<OrderItem>();

            await orderRepository.AddAsync(order);

            foreach (var item in orderItems)
            {
                item.OrderId = order.Id;
                await orderItemRepository.AddAsync(item);
            }

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

#### 5. Using advanced repository features

```csharp
public class ProductAnalyticsService
{
    private readonly IAdvancedRepository<Product> _productRepository;

    public ProductAnalyticsService(IAdvancedRepository<Product> productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<PagedResult<Product>> GetPaginatedProductsAsync(int page, int pageSize)
    {
        return await _productRepository.GetPagedAsync(
            _ => true,
            p => p.Price,
            ascending: false,
            page: page,
            pageSize: pageSize);
    }

    public async Task<IEnumerable<ProductSummary>> GetProductSummariesAsync()
    {
        return await _productRepository.GetWithProjectionAsync(
            p => p.Category == "Electronics",
            p => new ProductSummary
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price
            });
    }
}

public class ProductSummary
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}
```

#### 6. Working with PagedResult

The `PagedResult<T>` class provides comprehensive pagination metadata for data tables:

```csharp
// Controller or API Endpoint
public async Task<IActionResult> GetPagedProducts(int page = 1, int pageSize = 10)
{
    var pagedResult = await _productRepository.GetPagedAsync(
        _ => true,
        p => p.Name,
        ascending: true,
        page: page,
        pageSize: pageSize);
        
    // PagedResult<T> includes:
    // - Items: The collection for the current page
    // - Page: Current page number
    // - PageSize: Number of items per page
    // - TotalItems: Total count across all pages
    // - TotalPages: Total number of pages
    // - HasPreviousPage: Whether there's a previous page
    // - HasNextPage: Whether there's a next page
    
    return Ok(pagedResult);
}
```

#### 7. Advanced MongoDB-Native Filtering and Projection

When LINQ expressions don't translate well to MongoDB queries, use the native MongoDB definitions:

```csharp
public async Task<IActionResult> GetAdvancedFilteredProducts(int page = 1, int pageSize = 10)
{
    // Use MongoDB's native filter builders for complex queries
    var filterBuilder = Builders<Product>.Filter;
    var filter = filterBuilder.And(
        filterBuilder.Regex(p => p.Name, new BsonRegularExpression("^i", "i")), // Starts with "i", case insensitive
        filterBuilder.Or(
            filterBuilder.Gt(p => p.Price, 100),
            filterBuilder.In(p => p.Category, new[] { "Electronics", "Gadgets" })
        ),
        filterBuilder.Exists(p => p.Tags) // Field must exist
    );
    
    // Use MongoDB's sort builder
    var sort = Builders<Product>.Sort.Descending(p => p.Price);
    
    // Get results with native MongoDB definitions
    var results = await _productRepository.GetPagedWithDefinitionAsync(filter, sort, page, pageSize);
    return Ok(results);
}

public async Task<IActionResult> GetProductsWithProjection()
{
    // Use MongoDB's native projection builder to select specific fields
    var projectionBuilder = Builders<Product>.Projection;
    var projection = projectionBuilder
        .Include(p => p.Id)
        .Include(p => p.Name)
        .Include(p => p.Price)
        .Include("metadata.rating") // Access nested document fields
        .Exclude("_id"); // Exclude MongoDB internal ID
    
    // Create a filter
    var filter = Builders<Product>.Filter.Gt(p => p.Price, 50);
    
    // Get projected results
    var results = await _productRepository.GetWithDefinitionAsync(filter, projection);
    return Ok(results);
}
```

##### Common scenarios where MongoDB-native filters are required:

1. **Text search and regex operations** - For pattern matching, case-insensitive search
2. **Geospatial queries** - For location-based searches
3. **Querying nested arrays** - For array element matching with complex conditions
4. **Field existence checks** - When you need to check if a field exists
5. **Working with JSON directly** - When your C# model doesn't fully match the MongoDB schema
6. **Complex logical combinations** - When you need advanced AND/OR/NOR logic

## Sample Application

A sample minimal API application is included in the `samples/MongoRepository.Sample` directory. It demonstrates how to use the repository with a simple Todo application.

To run the sample:

```bash
cd samples/MongoRepository.Sample
dotnet run
```

Open your browser to `https://localhost:5001/swagger` to see the API documentation and test the endpoints.

## License

This project is licensed under the MIT License - see the LICENSE file for details. 