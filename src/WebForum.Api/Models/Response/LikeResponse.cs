namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for like/unlike operations
/// </summary>
public class LikeResponse
{
  /// <summary>
  /// ID of the post that was liked/unliked
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// Whether the current user has liked the post
  /// </summary>
  public bool IsLiked { get; set; }

  /// <summary>
  /// Total number of likes on the post
  /// </summary>
  public int LikeCount { get; set; }

  /// <summary>
  /// When the like action was performed
  /// </summary>
  public DateTimeOffset ActionTimestamp { get; set; } = DateTimeOffset.UtcNow;
}
