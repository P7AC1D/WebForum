using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Service implementation for post management including creation, retrieval, and like operations
/// </summary>
public class PostService : IPostService
{
  private readonly ForumDbContext _context;

  public PostService(ForumDbContext context)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
  }

  /// <summary>
  /// Get posts with comprehensive filtering, sorting, and pagination
  /// </summary>
  /// <param name="page">Page number for pagination</param>
  /// <param name="pageSize">Number of posts per page</param>
  /// <param name="authorId">Filter posts by specific author ID</param>
  /// <param name="dateFrom">Filter posts created from this date onwards</param>
  /// <param name="dateTo">Filter posts created until this date</param>
  /// <param name="tags">Filter posts by tags (comma-separated list)</param>
  /// <param name="sortBy">Sort field: 'date' or 'likeCount'</param>
  /// <param name="sortOrder">Sort order: 'asc' or 'desc'</param>
  /// <returns>Paginated list of posts with like counts and author information</returns>
  /// <exception cref="ArgumentException">Thrown when query parameters are invalid</exception>
  public async Task<PagedResult<Post>> GetPostsAsync(
      int page,
      int pageSize,
      int? authorId = null,
      DateTimeOffset? dateFrom = null,
      DateTimeOffset? dateTo = null,
      string? tags = null,
      string sortBy = "date",
      string sortOrder = "desc")
  {
    if (page < 1)
      throw new ArgumentException("Page must be greater than zero", nameof(page));

    if (pageSize < 1 || pageSize > 100)
      throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

    if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
      throw new ArgumentException("DateFrom must be before DateTo");

    var query = _context.Posts.AsQueryable();

    // Apply filters
    if (authorId.HasValue)
    {
      if (authorId <= 0)
        throw new ArgumentException("Author ID must be greater than zero", nameof(authorId));
      query = query.Where(p => p.AuthorId == authorId.Value);
    }

    if (dateFrom.HasValue)
      query = query.Where(p => p.CreatedAt >= dateFrom.Value);

    if (dateTo.HasValue)
      query = query.Where(p => p.CreatedAt <= dateTo.Value);

    if (!string.IsNullOrWhiteSpace(tags))
    {
      var tagsList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim().ToLower())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

      if (tagsList.Any())
      {
        query = query.Where(p => _context.PostTags
            .Any(pt => pt.PostId == p.Id && tagsList.Contains(pt.Tag.ToLower())));
      }
    }

    // Apply sorting
    query = sortBy?.ToLower() switch
    {
      "likecount" => sortOrder?.ToLower() == "asc"
          ? query.OrderBy(p => _context.Likes.Count(l => l.PostId == p.Id))
          : query.OrderByDescending(p => _context.Likes.Count(l => l.PostId == p.Id)),
      "date" or _ => sortOrder?.ToLower() == "asc"
          ? query.OrderBy(p => p.CreatedAt)
          : query.OrderByDescending(p => p.CreatedAt)
    };

    var totalItems = await query.CountAsync();
    var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

    var postEntities = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    // Convert DTO entities to domain models
    var posts = postEntities.Select(pe => pe.ToDomainModel()).ToList();

    return new PagedResult<Post>
    {
      Items = posts,
      Page = page,
      PageSize = pageSize,
      TotalCount = totalItems,
      TotalPages = totalPages,
      HasPrevious = page > 1,
      HasNext = page < totalPages
    };
  }

  /// <summary>
  /// Get a specific post by ID with complete details
  /// </summary>
  /// <param name="postId">Post ID to retrieve</param>
  /// <returns>Post details with author information, like count, and comment count</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="ArgumentException">Thrown when post ID is invalid</exception>
  public async Task<Post> GetPostByIdAsync(int postId)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    var postEntity = await _context.Posts
        .FirstOrDefaultAsync(p => p.Id == postId);

    if (postEntity == null)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    return postEntity.ToDomainModel();
  }

  /// <summary>
  /// Create a new forum post
  /// </summary>
  /// <param name="createPost">Post creation data including title and content</param>
  /// <param name="authorId">ID of the user creating the post</param>
  /// <returns>Created post information with generated ID and timestamps</returns>
  /// <exception cref="ArgumentException">Thrown when post data or author ID is invalid</exception>
  public async Task<Post> CreatePostAsync(CreatePost createPost, int authorId)
  {
    if (createPost == null)
      throw new ArgumentNullException(nameof(createPost));

    if (authorId <= 0)
      throw new ArgumentException("Author ID must be greater than zero", nameof(authorId));

    // Validate the post data
    var validationErrors = createPost.Validate();
    if (validationErrors.Any())
      throw new ArgumentException($"Post validation failed: {string.Join(", ", validationErrors)}");

    // Check if author exists
    var authorExists = await _context.Users.AnyAsync(u => u.Id == authorId);
    if (!authorExists)
      throw new ArgumentException($"Author with ID {authorId} does not exist", nameof(authorId));

    // Convert to Post domain model first, then to DTO entity
    var post = createPost.ToPost(authorId);
    var postEntity = PostEntity.FromDomainModel(post);

    // Add to database
    _context.Posts.Add(postEntity);
    await _context.SaveChangesAsync();

    // Set the generated ID back to the domain model
    post.Id = postEntity.Id;
    return post;
  }

  /// <summary>
  /// Check if a post exists by ID
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <returns>True if post exists, false otherwise</returns>
  public async Task<bool> PostExistsAsync(int postId)
  {
    if (postId <= 0)
      return false;

    return await _context.Posts.AnyAsync(p => p.Id == postId);
  }

  /// <summary>
  /// Get the author ID of a specific post
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <returns>Author ID of the post</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  public async Task<int> GetPostAuthorIdAsync(int postId)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    var post = await _context.Posts
        .Select(p => new { p.Id, p.AuthorId })
        .FirstOrDefaultAsync(p => p.Id == postId);

    if (post == null)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    return post.AuthorId;
  }
}
