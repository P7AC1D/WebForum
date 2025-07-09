using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Data.DTOs;

/// <summary>
/// Database entity for comments on forum posts
/// </summary>
public class CommentEntity
{
  public int Id { get; set; }

  [Required]
  public string Content { get; set; } = string.Empty;

  public int PostId { get; set; }

  public int AuthorId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  /// <summary>
  /// Convert CommentEntity to domain Comment model
  /// </summary>
  /// <returns>Domain Comment model</returns>
  public Models.Comment ToDomainModel()
  {
    return new Models.Comment
    {
      Id = Id,
      Content = Content,
      PostId = PostId,
      AuthorId = AuthorId,
      CreatedAt = CreatedAt
    };
  }

  /// <summary>
  /// Create CommentEntity from domain Comment model
  /// </summary>
  /// <param name="comment">Domain Comment model</param>
  /// <returns>CommentEntity for database storage</returns>
  public static CommentEntity FromDomainModel(Models.Comment comment)
  {
    return new CommentEntity
    {
      Id = comment.Id,
      Content = comment.Content,
      PostId = comment.PostId,
      AuthorId = comment.AuthorId,
      CreatedAt = comment.CreatedAt
    };
  }
}
