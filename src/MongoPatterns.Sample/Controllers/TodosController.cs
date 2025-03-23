using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoPatterns.Repository.Repositories;
using MongoPatterns.Outbox;
using MongoPatterns.Sample.Messages;
using MongoPatterns.Sample.Models;

namespace MongoPatterns.Sample.Controllers;

[ApiController]
[Route("[controller]")]
public class TodosController : ControllerBase
{
    private readonly ILogger<TodosController> _logger;

    public TodosController(ILogger<TodosController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromServices] IAdvancedRepository<TodoItem> repository)
    {
        var todos = await repository.GetAllAsync();
        return Ok(todos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, [FromServices] IRepository<TodoItem> repository)
    {
        var todo = await repository.GetByIdAsync(id);
        if (todo == null) return NotFound();
        return Ok(todo);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TodoItem todo,
        [FromServices] IRepository<TodoItem> repository,
        [FromServices] IOutboxService outboxService)
    {
        await repository.AddAsync(todo);

        // Publish a message to the outbox
        var message = new TodoCreatedMessage
        {
            TodoId = todo.Id,
            Title = todo.Title,
            CreatedAt = todo.CreatedAt
        };

        await outboxService.AddMessageAsync(message);

        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] TodoItem updatedTodoItem,
        [FromServices] IRepository<TodoItem> repository)
    {
        var todo = await repository.GetByIdAsync(id);
        if (todo == null) return NotFound();

        updatedTodoItem.Id = id;
        await repository.UpdateAsync(updatedTodoItem);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        [FromServices] IRepository<TodoItem> repository)
    {
        var todo = await repository.GetByIdAsync(id);
        if (todo == null) return NotFound();

        await repository.DeleteAsync(id);

        return NoContent();
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IAdvancedRepository<TodoItem> repository)
    {
        var pagedResult = await repository.GetPagedAsync(
            _ => true,
            t => t.CreatedAt,
            false,
            page,
            pageSize);

        return Ok(pagedResult);
    }

    [HttpGet("advanced")]
    public async Task<IActionResult> GetAdvanced(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IAdvancedRepository<TodoItem> repository)
    {
        // Example of using MongoDB filter builder
        var filterBuilder = Builders<TodoItem>.Filter;

        // Creating complex filters that may not translate well with LINQ expressions
        var filter = filterBuilder.And(
            filterBuilder.Regex(t => t.Title,
                new BsonRegularExpression("^T", "i")), // Starts with "T", case insensitive
            filterBuilder.Or(
                filterBuilder.Eq(t => t.IsCompleted, true),
                filterBuilder.Gt(t => t.CreatedAt, DateTime.UtcNow.AddDays(-7)) // Created in the last week
            )
        );

        // Using MongoDB sort builder
        var sort = Builders<TodoItem>.Sort.Descending(t => t.CreatedAt);

        // Get paged results with native MongoDB definitions
        var pagedResult = await repository.GetPagedWithDefinitionAsync(
            filter,
            sort,
            page,
            pageSize);

        return Ok(pagedResult);
    }

    [HttpGet("projected")]
    public async Task<IActionResult> GetProjected([FromServices] IAdvancedRepository<TodoItem> repository)
    {
        // Example of using MongoDB filter and projection builders
        var filterBuilder = Builders<TodoItem>.Filter;
        var projectionBuilder = Builders<TodoItem>.Projection;

        // Complex filter
        var filter = filterBuilder.And(
            filterBuilder.Exists(t => t.CompletedAt),
            filterBuilder.Ne(t => t.CompletedAt, null)
        );

        // Define projection to TodoItemSummary
        var projection = projectionBuilder.Expression(t => new TodoItemSummary
        {
            Id = t.Id,
            Title = t.Title,
            IsCompleted = t.IsCompleted,
            CompletedAt = t.CompletedAt
        });

        // Sort by completion date
        var sort = Builders<TodoItem>.Sort.Descending(t => t.CompletedAt!);

        // Get projected results with limit
        var results = await repository.GetWithDefinitionAsync(
            filter,
            projection,
            sort,
            10);

        return Ok(results);
    }

    [HttpGet("filter")]
    public async Task<IActionResult> GetFiltered(
        [FromQuery] string? titleContains,
        [FromQuery] bool? isCompleted,
        [FromServices] IAdvancedRepository<TodoItem> repository)
    {
        var filterBuilder = Builders<TodoItem>.Filter.Empty;

        if (!string.IsNullOrEmpty(titleContains))
            filterBuilder &=
                Builders<TodoItem>.Filter.Regex(t => t.Title, new BsonRegularExpression(titleContains, "i"));

        if (isCompleted.HasValue) filterBuilder &= Builders<TodoItem>.Filter.Eq(t => t.IsCompleted, isCompleted.Value);

        var todos = await repository.GetWithDefinitionAsync(filterBuilder);
        return Ok(todos);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromServices] IAdvancedRepository<TodoItem> repository)
    {
        // Simplified to use expressions instead of projection definitions
        var todos = await repository.GetAllAsync();
        var summaries = todos.Select(t => new TodoItemSummary
        {
            Id = t.Id,
            Title = t.Title,
            IsCompleted = t.IsCompleted,
            CompletedAt = t.CompletedAt
        }).ToList();

        return Ok(summaries);
    }

    [HttpGet("advanced-query")]
    public async Task<IActionResult> GetAdvancedQuery(
        [FromQuery] string? titleContains,
        [FromQuery] bool? isCompleted,
        [FromServices] IAdvancedRepository<TodoItem> repository,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] bool sortAscending = false)
    {
        var filterBuilder = Builders<TodoItem>.Filter.Empty;

        if (!string.IsNullOrEmpty(titleContains))
            filterBuilder &=
                Builders<TodoItem>.Filter.Regex(t => t.Title, new BsonRegularExpression(titleContains, "i"));

        if (isCompleted.HasValue) filterBuilder &= Builders<TodoItem>.Filter.Eq(t => t.IsCompleted, isCompleted.Value);

        var sortDefinition = sortAscending
            ? Builders<TodoItem>.Sort.Ascending(sortBy)
            : Builders<TodoItem>.Sort.Descending(sortBy);

        var todos = await repository.GetPagedWithDefinitionAsync(
            filterBuilder,
            sortDefinition,
            page,
            pageSize);

        return Ok(new
        {
            Page = page,
            PageSize = pageSize,
            Total = await repository.CountAsync(expr => true),
            Items = todos
        });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string searchText,
        [FromServices] IAdvancedRepository<TodoItem> repository)
    {
        var textSearchFilter = Builders<TodoItem>.Filter.Text(searchText);
        var todos = await repository.GetWithDefinitionAsync(textSearchFilter);
        return Ok(todos);
    }
}