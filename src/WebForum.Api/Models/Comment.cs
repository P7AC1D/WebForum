using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Domain model representing a comment on a forum post
/// </summary>
/// <remarks>
/// Business rules:
/// - Comments are permanent (no deletion per forum policy)
/// - Content is sanitized to prevent XSS attacks
/// - Comments are associated with a specific post and author
/// </remarks>
public class Comment
{
  /// <summary>
  /// Unique identifier for the comment
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Comment content (3-2000 characters, required, sanitized)
  /// </summary>
  [Required]
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// ID of the post this comment belongs to
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// ID of the user who authored this comment
  /// </summary>
  public int AuthorId { get; set; }

  /// <summary>
  /// When the comment was created (UTC timestamp)
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
