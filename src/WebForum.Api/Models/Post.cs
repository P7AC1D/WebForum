using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Domain model representing a forum post created by users
/// </summary>
/// <remarks>
/// Business rules:
/// - Posts are permanent (no deletion per forum policy)
/// - Content is sanitized to prevent XSS attacks
/// - CommentCount and LikeCount are computed at runtime and not stored in this model
/// - Posts can be tagged by moderators for content moderation
/// </remarks>
public class Post
{
  /// <summary>
  /// Unique identifier for the post
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Post title (5-200 characters, required)
  /// </summary>
  [Required]
  [StringLength(200)]
  public string Title { get; set; } = string.Empty;

  /// <summary>
  /// Post content/body (10-10000 characters, required, sanitized)
  /// </summary>
  [Required]
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// ID of the user who authored this post
  /// </summary>
  public int AuthorId { get; set; }

  /// <summary>
  /// When the post was created (UTC timestamp)
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  // Note: CommentCount and LikeCount are computed at the service layer
  // and included in response models, not stored in the domain model
}
