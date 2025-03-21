using System.Linq.Expressions;
using MongoRepository.Core.Models;

namespace MongoRepository.Core.Repositories;

/// <summary>
/// Interface for generic repository operations
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository works with</typeparam>
public interface IRepository<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Gets all entities in the collection
    /// </summary>
    /// <returns>All entities in the collection</returns>
    Task<IEnumerable<TEntity>> GetAllAsync();

    /// <summary>
    /// Gets all entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>All entities that match the filter</returns>
    Task<IEnumerable<TEntity>> GetAsync(Expression<Func<TEntity, bool>> filter);

    /// <summary>
    /// Gets a single entity by its id
    /// </summary>
    /// <param name="id">The id of the entity to get</param>
    /// <returns>The entity with the specified id, or null if not found</returns>
    Task<TEntity> GetByIdAsync(string id);

    /// <summary>
    /// Gets the first entity that matches the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>The first entity that matches the filter, or null if not found</returns>
    Task<TEntity> GetFirstAsync(Expression<Func<TEntity, bool>> filter);

    /// <summary>
    /// Adds a new entity to the collection
    /// </summary>
    /// <param name="entity">The entity to add</param>
    Task AddAsync(TEntity entity);

    /// <summary>
    /// Adds multiple entities to the collection
    /// </summary>
    /// <param name="entities">The entities to add</param>
    Task AddRangeAsync(IEnumerable<TEntity> entities);

    /// <summary>
    /// Updates an existing entity in the collection
    /// </summary>
    /// <param name="entity">The entity to update</param>
    Task UpdateAsync(TEntity entity);

    /// <summary>
    /// Removes an entity from the collection
    /// </summary>
    /// <param name="id">The id of the entity to remove</param>
    Task DeleteAsync(string id);

    /// <summary>
    /// Removes all entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    Task DeleteManyAsync(Expression<Func<TEntity, bool>> filter);

    /// <summary>
    /// Gets a count of entities that match the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>The count of matching entities</returns>
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? filter = null);

    /// <summary>
    /// Checks if any entity matches the specified filter
    /// </summary>
    /// <param name="filter">The filter to apply</param>
    /// <returns>True if any entity matches the filter, otherwise false</returns>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter);
}