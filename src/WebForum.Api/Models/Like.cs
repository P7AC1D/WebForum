namespace WebForum.Api.Models;

/// <summary>
/// Domain model representing a user's like on a forum post
/// </summary>
/// <remarks>
/// Business rules:
/// - Each user can only like a post once
/// - Users cannot like their own posts
/// - Likes are permanent (no deletion, only unlike)
/// </remarks>
public class Like
{
  /// <summary>
  /// Unique identifier for the like
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// ID of the post that was liked
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// ID of the user who liked the post
  /// </summary>
  public int UserId { get; set; }

  /// <summary>
  /// When the like was created (UTC timestamp)
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
