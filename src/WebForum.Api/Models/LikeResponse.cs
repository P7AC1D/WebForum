namespace WebForum.Api.Models;

/// <summary>
/// Response model for like/unlike operations on forum posts
/// </summary>
/// <remarks>
/// This model is returned when a user likes or unlikes a post,
/// providing the current state and total count for the post.
/// </remarks>
public class LikeResponse
{
  /// <summary>
  /// ID of the post that was liked or unliked
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// Indicates whether the current user has liked the post
  /// </summary>
  public bool IsLiked { get; set; }

  /// <summary>
  /// Total number of likes on the post after the operation
  /// </summary>
  public int LikeCount { get; set; }
}
