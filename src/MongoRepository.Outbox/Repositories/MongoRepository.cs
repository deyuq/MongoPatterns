using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoRepository.Outbox.Models;

namespace MongoRepository.Outbox.Repositories;

/// <summary>
/// MongoDB implementation of the repository pattern
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class MongoRepository<T> : IRepository<T> where T : Entity
{
    private readonly IMongoCollection<T> _collection;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoRepository{T}"/> class.
    /// </summary>
    /// <param name="collection">The MongoDB collection</param>
    public MongoRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync(string id)
    {
        return await _collection.Find(e => e.Id == id).FirstOrDefaultAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> filter)
    {
        return await _collection.Find(filter).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> GetWithDefinitionAsync(
        FilterDefinition<T> filter,
        SortDefinition<T>? sort = null,
        int? limit = null)
    {
        var query = _collection.Find(filter);

        if (sort != null)
        {
            query = query.Sort(sort);
        }

        if (limit.HasValue)
        {
            query = query.Limit(limit.Value);
        }

        return await query.ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<long> CountAsync(FilterDefinition<T> filter)
    {
        return await _collection.CountDocumentsAsync(filter);
    }

    /// <inheritdoc/>
    public async Task AddAsync(T entity)
    {
        await _collection.InsertOneAsync(entity);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(T entity)
    {
        await _collection.ReplaceOneAsync(e => e.Id == entity.Id, entity);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id)
    {
        await _collection.DeleteOneAsync(e => e.Id == id);
    }
}