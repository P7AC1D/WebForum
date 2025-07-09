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
  /// Username of the comment author
  /// </summary>
  public string AuthorUsername { get; set; } = string.Empty;

  /// <summary>
  /// When the comment was created
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>
  /// Creates a CommentResponse from a Comment domain model
  /// </summary>
  /// <param name="comment">Comment domain model</param>
  /// <param name="authorUsername">Username of the comment author</param>
  /// <returns>CommentResponse for API consumption</returns>
  public static CommentResponse FromComment(Comment comment, string authorUsername = "")
  {
    return new CommentResponse
    {
      Id = comment.Id,
      Content = comment.Content,
      PostId = comment.PostId,
      AuthorId = comment.AuthorId,
      AuthorUsername = authorUsername,
      CreatedAt = comment.CreatedAt
    };
  }
}
