namespace WebForum.Api.Models;

/// <summary>
/// Model for tagged posts with moderation information
/// </summary>
public class TaggedPost
{
  public int Id { get; set; }
  public string Title { get; set; } = string.Empty;
  public string Content { get; set; } = string.Empty;
  public int AuthorId { get; set; }
  public string AuthorUsername { get; set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; set; }
  public string Tag { get; set; } = string.Empty;
  public int TaggedByUserId { get; set; }
  public string TaggedByUsername { get; set; } = string.Empty;
  public DateTimeOffset TaggedAt { get; set; }
}
