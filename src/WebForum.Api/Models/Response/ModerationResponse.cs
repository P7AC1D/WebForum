namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for moderation actions performed on forum posts
/// </summary>
/// <remarks>
/// This model provides confirmation and details when moderators apply or remove
/// tags from posts for content moderation and regulatory compliance purposes.
/// </remarks>
public class ModerationResponse
{
  /// <summary>
  /// ID of the post that was moderated
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// Type of moderation action performed ("tagged" or "untagged")
  /// </summary>
  /// <example>tagged, untagged</example>
  public string Action { get; set; } = string.Empty;

  /// <summary>
  /// The moderation tag that was applied or removed
  /// </summary>
  /// <example>misleading-information, false-information</example>
  public string Tag { get; set; } = string.Empty;

  /// <summary>
  /// ID of the moderator who performed the action
  /// </summary>
  public int ModeratorId { get; set; }

  /// <summary>
  /// Username of the moderator who performed the action
  /// </summary>
  public string ModeratorUsername { get; set; } = string.Empty;

  /// <summary>
  /// When the moderation action was performed (UTC timestamp)
  /// </summary>
  public DateTimeOffset ActionTimestamp { get; set; }
}
