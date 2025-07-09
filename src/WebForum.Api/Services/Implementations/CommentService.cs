using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Service implementation for comment management including creation and retrieval
/// </summary>
public class CommentService : ICommentService
{
  private readonly ForumDbContext _context;

  public CommentService(ForumDbContext context)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
  }

  /// <summary>
  /// Get comments for a specific post with pagination and sorting
  /// </summary>
  /// <param name="postId">Post ID whose comments to retrieve</param>
  /// <param name="page">Page number for pagination</param>
  /// <param name="pageSize">Number of comments per page</param>
  /// <param name="sortOrder">Sort order by creation date: 'asc' or 'desc'</param>
  /// <returns>Paginated list of comments for the specified post</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
  public async Task<PagedResult<Comment>> GetPostCommentsAsync(int postId, int page, int pageSize, string sortOrder)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    if (page < 1)
      throw new ArgumentException("Page must be greater than zero", nameof(page));

    if (pageSize < 1 || pageSize > 100)
      throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

    // Check if post exists
    var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
    if (!postExists)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    var query = _context.Comments
        .Where(c => c.PostId == postId);

    // Apply sorting
    query = sortOrder?.ToLower() switch
    {
      "asc" or "oldest" => query.OrderBy(c => c.CreatedAt),
      "desc" or "newest" or _ => query.OrderByDescending(c => c.CreatedAt)
    };

    var totalItems = await query.CountAsync();
    var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

    var comments = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PagedResult<Comment>
    {
      Items = comments,
      Page = page,
      PageSize = pageSize,
      TotalCount = totalItems,
      TotalPages = totalPages,
      HasPrevious = page > 1,
      HasNext = page < totalPages
    };
  }

  /// <summary>
  /// Add a comment to a specific post
  /// </summary>
  /// <param name="postId">Post ID to comment on</param>
  /// <param name="createComment">Comment creation data including content</param>
  /// <param name="authorId">ID of the user creating the comment</param>
  /// <returns>Created comment information with author details and timestamps</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="ArgumentException">Thrown when comment data or IDs are invalid</exception>
  public async Task<Comment> CreateCommentAsync(int postId, CreateComment createComment, int authorId)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    if (createComment == null)
      throw new ArgumentNullException(nameof(createComment));

    if (authorId <= 0)
      throw new ArgumentException("Author ID must be greater than zero", nameof(authorId));

    // Validate the comment data
    var validationErrors = createComment.Validate();
    if (validationErrors.Any())
      throw new ArgumentException($"Comment validation failed: {string.Join(", ", validationErrors)}");

    // Check if post exists
    var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
    if (!postExists)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    // Check if author exists
    var authorExists = await _context.Users.AnyAsync(u => u.Id == authorId);
    if (!authorExists)
      throw new ArgumentException($"Author with ID {authorId} does not exist", nameof(authorId));

    // Convert to Comment entity
    var comment = createComment.ToComment(authorId, postId);

    // Add to database
    _context.Comments.Add(comment);
    await _context.SaveChangesAsync();

    return comment;
  }

  /// <summary>
  /// Check if a comment exists by ID
  /// </summary>
  /// <param name="commentId">Comment ID to check</param>
  /// <returns>True if comment exists, false otherwise</returns>
  public async Task<bool> CommentExistsAsync(int commentId)
  {
    if (commentId <= 0)
      return false;

    return await _context.Comments.AnyAsync(c => c.Id == commentId);
  }

  /// <summary>
  /// Get the count of comments for a specific post
  /// </summary>
  /// <param name="postId">Post ID to count comments for</param>
  /// <returns>Number of comments for the post</returns>
  public async Task<int> GetCommentCountForPostAsync(int postId)
  {
    if (postId <= 0)
      return 0;

    return await _context.Comments.CountAsync(c => c.PostId == postId);
  }
}
