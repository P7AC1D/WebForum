namespace WebForum.Api.Data.DTOs;

/// <summary>
/// Database entity for post likes/reactions
/// </summary>
public class LikeEntity
{
  public int Id { get; set; }

  public int PostId { get; set; }

  public int UserId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  /// <summary>
  /// Convert LikeEntity to domain Like model
  /// </summary>
  /// <returns>Domain Like model</returns>
  public Models.Like ToDomainModel()
  {
    return new Models.Like
    {
      Id = Id,
      PostId = PostId,
      UserId = UserId,
      CreatedAt = CreatedAt
    };
  }

  /// <summary>
  /// Create LikeEntity from domain Like model
  /// </summary>
  /// <param name="like">Domain Like model</param>
  /// <returns>LikeEntity for database storage</returns>
  public static LikeEntity FromDomainModel(Models.Like like)
  {
    return new LikeEntity
    {
      Id = like.Id,
      PostId = like.PostId,
      UserId = like.UserId,
      CreatedAt = like.CreatedAt
    };
  }
}
