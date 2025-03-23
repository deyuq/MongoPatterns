using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoPatterns.Repository.Repositories;
using MongoPatterns.Outbox.Models;

namespace MongoPatterns.Sample.Controllers;

[ApiController]
[Route("[controller]")]
public class OutboxController : ControllerBase
{
    private readonly ILogger<OutboxController> _logger;

    public OutboxController(ILogger<OutboxController> logger)
    {
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromServices] IRepository<OutboxMessage> repository)
    {
        var pendingCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Pending);
        var processingCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Processing);
        var processedCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Processed);
        var failedCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Failed);
        var abandonedCount = await repository.CountAsync(m => m.Status == OutboxMessageStatus.Abandoned);

        return Ok(new
        {
            Pending = pendingCount,
            Processing = processingCount,
            Processed = processedCount,
            Failed = failedCount,
            Abandoned = abandonedCount,
            Total = pendingCount + processingCount + processedCount + failedCount + abandonedCount
        });
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] OutboxMessageStatus? status,
        [FromServices] IAdvancedRepository<OutboxMessage> repository)
    {
        if (status.HasValue)
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Status, status.Value);
            var messages = await repository.GetWithDefinitionAsync(filter);
            return Ok(messages);
        }
        else
        {
            var messages = await repository.GetAllAsync();
            return Ok(messages);
        }
    }

    [HttpGet("messages/{id}")]
    public async Task<IActionResult> GetMessage(
        string id,
        [FromServices] IRepository<OutboxMessage> repository)
    {
        var message = await repository.GetByIdAsync(id);
        if (message == null)
        {
            return NotFound();
        }
        return Ok(message);
    }

    [HttpPost("messages/{id}/reprocess")]
    public async Task<IActionResult> ReprocessMessage(
        string id,
        [FromServices] IRepository<OutboxMessage> repository)
    {
        var message = await repository.GetByIdAsync(id);
        if (message == null)
        {
            return NotFound();
        }

        if (message.Status == OutboxMessageStatus.Failed || message.Status == OutboxMessageStatus.Abandoned)
        {
            message.Status = OutboxMessageStatus.Pending;
            message.Error = $"Manually reset to Pending at {DateTime.UtcNow}";
            await repository.UpdateAsync(message);
            return Ok(new { message = "Message has been reset to Pending status for reprocessing" });
        }

        return BadRequest(new { message = $"Cannot reprocess message with status {message.Status}" });
    }
}