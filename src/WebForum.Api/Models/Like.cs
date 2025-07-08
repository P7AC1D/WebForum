namespace WebForum.Api.Models;

public class Like
{
  public int Id { get; set; }

  public int PostId { get; set; }

  public int UserId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
