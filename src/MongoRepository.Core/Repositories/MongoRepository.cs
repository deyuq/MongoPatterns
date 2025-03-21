using System.Linq.Expressions;
using MongoDB.Driver;
using MongoRepository.Core.Models;
using MongoRepository.Core.Settings;

namespace MongoRepository.Core.Repositories;

/// <summary>
/// MongoDB implementation of the repository pattern
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository works with</typeparam>
public class MongoRepository<TEntity> : IRepository<TEntity> where TEntity : IEntity
{
    protected readonly IMongoCollection<TEntity> _collection;
    protected readonly IClientSessionHandle? _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoRepository{TEntity}"/> class.
    /// </summary>
    /// <param name="settings">MongoDB settings</param>
    /// <param name="session">Optional session for transaction support</param>
    public MongoRepository(MongoDbSettings settings, IClientSessionHandle? session = null)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<TEntity>(typeof(TEntity).Name.ToLower());
        _session = session;
    }

    /// <summary>
    /// Gets all entities in the collection
    /// </summary>
    /// <returns>All entities in the collection</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        if (_session != null)
        {
            return await _collection.Find(_session, _ => true).ToListAsync();
        }
        return await _collection.Find(_ => true).ToListAsync();
    }

    /// <summary>
    /// Gets all entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>All entities that match the filter</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAsync(Expression<Func<TEntity, bool>> filter)
    {
        if (_session != null)
        {
            return await _collection.Find(_session, filter).ToListAsync();
        }
        return await _collection.Find(filter).ToListAsync();
    }

    /// <summary>
    /// Gets a single entity by its id
    /// </summary>
    /// <param name="id">The id of the entity to get</param>
    /// <returns>The entity with the specified id, or null if not found</returns>
    public virtual async Task<TEntity> GetByIdAsync(string id)
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);

        if (_session != null)
        {
            return await _collection.Find(_session, filter).FirstOrDefaultAsync();
        }
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets the first entity that matches the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>The first entity that matches the filter, or null if not found</returns>
    public virtual async Task<TEntity> GetFirstAsync(Expression<Func<TEntity, bool>> filter)
    {
        if (_session != null)
        {
            return await _collection.Find(_session, filter).FirstOrDefaultAsync();
        }
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Adds a new entity to the collection
    /// </summary>
    /// <param name="entity">The entity to add</param>
    public virtual async Task AddAsync(TEntity entity)
    {
        if (_session != null)
        {
            await _collection.InsertOneAsync(_session, entity);
        }
        else
        {
            await _collection.InsertOneAsync(entity);
        }
    }

    /// <summary>
    /// Adds multiple entities to the collection
    /// </summary>
    /// <param name="entities">The entities to add</param>
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        if (_session != null)
        {
            await _collection.InsertManyAsync(_session, entities);
        }
        else
        {
            await _collection.InsertManyAsync(entities);
        }
    }

    /// <summary>
    /// Updates an existing entity in the collection
    /// </summary>
    /// <param name="entity">The entity to update</param>
    public virtual async Task UpdateAsync(TEntity entity)
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id);

        if (_session != null)
        {
            await _collection.ReplaceOneAsync(_session, filter, entity);
        }
        else
        {
            await _collection.ReplaceOneAsync(filter, entity);
        }
    }

    /// <summary>
    /// Removes an entity from the collection
    /// </summary>
    /// <param name="id">The id of the entity to remove</param>
    public virtual async Task DeleteAsync(string id)
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);

        if (_session != null)
        {
            await _collection.DeleteOneAsync(_session, filter);
        }
        else
        {
            await _collection.DeleteOneAsync(filter);
        }
    }

    /// <summary>
    /// Removes all entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    public virtual async Task DeleteManyAsync(Expression<Func<TEntity, bool>> filter)
    {
        if (_session != null)
        {
            await _collection.DeleteManyAsync(_session, filter);
        }
        else
        {
            await _collection.DeleteManyAsync(filter);
        }
    }

    /// <summary>
    /// Gets a count of entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>The count of matching entities</returns>
    public virtual async Task<long> CountAsync(Expression<Func<TEntity, bool>>? filter = null)
    {
        if (filter == null)
        {
            filter = _ => true;
        }

        if (_session != null)
        {
            return await _collection.CountDocumentsAsync(_session, filter);
        }
        return await _collection.CountDocumentsAsync(filter);
    }

    /// <summary>
    /// Checks if any entity matches the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>True if any entity matches the filter, otherwise false</returns>
    public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter)
    {
        return await CountAsync(filter) > 0;
    }
}