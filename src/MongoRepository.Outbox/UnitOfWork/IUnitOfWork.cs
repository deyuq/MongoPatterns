using System.Threading.Tasks;
using MongoRepository.Outbox.Models;
using MongoRepository.Outbox.Repositories;

namespace MongoRepository.Outbox.UnitOfWork;

/// <summary>
/// Interface for unit of work pattern with MongoDB transaction support
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Gets a repository for the specified entity type within the current transaction
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A repository for the entity type</returns>
    IRepository<T> GetRepository<T>() where T : Entity;

    /// <summary>
    /// Begins a transaction
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task BeginTransactionAsync();

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CommitTransactionAsync();

    /// <summary>
    /// Aborts the current transaction
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task AbortTransactionAsync();
}