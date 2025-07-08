namespace WebForum.Api.Models;

/// <summary>
/// Generic pagination result container for API responses
/// </summary>
/// <typeparam name="T">Type of items in the paginated collection</typeparam>
public class PagedResult<T>
{
  /// <summary>
  /// Collection of items for the current page
  /// </summary>
  public List<T> Items { get; set; } = new();

  /// <summary>
  /// Total number of items across all pages
  /// </summary>
  public int TotalCount { get; set; }

  /// <summary>
  /// Current page number (1-based)
  /// </summary>
  public int Page { get; set; }

  /// <summary>
  /// Number of items per page
  /// </summary>
  public int PageSize { get; set; }

  /// <summary>
  /// Total number of pages available
  /// </summary>
  public int TotalPages { get; set; }

  /// <summary>
  /// Indicates if there is a next page available
  /// </summary>
  public bool HasNext { get; set; }

  /// <summary>
  /// Indicates if there is a previous page available
  /// </summary>
  public bool HasPrevious { get; set; }

  /// <summary>
  /// Creates a paginated result with automatic calculation of pagination metadata
  /// </summary>
  /// <param name="items">Items for the current page</param>
  /// <param name="totalCount">Total number of items across all pages</param>
  /// <param name="page">Current page number (1-based)</param>
  /// <param name="pageSize">Number of items per page</param>
  /// <returns>Configured PagedResult with calculated metadata</returns>
  public static PagedResult<T> Create(List<T> items, int totalCount, int page, int pageSize)
  {
    var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
    
    return new PagedResult<T>
    {
      Items = items,
      TotalCount = totalCount,
      Page = page,
      PageSize = pageSize,
      TotalPages = totalPages,
      HasNext = page < totalPages,
      HasPrevious = page > 1
    };
  }

  /// <summary>
  /// Creates an empty paginated result
  /// </summary>
  /// <param name="page">Current page number</param>
  /// <param name="pageSize">Number of items per page</param>
  /// <returns>Empty PagedResult</returns>
  public static PagedResult<T> Empty(int page = 1, int pageSize = 10)
  {
    return Create(new List<T>(), 0, page, pageSize);
  }

  /// <summary>
  /// Validates pagination parameters and returns validation errors
  /// </summary>
  /// <param name="page">Page number to validate</param>
  /// <param name="pageSize">Page size to validate</param>
  /// <param name="maxPageSize">Maximum allowed page size (default: 100)</param>
  /// <returns>List of validation errors, empty if valid</returns>
  public static List<string> ValidatePaginationParameters(int page, int pageSize, int maxPageSize = 100)
  {
    var errors = new List<string>();

    if (page < 1)
      errors.Add("Page number must be 1 or greater");

    if (pageSize < 1)
      errors.Add("Page size must be 1 or greater");

    if (pageSize > maxPageSize)
      errors.Add($"Page size cannot exceed {maxPageSize}");

    return errors;
  }
}