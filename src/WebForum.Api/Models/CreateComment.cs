using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Model for creating new comments on forum posts
/// </summary>
public class CreateComment
{
  /// <summary>
  /// Comment content (required, 3-2000 characters)
  /// </summary>
  [Required(ErrorMessage = "Content is required")]
  [StringLength(2000, MinimumLength = 3, ErrorMessage = "Content must be between 3 and 2,000 characters")]
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// Validates the comment creation data
  /// </summary>
  /// <returns>List of validation errors, empty if valid</returns>
  public List<string> Validate()
  {
    var errors = new List<string>();

    // Additional business rule validations
    if (string.IsNullOrWhiteSpace(Content))
      errors.Add("Content cannot be empty or whitespace");

    // Check for minimum meaningful content
    if (!string.IsNullOrEmpty(Content) && Content.Trim().Length < 3)
      errors.Add("Content must contain at least 3 meaningful characters");

    // Check for appropriate content format
    if (!string.IsNullOrEmpty(Content) && Content.Trim().Length != Content.Length)
      errors.Add("Content cannot start or end with whitespace");

    return errors;
  }

  /// <summary>
  /// Converts CreateComment to Comment entity
  /// </summary>
  /// <param name="authorId">ID of the user creating the comment</param>
  /// <param name="postId">ID of the post being commented on</param>
  /// <returns>Comment entity ready for database insertion</returns>
  public Comment ToComment(int authorId, int postId)
  {
    var now = DateTimeOffset.UtcNow;

    return new Comment
    {
      Content = Content.Trim(),
      AuthorId = authorId,
      PostId = postId,
      CreatedAt = now,
      UpdatedAt = now
    };
  }
}