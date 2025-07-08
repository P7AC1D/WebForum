namespace WebForum.Api.Models;

/// <summary>
/// Response model for moderation actions
/// </summary>
public class ModerationResponse
{
  public int PostId { get; set; }
  public string Action { get; set; } = string.Empty; // "tagged" or "untagged"
  public string Tag { get; set; } = string.Empty;
  public int ModeratorId { get; set; }
  public string ModeratorUsername { get; set; } = string.Empty;
  public DateTimeOffset ActionTimestamp { get; set; }
}
