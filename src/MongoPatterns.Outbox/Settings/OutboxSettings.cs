namespace MongoPatterns.Outbox.Settings;

/// <summary>
/// Settings for the outbox pattern implementation
/// </summary>
public class OutboxSettings
{
    /// <summary>
    /// Gets or sets the interval in seconds at which the outbox processor checks for new messages.
    /// Default is 10 seconds.
    /// </summary>
    public int ProcessingIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the delay in milliseconds between each MongoDB request during processing.
    /// This helps reduce the load on MongoDB by adding a small pause between operations.
    /// Default is 1000 milliseconds (1 second).
    /// </summary>
    public int ProcessingDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed messages.
    /// Default is 3 attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay in seconds between retry attempts (exponential backoff will be applied).
    /// Default is 60 seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the batch size for processing messages.
    /// Default is 10 messages per batch.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether the processor should automatically start on application startup.
    /// Default is true.
    /// </summary>
    public bool AutoStartProcessor { get; set; } = true;

    /// <summary>
    /// Gets or sets the time in minutes after which a message in 'Processing' state 
    /// will be considered stuck and reset to 'Pending'.
    /// Default is 5 minutes.
    /// </summary>
    public int ProcessingTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the prefix for the outbox message collection name.
    /// This allows different microservices to use their own outbox collections.
    /// </summary>
    public string? CollectionPrefix { get; set; }
}