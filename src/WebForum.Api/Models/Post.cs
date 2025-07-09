using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Domain model for forum posts
/// </summary>
public class Post
{
  public int Id { get; set; }

  [Required]
  [StringLength(200)]
  public string Title { get; set; } = string.Empty;

  [Required]
  public string Content { get; set; } = string.Empty;

  public int AuthorId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  // Note: CommentCount and LikeCount are computed at the service layer
  // and included in response models, not stored in the domain model
}
