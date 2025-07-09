namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for moderation actions
/// </summary>
public class ModerationResponse
{
  /// <summary>
  /// ID of the post that was moderated
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// Type of moderation action performed
  /// </summary>
  public string Action { get; set; } = string.Empty; // "tagged" or "untagged"

  /// <summary>
  /// The tag that was applied or removed
  /// </summary>
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
  /// When the moderation action was performed
  /// </summary>
  public DateTimeOffset ActionTimestamp { get; set; }
}
