using System.Collections.Generic;

namespace MongoRepository.Core.Models;

/// <summary>
/// Represents a paged result of data with pagination metadata
/// </summary>
/// <typeparam name="T">The type of the items in the result</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// Gets or sets the items for the current page
    /// </summary>
    public IEnumerable<T> Items { get; set; } = new List<T>();

    /// <summary>
    /// Gets or sets the current page number (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all pages
    /// </summary>
    public long TotalItems { get; set; }

    /// <summary>
    /// Gets the total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>
    /// Gets a value indicating whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
}