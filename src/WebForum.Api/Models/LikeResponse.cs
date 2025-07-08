namespace WebForum.Api.Models;

/// <summary>
/// Response model for like/unlike operations
/// </summary>
public class LikeResponse
{
  public int PostId { get; set; }
  public bool IsLiked { get; set; }
  public int LikeCount { get; set; }
}
