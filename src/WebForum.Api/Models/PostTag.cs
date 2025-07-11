using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Domain model representing a moderation tag applied to a forum post
/// </summary>
/// <remarks>
/// Tags are used primarily for content moderation and regulatory compliance.
/// Only moderators can apply and remove tags from posts.
/// </remarks>
public class PostTag
{
  /// <summary>
  /// Unique identifier for the post tag
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// ID of the post that has been tagged
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// Tag name/label (max 50 characters, required)
  /// </summary>
  /// <example>misleading-information, false-information</example>
  [Required]
  [StringLength(50)]
  public string Tag { get; set; } = string.Empty;

  /// <summary>
  /// ID of the moderator who applied this tag
  /// </summary>
  public int CreatedByUserId { get; set; }

  /// <summary>
  /// When the tag was applied (UTC timestamp)
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Static class containing predefined tag constants for content moderation
/// </summary>
public static class PostTags
{
  /// <summary>
  /// Tag for content that contains misleading information
  /// </summary>
  public const string MisleadingInformation = "misleading-information";

  /// <summary>
  /// Tag for content that contains false information
  /// </summary>
  public const string FalseInformation = "false-information";
}
