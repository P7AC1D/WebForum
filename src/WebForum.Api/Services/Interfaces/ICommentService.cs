using WebForum.Api.Models;

namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for comment management including creation and retrieval
/// </summary>
public interface ICommentService
{
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
  Task<PagedResult<Comment>> GetPostCommentsAsync(int postId, int page, int pageSize, string sortOrder);

  /// <summary>
  /// Get a specific comment by ID with author information
  /// </summary>
  /// <param name="commentId">Comment ID to retrieve</param>
  /// <returns>Comment details with author information</returns>
  /// <exception cref="KeyNotFoundException">Thrown when comment is not found</exception>
  /// <exception cref="ArgumentException">Thrown when comment ID is invalid</exception>
  Task<Comment> GetCommentByIdAsync(int commentId);

  /// <summary>
  /// Add a comment to a specific post
  /// </summary>
  /// <param name="postId">Post ID to comment on</param>
  /// <param name="createComment">Comment creation data including content</param>
  /// <param name="authorId">ID of the user creating the comment</param>
  /// <returns>Created comment information with author details and timestamps</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="ArgumentException">Thrown when comment data or IDs are invalid</exception>
  Task<Comment> CreateCommentAsync(int postId, CreateComment createComment, int authorId);

  /// <summary>
  /// Check if a comment exists by ID
  /// </summary>
  /// <param name="commentId">Comment ID to check</param>
  /// <returns>True if comment exists, false otherwise</returns>
  Task<bool> CommentExistsAsync(int commentId);

  /// <summary>
  /// Get the count of comments for a specific post
  /// </summary>
  /// <param name="postId">Post ID to count comments for</param>
  /// <returns>Number of comments for the post</returns>
  Task<int> GetCommentCountForPostAsync(int postId);
}
