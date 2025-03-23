using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoPatterns.Outbox;
using MongoPatterns.Sample.Messages;

namespace MongoPatterns.Sample.Handlers;

/// <summary>
/// Handler for processing TodoCreatedMessage messages from the outbox.
/// </summary>
public class TodoCreatedHandler : IMessageHandler<TodoCreatedMessage>
{
    private readonly ILogger<TodoCreatedHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoCreatedHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger</param>
    public TodoCreatedHandler(ILogger<TodoCreatedHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the message type this handler can process
    /// </summary>
    public string MessageType => typeof(TodoCreatedMessage).FullName ?? nameof(TodoCreatedMessage);

    /// <summary>
    /// Handles a todo created message
    /// </summary>
    /// <param name="message">The message to handle</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task HandleAsync(TodoCreatedMessage message)
    {
        _logger.LogInformation("Processing TodoCreatedMessage: Title={Title}, Id={Id}, CreatedAt={CreatedAt}",
            message.Title, message.TodoId, message.CreatedAt);

        // In a real application, you might want to send an email, publish to an event bus, etc.
        // This is just a demo handler that logs the message
        await Task.Delay(5000);

        _logger.LogInformation("TodoCreatedMessage processed: Title={Title}, Id={Id}, CreatedAt={CreatedAt}",
            message.Title, message.TodoId, message.CreatedAt);
    }
}