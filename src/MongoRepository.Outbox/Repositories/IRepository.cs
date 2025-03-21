using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoRepository.Outbox.Models;

namespace MongoRepository.Outbox.Repositories;

/// <summary>
/// Interface for a basic MongoDB repository
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public interface IRepository<T> where T : Entity
{
    /// <summary>
    /// Gets all entities
    /// </summary>
    /// <returns>A collection of all entities</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Gets an entity by its ID
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <returns>The entity, or null if not found</returns>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// Gets entities that match a filter expression
    /// </summary>
    /// <param name="filter">The filter expression</param>
    /// <returns>A collection of entities that match the filter</returns>
    Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Gets entities that match a MongoDB filter definition
    /// </summary>
    /// <param name="filter">The MongoDB filter definition</param>
    /// <param name="sort">Optional MongoDB sort definition</param>
    /// <param name="limit">Optional limit on the number of results</param>
    /// <returns>A collection of entities that match the filter</returns>
    Task<IEnumerable<T>> GetWithDefinitionAsync(
        FilterDefinition<T> filter,
        SortDefinition<T>? sort = null,
        int? limit = null);

    /// <summary>
    /// Counts entities that match a filter expression
    /// </summary>
    /// <param name="filter">The filter expression</param>
    /// <returns>The count of entities that match the filter</returns>
    Task<long> CountAsync(FilterDefinition<T> filter);

    /// <summary>
    /// Adds a new entity
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task AddAsync(T entity);

    /// <summary>
    /// Updates an existing entity
    /// </summary>
    /// <param name="entity">The entity to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Deletes an entity by its ID
    /// </summary>
    /// <param name="id">The ID of the entity to delete</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task DeleteAsync(string id);
}