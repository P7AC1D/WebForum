using WebForum.Api.Models;

namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for moderation operations including post tagging
/// </summary>
public interface IModerationService
{
  /// <summary>
  /// Tag a post as "misleading or false information" for regulatory compliance
  /// </summary>
  /// <param name="postId">Post ID to tag</param>
  /// <param name="moderatorId">Moderator user ID performing the action</param>
  /// <returns>Moderation response with tag details</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="InvalidOperationException">Thrown when post is already tagged</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  /// <exception cref="UnauthorizedAccessException">Thrown when user is not a moderator</exception>
  Task<ModerationResponse> TagPostAsync(int postId, int moderatorId);

  /// <summary>
  /// Remove "misleading or false information" tag from a post
  /// </summary>
  /// <param name="postId">Post ID to remove tag from</param>
  /// <param name="moderatorId">Moderator user ID performing the action</param>
  /// <returns>Moderation response with removal details</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post or tag is not found</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  /// <exception cref="UnauthorizedAccessException">Thrown when user is not a moderator</exception>
  Task<ModerationResponse> RemoveTagFromPostAsync(int postId, int moderatorId);

  /// <summary>
  /// Get all posts that have been tagged as "misleading or false information"
  /// </summary>
  /// <param name="page">Page number for pagination</param>
  /// <param name="pageSize">Number of posts per page</param>
  /// <returns>Paginated list of tagged posts with moderation details</returns>
  /// <exception cref="ArgumentException">Thrown when pagination parameters are invalid</exception>
  Task<PagedResult<TaggedPost>> GetTaggedPostsAsync(int page, int pageSize);

  /// <summary>
  /// Check if a post is tagged as "misleading or false information"
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <returns>True if post is tagged, false otherwise</returns>
  Task<bool> IsPostTaggedAsync(int postId);

  /// <summary>
  /// Check if a user is a moderator
  /// </summary>
  /// <param name="userId">User ID to check</param>
  /// <returns>True if user is a moderator, false otherwise</returns>
  Task<bool> IsUserModeratorAsync(int userId);

  /// <summary>
  /// Get moderation history for a specific post
  /// </summary>
  /// <param name="postId">Post ID to get history for</param>
  /// <returns>List of moderation actions performed on the post</returns>
  Task<IEnumerable<PostTag>> GetModerationHistoryForPostAsync(int postId);
}
