using System.Linq.Expressions;
using MongoDB.Driver;
using MongoPatterns.Repository.Models;
using MongoPatterns.Repository.Settings;

namespace MongoPatterns.Repository.Repositories;

/// <summary>
/// MongoDB implementation of the repository pattern
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository works with</typeparam>
public class MongoRepository<TEntity> : IRepository<TEntity> where TEntity : IEntity
{
    protected readonly IMongoCollection<TEntity> Collection;
    protected readonly IClientSessionHandle? Session;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoPatterns{TEntity}"/> class.
    /// </summary>
    /// <param name="settings">MongoDB settings</param>
    /// <param name="session">Optional session for transaction support</param>
    public MongoRepository(MongoDbSettings settings, IClientSessionHandle? session = null)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        Collection = GetCollection(database);
        Session = session;
    }

    protected virtual IMongoCollection<TEntity> GetCollection(IMongoDatabase database)
    {
        return database.GetCollection<TEntity>(typeof(TEntity).Name.ToLower());
    }

    /// <summary>
    /// Gets all entities in the collection
    /// </summary>
    /// <returns>All entities in the collection</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        if (Session != null)
        {
            return await Collection.Find(Session, _ => true).ToListAsync();
        }
        return await Collection.Find(_ => true).ToListAsync();
    }

    /// <summary>
    /// Gets all entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>All entities that match the filter</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAsync(Expression<Func<TEntity, bool>> filter)
    {
        if (Session != null)
        {
            return await Collection.Find(Session, filter).ToListAsync();
        }
        return await Collection.Find(filter).ToListAsync();
    }

    /// <summary>
    /// Gets a single entity by its id
    /// </summary>
    /// <param name="id">The id of the entity to get</param>
    /// <returns>The entity with the specified id, or null if not found</returns>
    public virtual async Task<TEntity> GetByIdAsync(string id)
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);

        if (Session != null)
        {
            return await Collection.Find(Session, filter).FirstOrDefaultAsync();
        }
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets the first entity that matches the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>The first entity that matches the filter, or null if not found</returns>
    public virtual async Task<TEntity> GetFirstAsync(Expression<Func<TEntity, bool>> filter)
    {
        if (Session != null)
        {
            return await Collection.Find(Session, filter).FirstOrDefaultAsync();
        }
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Adds a new entity to the collection
    /// </summary>
    /// <param name="entity">The entity to add</param>
    public virtual async Task AddAsync(TEntity entity)
    {
        if (Session != null)
        {
            await Collection.InsertOneAsync(Session, entity);
        }
        else
        {
            await Collection.InsertOneAsync(entity);
        }
    }

    /// <summary>
    /// Adds multiple entities to the collection
    /// </summary>
    /// <param name="entities">The entities to add</param>
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        if (Session != null)
        {
            await Collection.InsertManyAsync(Session, entities);
        }
        else
        {
            await Collection.InsertManyAsync(entities);
        }
    }

    /// <summary>
    /// Updates an existing entity in the collection
    /// </summary>
    /// <param name="entity">The entity to update</param>
    public virtual async Task UpdateAsync(TEntity entity)
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id);

        if (Session != null)
        {
            await Collection.ReplaceOneAsync(Session, filter, entity);
        }
        else
        {
            await Collection.ReplaceOneAsync(filter, entity);
        }
    }

    /// <summary>
    /// Removes an entity from the collection
    /// </summary>
    /// <param name="id">The id of the entity to remove</param>
    public virtual async Task DeleteAsync(string id)
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);

        if (Session != null)
        {
            await Collection.DeleteOneAsync(Session, filter);
        }
        else
        {
            await Collection.DeleteOneAsync(filter);
        }
    }

    /// <summary>
    /// Removes all entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    public virtual async Task DeleteManyAsync(Expression<Func<TEntity, bool>> filter)
    {
        if (Session != null)
        {
            await Collection.DeleteManyAsync(Session, filter);
        }
        else
        {
            await Collection.DeleteManyAsync(filter);
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

        if (Session != null)
        {
            return await Collection.CountDocumentsAsync(Session, filter);
        }
        return await Collection.CountDocumentsAsync(filter);
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