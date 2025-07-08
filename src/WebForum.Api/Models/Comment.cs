using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

public class Comment
{
  public int Id { get; set; }

  [Required]
  public string Content { get; set; } = string.Empty;

  public int PostId { get; set; }

  public int AuthorId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
