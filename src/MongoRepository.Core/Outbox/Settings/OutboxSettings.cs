namespace MongoRepository.Core.Outbox.Settings;

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
}