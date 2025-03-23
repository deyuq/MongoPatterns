namespace MongoPatterns.Outbox.StartupTasks;

/// <summary>
///     Interface for tasks that should run at application startup
/// </summary>
public interface IOutboxStartupTask
{
    /// <summary>
    ///     Executes the startup task
    /// </summary>
    void Execute();
}