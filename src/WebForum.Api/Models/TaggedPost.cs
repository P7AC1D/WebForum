namespace WebForum.Api.Models;

/// <summary>
/// Model representing a forum post that has been tagged by moderators with additional moderation metadata
/// </summary>
/// <remarks>
/// This model combines post data with moderation information for administrative views.
/// Used primarily in moderation workflows to display tagged content with context.
/// </remarks>
public class TaggedPost
{
  /// <summary>
  /// Unique identifier for the post
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Title of the post (max 200 characters)
  /// </summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>
  /// Content/body of the post
  /// </summary>
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// ID of the user who authored the post
  /// </summary>
  public int AuthorId { get; set; }

  /// <summary>
  /// When the post was originally created (UTC timestamp)
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>
  /// The moderation tag applied to this post
  /// </summary>
  /// <example>misleading-information, false-information</example>
  public string Tag { get; set; } = string.Empty;

  /// <summary>
  /// ID of the moderator who applied the tag
  /// </summary>
  public int TaggedByUserId { get; set; }

  /// <summary>
  /// Username of the moderator who applied the tag
  /// </summary>
  public string TaggedByUsername { get; set; } = string.Empty;

  /// <summary>
  /// When the tag was applied to the post (UTC timestamp)
  /// </summary>
  public DateTimeOffset TaggedAt { get; set; }
}
