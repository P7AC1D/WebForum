using WebForum.Api.Models;

namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for like/unlike operations on posts
/// </summary>
public interface ILikeService
{
  /// <summary>
  /// Toggle like status on a post (like if not liked, unlike if already liked)
  /// </summary>
  /// <param name="postId">Post ID to like/unlike</param>
  /// <param name="userId">User ID performing the action</param>
  /// <returns>Like response with updated status and count</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="InvalidOperationException">Thrown when user tries to like their own post</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  Task<LikeResponse> ToggleLikeAsync(int postId, int userId);

  /// <summary>
  /// Remove like from a post (explicit unlike operation)
  /// </summary>
  /// <param name="postId">Post ID to unlike</param>
  /// <param name="userId">User ID performing the action</param>
  /// <returns>Like response with updated status and count</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post or like is not found</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  Task<LikeResponse> UnlikePostAsync(int postId, int userId);

  /// <summary>
  /// Check if a user has liked a specific post
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <param name="userId">User ID to check</param>
  /// <returns>True if user has liked the post, false otherwise</returns>
  Task<bool> HasUserLikedPostAsync(int postId, int userId);

  /// <summary>
  /// Get the total number of likes for a post
  /// </summary>
  /// <param name="postId">Post ID to count likes for</param>
  /// <returns>Number of likes for the post</returns>
  Task<int> GetLikeCountForPostAsync(int postId);

  /// <summary>
  /// Get all likes for a specific post with user information
  /// </summary>
  /// <param name="postId">Post ID to get likes for</param>
  /// <returns>List of likes with user information</returns>
  Task<IEnumerable<Like>> GetLikesForPostAsync(int postId);
}
