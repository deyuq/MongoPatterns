using Microsoft.AspNetCore.Mvc;
using MongoRepository.Core.UnitOfWork;
using MongoRepository.Outbox;
using MongoRepository.Outbox.Models;
using MongoRepository.Sample.Messages;
using MongoRepository.Sample.Models;
using System.Text.Json;

namespace MongoRepository.Sample.Controllers;

/// <summary>
/// Example of using MongoDB transactions with a replica set
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<TransactionController> _logger;

    /// <summary>
    /// Creates a new instance of the TransactionController
    /// </summary>
    public TransactionController(
        IUnitOfWork unitOfWork,
        IOutboxService outboxService,
        ILogger<TransactionController> logger)
    {
        _unitOfWork = unitOfWork;
        _outboxService = outboxService;
        _logger = logger;
    }

    /// <summary>
    /// Creates multiple todos in a single transaction
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> CreateMultipleTodos([FromBody] List<TodoCreateModel> models)
    {
        try
        {
            // Start a transaction
            await _unitOfWork.BeginTransactionAsync();
            _logger.LogInformation("Transaction started");

            var todoRepository = _unitOfWork.GetRepository<TodoItem>();
            var createdTodos = new List<TodoItem>();

            foreach (var model in models)
            {
                var todo = new TodoItem
                {
                    Title = model.Title,
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await todoRepository.AddAsync(todo);
                createdTodos.Add(todo);

                // Create a message for the outbox
                var message = new TodoCreatedMessage
                {
                    TodoId = todo.Id,
                    Title = todo.Title,
                    CreatedAt = todo.CreatedAt
                };

                // Add message to outbox (will be part of the transaction)
                await _outboxService.AddMessageAsync(
                    typeof(TodoCreatedMessage).FullName ?? "TodoCreatedMessage",
                    JsonSerializer.Serialize(message));

                _logger.LogInformation("Todo and message added: {TodoId}", todo.Id);
            }

            // Commit the transaction - all todos and outbox messages are saved atomically
            await _unitOfWork.CommitTransactionAsync();
            _logger.LogInformation("Transaction committed successfully");

            return Ok(createdTodos);
        }
        catch (Exception ex)
        {
            // If anything fails, abort the transaction
            await _unitOfWork.AbortTransactionAsync();
            _logger.LogError(ex, "Transaction aborted due to error");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Tests transaction rollback with a simulated error
    /// </summary>
    [HttpPost("rollback-test")]
    public async Task<IActionResult> TestRollback([FromBody] List<TodoCreateModel> models)
    {
        try
        {
            // Start a transaction
            await _unitOfWork.BeginTransactionAsync();
            _logger.LogInformation("Transaction started for rollback test");

            var todoRepository = _unitOfWork.GetRepository<TodoItem>();
            var createdTodos = new List<TodoItem>();

            foreach (var model in models)
            {
                var todo = new TodoItem
                {
                    Title = model.Title,
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await todoRepository.AddAsync(todo);
                createdTodos.Add(todo);

                // Create a message for the outbox
                var message = new TodoCreatedMessage
                {
                    TodoId = todo.Id,
                    Title = todo.Title,
                    CreatedAt = todo.CreatedAt
                };

                // Add message to outbox (will be part of the transaction)
                await _outboxService.AddMessageAsync(
                    typeof(TodoCreatedMessage).FullName ?? "TodoCreatedMessage",
                    JsonSerializer.Serialize(message));

                _logger.LogInformation("Todo and message added: {TodoId}", todo.Id);

                // Simulate an error after the first item if there are multiple items
                if (models.Count > 1 && models.IndexOf(model) == 0)
                {
                    throw new Exception("Simulated error to test transaction rollback");
                }
            }

            // This should never be reached in the test case
            await _unitOfWork.CommitTransactionAsync();
            _logger.LogInformation("Transaction committed successfully");

            return Ok(createdTodos);
        }
        catch (Exception ex)
        {
            // Abort the transaction
            await _unitOfWork.AbortTransactionAsync();
            _logger.LogInformation("Transaction rollback test successful: {ErrorMessage}", ex.Message);
            return Ok(new { message = "Transaction rollback test successful", error = ex.Message });
        }
    }
}