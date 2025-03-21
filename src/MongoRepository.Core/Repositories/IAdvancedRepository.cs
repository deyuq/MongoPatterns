using System.Linq.Expressions;
using MongoDB.Driver;
using MongoRepository.Core.Models;

namespace MongoRepository.Core.Repositories;

/// <summary>
/// Interface for advanced repository operations
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository works with</typeparam>
public interface IAdvancedRepository<TEntity> : IRepository<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Gets a queryable of entities in the collection
    /// </summary>
    /// <returns>An IQueryable of entities</returns>
    IQueryable<TEntity> AsQueryable();

    /// <summary>
    /// Performs a bulk update of entities matching the filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <param name="update">The update definition</param>
    /// <returns>The result of the update operation</returns>
    Task<UpdateResult> BulkUpdateAsync(Expression<Func<TEntity, bool>> filter, UpdateDefinition<TEntity> update);

    /// <summary>
    /// Gets a filtered and sorted collection of entities with pagination
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <param name="sortField">The field to sort by</param>
    /// <param name="ascending">Whether to sort ascending</param>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The page size</param>
    /// <returns>A paged result containing the entities and pagination metadata</returns>
    Task<PagedResult<TEntity>> GetPagedAsync(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, object>> sortField,
        bool ascending = true,
        int page = 1,
        int pageSize = 10);

    /// <summary>
    /// Gets entities with a projection to a different type
    /// </summary>
    /// <typeparam name="TProjection">The type to project to</typeparam>
    /// <param name="filter">The filter to apply</param>
    /// <param name="projection">The projection to apply</param>
    /// <returns>The projected entities</returns>
    Task<IEnumerable<TProjection>> GetWithProjectionAsync<TProjection>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TProjection>> projection);

    /// <summary>
    /// Gets the MongoDB collection used by this repository
    /// </summary>
    /// <returns>The MongoDB collection</returns>
    IMongoCollection<TEntity> GetCollection();
}