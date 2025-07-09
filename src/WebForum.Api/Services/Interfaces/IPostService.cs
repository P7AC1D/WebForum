using WebForum.Api.Models;
using WebForum.Api.Models.Request;

namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for post management including creation, retrieval, and like operations
/// </summary>
public interface IPostService
{
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
  Task<PagedResult<Post>> GetPostsAsync(
      int page,
      int pageSize,
      int? authorId = null,
      DateTimeOffset? dateFrom = null,
      DateTimeOffset? dateTo = null,
      string? tags = null,
      string sortBy = "date",
      string sortOrder = "desc");

  /// <summary>
  /// Get a specific post by ID with complete details
  /// </summary>
  /// <param name="postId">Post ID to retrieve</param>
  /// <returns>Post details with author information, like count, and comment count</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="ArgumentException">Thrown when post ID is invalid</exception>
  Task<Post> GetPostByIdAsync(int postId);

  /// <summary>
  /// Create a new forum post
  /// </summary>
  /// <param name="createPost">Post creation data including title and content</param>
  /// <param name="authorId">ID of the user creating the post</param>
  /// <returns>Created post information with generated ID and timestamps</returns>
  /// <exception cref="ArgumentException">Thrown when post data or author ID is invalid</exception>
  Task<Post> CreatePostAsync(CreatePostRequest createPost, int authorId);

  /// <summary>
  /// Check if a post exists by ID
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <returns>True if post exists, false otherwise</returns>
  Task<bool> PostExistsAsync(int postId);

  /// <summary>
  /// Get the author ID of a specific post
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <returns>Author ID of the post</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  Task<int> GetPostAuthorIdAsync(int postId);
}
