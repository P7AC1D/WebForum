namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for comments with author information
/// </summary>
public class CommentResponse
{
  /// <summary>
  /// Unique identifier for the comment
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Comment content
  /// </summary>
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// ID of the post this comment belongs to
  /// </summary>
  public int PostId { get; set; }

  /// <summary>
  /// ID of the comment author
  /// </summary>
  public int AuthorId { get; set; }

  /// <summary>
  /// When the comment was created
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>
  /// Creates a CommentResponse from a Comment domain model
  /// </summary>
  /// <param name="comment">Comment domain model</param>
  /// <returns>CommentResponse for API consumption</returns>
  public static CommentResponse FromComment(Comment comment)
  {
    return new CommentResponse
    {
      Id = comment.Id,
      Content = comment.Content,
      PostId = comment.PostId,
      AuthorId = comment.AuthorId,
      CreatedAt = comment.CreatedAt
    };
  }
}
