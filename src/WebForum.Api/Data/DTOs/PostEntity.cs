using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Data.DTOs;

/// <summary>
/// Database entity for forum posts
/// </summary>
public class PostEntity
{
  public int Id { get; set; }

  [Required]
  [StringLength(200)]
  public string Title { get; set; } = string.Empty;

  [Required]
  public string Content { get; set; } = string.Empty;

  public int AuthorId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  /// <summary>
  /// Convert PostEntity to domain Post model
  /// </summary>
  /// <returns>Domain Post model</returns>
  public Models.Post ToDomainModel()
  {
    return new Models.Post
    {
      Id = Id,
      Title = Title,
      Content = Content,
      AuthorId = AuthorId,
      CreatedAt = CreatedAt
    };
  }

  /// <summary>
  /// Create PostEntity from domain Post model
  /// </summary>
  /// <param name="post">Domain Post model</param>
  /// <returns>PostEntity for database storage</returns>
  public static PostEntity FromDomainModel(Models.Post post)
  {
    return new PostEntity
    {
      Id = post.Id,
      Title = post.Title,
      Content = post.Content,
      AuthorId = post.AuthorId,
      CreatedAt = post.CreatedAt
    };
  }
}
