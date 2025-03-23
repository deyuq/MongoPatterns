using MongoPatterns.Repository.Models;
using MongoPatterns.Repository.Repositories;

namespace MongoPatterns.Repository.UnitOfWork;

/// <summary>
/// Interface for the Unit of Work pattern implementation
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Gets or creates a repository for the specified entity type
    /// </summary>
    /// <typeparam name="TEntity">The type of entity</typeparam>
    /// <returns>A repository for the specified entity type</returns>
    IRepository<TEntity> GetRepository<TEntity>() where TEntity : IEntity;

    /// <summary>
    /// Starts a new transaction
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Aborts the current transaction
    /// </summary>
    Task AbortTransactionAsync();
}