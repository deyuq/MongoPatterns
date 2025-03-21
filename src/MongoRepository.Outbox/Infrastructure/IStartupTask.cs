namespace MongoRepository.Outbox.Infrastructure;

/// <summary>
/// Interface for tasks that should run at application startup
/// </summary>
public interface IStartupTask
{
    /// <summary>
    /// Executes the startup task
    /// </summary>
    void Execute();
}