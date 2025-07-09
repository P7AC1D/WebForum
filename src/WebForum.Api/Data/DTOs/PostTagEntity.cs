using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Data.DTOs;

/// <summary>
/// Database entity for post tags/labels
/// </summary>
public class PostTagEntity
{
  public int Id { get; set; }

  public int PostId { get; set; }

  [Required]
  [StringLength(50)]
  public string Tag { get; set; } = string.Empty;

  public int CreatedByUserId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  /// <summary>
  /// Convert PostTagEntity to domain PostTag model
  /// </summary>
  /// <returns>Domain PostTag model</returns>
  public Models.PostTag ToDomainModel()
  {
    return new Models.PostTag
    {
      Id = Id,
      PostId = PostId,
      Tag = Tag,
      CreatedByUserId = CreatedByUserId,
      CreatedAt = CreatedAt
    };
  }

  /// <summary>
  /// Create PostTagEntity from domain PostTag model
  /// </summary>
  /// <param name="postTag">Domain PostTag model</param>
  /// <returns>PostTagEntity for database storage</returns>
  public static PostTagEntity FromDomainModel(Models.PostTag postTag)
  {
    return new PostTagEntity
    {
      Id = postTag.Id,
      PostId = postTag.PostId,
      Tag = postTag.Tag,
      CreatedByUserId = postTag.CreatedByUserId,
      CreatedAt = postTag.CreatedAt
    };
  }
}
