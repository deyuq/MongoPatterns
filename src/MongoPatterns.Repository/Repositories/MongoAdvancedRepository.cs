using System.Linq.Expressions;
using MongoDB.Driver;
using MongoPatterns.Repository.Models;
using MongoPatterns.Repository.Settings;

namespace MongoPatterns.Repository.Repositories;

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
    public MongoAdvancedRepository(MongoDbSettings settings, IClientSessionHandle? session = null)
        : base(settings, session)
    {
    }

    /// <summary>
    /// Gets a queryable of entities in the collection
    /// </summary>
    /// <returns>An IQueryable of entities</returns>
    public virtual IQueryable<TEntity> AsQueryable()
    {
        return Collection.AsQueryable();
    }

    /// <summary>
    /// Performs a bulk update of entities matching the filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <param name="update">The update definition</param>
    /// <returns>The result of the update operation</returns>
    public virtual async Task<UpdateResult> BulkUpdateAsync(Expression<Func<TEntity, bool>> filter, UpdateDefinition<TEntity> update)
    {
        if (Session != null)
        {
            return await Collection.UpdateManyAsync(Session, filter, update);
        }
        return await Collection.UpdateManyAsync(filter, update);
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
        if (Session != null)
        {
            totalItems = await Collection.CountDocumentsAsync(Session, filter);
        }
        else
        {
            totalItems = await Collection.CountDocumentsAsync(filter);
        }

        // Get the items for the current page
        IEnumerable<TEntity> items;
        if (Session != null)
        {
            items = await Collection.Find(Session, filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();
        }
        else
        {
            items = await Collection.Find(filter)
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
    /// Gets a filtered and sorted collection of entities with pagination using MongoDB-native filter and sort definitions
    /// </summary>
    /// <param name="filter">The MongoDB filter definition to apply</param>
    /// <param name="sort">The MongoDB sort definition to apply</param>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The page size</param>
    /// <returns>A paged result containing the entities and pagination metadata</returns>
    public virtual async Task<PagedResult<TEntity>> GetPagedWithDefinitionAsync(
        FilterDefinition<TEntity> filter,
        SortDefinition<TEntity> sort,
        int page = 1,
        int pageSize = 10)
    {
        var skip = (page - 1) * pageSize;

        // Count total items for pagination metadata
        long totalItems;
        if (Session != null)
        {
            totalItems = await Collection.CountDocumentsAsync(Session, filter);
        }
        else
        {
            totalItems = await Collection.CountDocumentsAsync(filter);
        }

        // Get the items for the current page
        IEnumerable<TEntity> items;
        if (Session != null)
        {
            items = await Collection.Find(Session, filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();
        }
        else
        {
            items = await Collection.Find(filter)
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
    /// Gets a filtered and sorted collection of entities with pagination and projection using MongoDB-native definitions
    /// </summary>
    /// <typeparam name="TProjection">The type to project to</typeparam>
    /// <param name="filter">The MongoDB filter definition to apply</param>
    /// <param name="projection">The MongoDB projection definition to apply</param>
    /// <param name="sort">The MongoDB sort definition to apply</param>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The page size</param>
    /// <returns>A paged result containing the projected entities and pagination metadata</returns>
    public virtual async Task<PagedResult<TProjection>> GetPagedWithDefinitionAsync<TProjection>(
        FilterDefinition<TEntity> filter,
        ProjectionDefinition<TEntity, TProjection> projection,
        SortDefinition<TEntity> sort,
        int page = 1,
        int pageSize = 10)
    {
        var skip = (page - 1) * pageSize;

        // Count total items for pagination metadata
        long totalItems;
        if (Session != null)
        {
            totalItems = await Collection.CountDocumentsAsync(Session, filter);
        }
        else
        {
            totalItems = await Collection.CountDocumentsAsync(filter);
        }

        // Get the projected items for the current page
        IEnumerable<TProjection> items;
        if (Session != null)
        {
            items = await Collection.Find(Session, filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .Project(projection)
                .ToListAsync();
        }
        else
        {
            items = await Collection.Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .Project(projection)
                .ToListAsync();
        }

        // Create and return the paged result
        return new PagedResult<TProjection>
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
        if (Session != null)
        {
            return await Collection.Find(Session, filter).Project(projection).ToListAsync();
        }
        return await Collection.Find(filter).Project(projection).ToListAsync();
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<TEntity>> GetWithDefinitionAsync(FilterDefinition<TEntity> filter)
    {
        if (Session != null)
        {
            return await Collection.Find(Session, filter).ToListAsync();
        }
        return await Collection.Find(filter).ToListAsync();
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<TEntity>> GetWithDefinitionAsync(
        FilterDefinition<TEntity> filter,
        SortDefinition<TEntity> sort,
        int? limit = null)
    {
        var findFluent = Session != null
            ? Collection.Find(Session, filter).Sort(sort)
            : Collection.Find(filter).Sort(sort);

        if (limit.HasValue)
        {
            findFluent = findFluent.Limit(limit.Value);
        }

        return await findFluent.ToListAsync();
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<TProjection>> GetWithDefinitionAsync<TProjection>(
        FilterDefinition<TEntity> filter,
        ProjectionDefinition<TEntity, TProjection> projection)
    {
        if (Session != null)
        {
            return await Collection.Find(Session, filter).Project(projection).ToListAsync();
        }
        return await Collection.Find(filter).Project(projection).ToListAsync();
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<TProjection>> GetWithDefinitionAsync<TProjection>(
        FilterDefinition<TEntity> filter,
        ProjectionDefinition<TEntity, TProjection> projection,
        SortDefinition<TEntity> sort,
        int? limit = null)
    {
        var findFluent = Session != null
            ? Collection.Find(Session, filter).Sort(sort)
            : Collection.Find(filter).Sort(sort);

        if (limit.HasValue)
        {
            findFluent = findFluent.Limit(limit.Value);
        }

        return await findFluent.Project(projection).ToListAsync();
    }

    /// <summary>
    /// Gets the MongoDB collection used by this repository
    /// </summary>
    /// <returns>The MongoDB collection</returns>
    public virtual IMongoCollection<TEntity> GetCollection()
    {
        return Collection;
    }
}