using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Model for creating new forum posts
/// </summary>
public class CreatePost
{
  /// <summary>
  /// Post title (required, 5-200 characters)
  /// </summary>
  [Required(ErrorMessage = "Title is required")]
  [StringLength(200, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 200 characters")]
  public string Title { get; set; } = string.Empty;

  /// <summary>
  /// Post content (required, 10-10000 characters)
  /// </summary>
  [Required(ErrorMessage = "Content is required")]
  [StringLength(10000, MinimumLength = 10, ErrorMessage = "Content must be between 10 and 10,000 characters")]
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// Validates the post creation data
  /// </summary>
  /// <returns>List of validation errors, empty if valid</returns>
  public List<string> Validate()
  {
    var errors = new List<string>();

    // Additional business rule validations
    if (string.IsNullOrWhiteSpace(Title))
      errors.Add("Title cannot be empty or whitespace");

    if (string.IsNullOrWhiteSpace(Content))
      errors.Add("Content cannot be empty or whitespace");

    // Check for appropriate title format
    if (!string.IsNullOrEmpty(Title) && Title.Trim().Length != Title.Length)
      errors.Add("Title cannot start or end with whitespace");

    // Check for minimum meaningful content
    if (!string.IsNullOrEmpty(Content) && Content.Trim().Length < 10)
      errors.Add("Content must contain at least 10 meaningful characters");

    return errors;
  }

  /// <summary>
  /// Converts CreatePost to Post entity
  /// </summary>
  /// <param name="authorId">ID of the user creating the post</param>
  /// <returns>Post entity ready for database insertion</returns>
  public Post ToPost(int authorId)
  {
    var now = DateTimeOffset.UtcNow;

    return new Post
    {
      Title = Title.Trim(),
      Content = Content.Trim(),
      AuthorId = authorId,
      CreatedAt = now,
      UpdatedAt = now
    };
  }
}