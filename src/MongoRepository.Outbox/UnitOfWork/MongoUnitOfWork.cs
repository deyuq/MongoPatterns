using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoRepository.Outbox.Models;
using MongoRepository.Outbox.Repositories;

namespace MongoRepository.Outbox.UnitOfWork;

/// <summary>
/// MongoDB implementation of the unit of work pattern with transaction support
/// </summary>
public class MongoUnitOfWork : IUnitOfWork, IDisposable
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoUnitOfWork> _logger;
    private IClientSessionHandle? _session;
    private readonly Dictionary<Type, object> _repositories = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoUnitOfWork"/> class.
    /// </summary>
    /// <param name="client">The MongoDB client</param>
    /// <param name="database">The MongoDB database</param>
    /// <param name="logger">The logger</param>
    public MongoUnitOfWork(
        IMongoClient client,
        IMongoDatabase database,
        ILogger<MongoUnitOfWork> logger)
    {
        _client = client;
        _database = database;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IRepository<T> GetRepository<T>() where T : Entity
    {
        var type = typeof(T);

        if (_repositories.ContainsKey(type))
        {
            return (IRepository<T>)_repositories[type];
        }

        var collection = _session != null
            ? _database.GetCollection<T>(GetCollectionName<T>())
            : _database.GetCollection<T>(GetCollectionName<T>());

        var repository = new MongoRepository<T>(collection);
        _repositories.Add(type, repository);

        return repository;
    }

    /// <inheritdoc/>
    public async Task BeginTransactionAsync()
    {
        if (_session != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _session = await _client.StartSessionAsync();
        _session.StartTransaction();

        _logger.LogDebug("Transaction started");
    }

    /// <inheritdoc/>
    public async Task CommitTransactionAsync()
    {
        if (_session == null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        try
        {
            await _session.CommitTransactionAsync();
            _logger.LogDebug("Transaction committed successfully");
        }
        finally
        {
            _session.Dispose();
            _session = null;
        }
    }

    /// <inheritdoc/>
    public async Task AbortTransactionAsync()
    {
        if (_session == null)
        {
            throw new InvalidOperationException("No active transaction to abort.");
        }

        try
        {
            await _session.AbortTransactionAsync();
            _logger.LogDebug("Transaction aborted");
        }
        finally
        {
            _session.Dispose();
            _session = null;
        }
    }

    /// <summary>
    /// Disposes the current session if active
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }

    private string GetCollectionName<T>()
    {
        return typeof(T).Name;
    }
}