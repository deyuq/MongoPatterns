using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoRepository.Core.Models;
using MongoRepository.Core.Settings;

namespace MongoRepository.Core.Repositories;

/// <summary>
/// MongoDB implementation of the repository pattern with advanced queries
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository works with</typeparam>
public class MongoAdvancedRepository<TEntity> : MongoRepository<TEntity>, IAdvancedRepository<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MongoAdvancedRepository{TEntity}"/> class.
    /// </summary>
    /// <param name="settings">MongoDB settings</param>
    /// <param name="session">Optional session for transaction support</param>
    public MongoAdvancedRepository(IMongoDbSettings settings, IClientSessionHandle? session = null)
        : base(settings, session)
    {
    }

    /// <summary>
    /// Gets a queryable of entities in the collection
    /// </summary>
    /// <returns>An IQueryable of entities</returns>
    public virtual IQueryable<TEntity> AsQueryable()
    {
        return _collection.AsQueryable();
    }

    /// <summary>
    /// Performs a bulk update of entities matching the filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <param name="update">The update definition</param>
    /// <returns>The result of the update operation</returns>
    public virtual async Task<UpdateResult> BulkUpdateAsync(Expression<Func<TEntity, bool>> filter, UpdateDefinition<TEntity> update)
    {
        if (_session != null)
        {
            return await _collection.UpdateManyAsync(_session, filter, update);
        }
        return await _collection.UpdateManyAsync(filter, update);
    }

    /// <summary>
    /// Gets a filtered and sorted collection of entities with pagination
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <param name="sortField">The field to sort by</param>
    /// <param name="ascending">Whether to sort ascending</param>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The page size</param>
    /// <returns>A paged result containing the entities and pagination metadata</returns>
    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, object>> sortField,
        bool ascending = true,
        int page = 1,
        int pageSize = 10)
    {
        var skip = (page - 1) * pageSize;
        var sort = ascending
            ? Builders<TEntity>.Sort.Ascending(sortField)
            : Builders<TEntity>.Sort.Descending(sortField);

        // Count total items for pagination metadata
        long totalItems;
        if (_session != null)
        {
            totalItems = await _collection.CountDocumentsAsync(_session, filter);
        }
        else
        {
            totalItems = await _collection.CountDocumentsAsync(filter);
        }

        // Get the items for the current page
        IEnumerable<TEntity> items;
        if (_session != null)
        {
            items = await _collection.Find(_session, filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();
        }
        else
        {
            items = await _collection.Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();
        }

        // Create and return the paged result
        return new PagedResult<TEntity>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    /// <summary>
    /// Gets entities with a projection to a different type
    /// </summary>
    /// <typeparam name="TProjection">The type to project to</typeparam>
    /// <param name="filter">The filter to apply</param>
    /// <param name="projection">The projection to apply</param>
    /// <returns>The projected entities</returns>
    public virtual async Task<IEnumerable<TProjection>> GetWithProjectionAsync<TProjection>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TProjection>> projection)
    {
        if (_session != null)
        {
            return await _collection.Find(_session, filter).Project(projection).ToListAsync();
        }
        return await _collection.Find(filter).Project(projection).ToListAsync();
    }

    /// <summary>
    /// Gets the MongoDB collection used by this repository
    /// </summary>
    /// <returns>The MongoDB collection</returns>
    public virtual IMongoCollection<TEntity> GetCollection()
    {
        return _collection;
    }
}