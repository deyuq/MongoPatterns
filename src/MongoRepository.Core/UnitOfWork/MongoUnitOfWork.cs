using System.Collections.Concurrent;
using MongoDB.Driver;
using MongoRepository.Core.Models;
using MongoRepository.Core.Repositories;
using MongoRepository.Core.Settings;

namespace MongoRepository.Core.UnitOfWork;

/// <summary>
/// MongoDB implementation of the Unit of Work pattern
/// </summary>
public class MongoUnitOfWork : IUnitOfWork
{
    private readonly IMongoDbSettings _settings;
    private readonly IMongoClient _client;
    private IClientSessionHandle? _session;
    private readonly ConcurrentDictionary<Type, object> _repositories;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoUnitOfWork"/> class.
    /// </summary>
    /// <param name="settings">MongoDB settings</param>
    public MongoUnitOfWork(IMongoDbSettings settings)
    {
        _settings = settings;
        _client = new MongoClient(settings.ConnectionString);
        _repositories = new ConcurrentDictionary<Type, object>();
        _disposed = false;
    }

    /// <summary>
    /// Gets or creates a repository for the specified entity type
    /// </summary>
    /// <typeparam name="TEntity">The type of entity</typeparam>
    /// <returns>A repository for the specified entity type</returns>
    public IRepository<TEntity> GetRepository<TEntity>() where TEntity : IEntity
    {
        return (IRepository<TEntity>)_repositories.GetOrAdd(
            typeof(TEntity),
            entityType => new MongoRepository<TEntity>(_settings, _session));
    }

    /// <summary>
    /// Starts a new transaction
    /// </summary>
    public async Task BeginTransactionAsync()
    {
        if (_session == null)
        {
            _session = await _client.StartSessionAsync();
            _session.StartTransaction();

            // Update existing repositories with the new session
            foreach (var repository in _repositories.Values)
            {
                var type = repository.GetType();
                var sessionField = type.GetField("_session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                sessionField?.SetValue(repository, _session);
            }
        }
    }

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    public async Task CommitTransactionAsync()
    {
        if (_session != null && _session.IsInTransaction)
        {
            await _session.CommitTransactionAsync();
        }
    }

    /// <summary>
    /// Aborts the current transaction
    /// </summary>
    public async Task AbortTransactionAsync()
    {
        if (_session != null && _session.IsInTransaction)
        {
            await _session.AbortTransactionAsync();
        }
    }

    /// <summary>
    /// Disposes the unit of work and its resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the unit of work and its resources
    /// </summary>
    /// <param name="disposing">True if disposing, false if finalizing</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _session?.Dispose();
            }

            _disposed = true;
        }
    }
}