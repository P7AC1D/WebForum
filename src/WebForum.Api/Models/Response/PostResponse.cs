namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for forum posts with computed properties
/// </summary>
public class PostResponse
{
  /// <summary>
  /// Unique identifier for the post
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Post title
  /// </summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>
  /// Post content
  /// </summary>
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// ID of the post author
  /// </summary>
  public int AuthorId { get; set; }

  /// <summary>
  /// When the post was created
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>
  /// Number of comments on this post
  /// </summary>
  public int CommentCount { get; set; }

  /// <summary>
  /// Number of likes on this post
  /// </summary>
  public int LikeCount { get; set; }

  /// <summary>
  /// Creates a PostResponse from a Post domain model
  /// </summary>
  /// <param name="post">Post domain model</param>
  /// <param name="commentCount">Number of comments on the post</param>
  /// <param name="likeCount">Number of likes on the post</param>
  /// <returns>PostResponse for API consumption</returns>
  public static PostResponse FromPost(Post post, int commentCount = 0, int likeCount = 0)
  {
    return new PostResponse
    {
      Id = post.Id,
      Title = post.Title,
      Content = post.Content,
      AuthorId = post.AuthorId,
      CreatedAt = post.CreatedAt,
      CommentCount = commentCount,
      LikeCount = likeCount
    };
  }
}
