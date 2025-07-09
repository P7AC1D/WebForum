namespace WebForum.Api.Models;

/// <summary>
/// User information for API responses (authentication and profiles)
/// </summary>
public class UserInfo
{
  /// <summary>
  /// User's unique identifier
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// User's username
  /// </summary>
  public string Username { get; set; } = string.Empty;

  /// <summary>
  /// User's email address (included in auth responses, excluded from public profiles)
  /// </summary>
  public string? Email { get; set; }

  /// <summary>
  /// User's role in the system
  /// </summary>
  public string Role { get; set; } = string.Empty;

  /// <summary>
  /// When the user account was created
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>
  /// User's last activity timestamp (optional for profiles)
  /// </summary>
  public DateTimeOffset? LastActive { get; set; }

  /// <summary>
  /// Total number of posts created by user (optional for profiles)
  /// </summary>
  public int? PostCount { get; set; }

  /// <summary>
  /// Total number of comments made by user (optional for profiles)
  /// </summary>
  public int? CommentCount { get; set; }

  /// <summary>
  /// Total number of likes received on user's posts (optional for profiles)
  /// </summary>
  public int? LikesReceived { get; set; }

  /// <summary>
  /// Creates UserInfo for authentication responses (includes email)
  /// </summary>
  /// <param name="user">User entity</param>
  /// <returns>User information for authentication responses</returns>
  public static UserInfo FromUser(User user)
  {
    return new UserInfo
    {
      Id = user.Id,
      Username = user.Username,
      Email = user.Email,
      Role = user.Role.ToString(),
      CreatedAt = user.CreatedAt
    };
  }

  /// <summary>
  /// Creates UserInfo for public profiles (excludes email, includes statistics)
  /// </summary>
  /// <param name="user">User entity</param>
  /// <param name="postCount">Number of posts by user</param>
  /// <param name="commentCount">Number of comments by user</param>
  /// <param name="likesReceived">Number of likes received on user's posts</param>
  /// <returns>User information for public profile display</returns>
  public static UserInfo ForPublicProfile(User user, int postCount = 0, int commentCount = 0, int likesReceived = 0)
  {
    return new UserInfo
    {
      Id = user.Id,
      Username = user.Username,
      Email = null, // Exclude email from public profiles
      Role = user.Role.ToString(),
      CreatedAt = user.CreatedAt,
      PostCount = postCount,
      CommentCount = commentCount,
      LikesReceived = likesReceived
    };
  }

  /// <summary>
  /// Creates basic UserInfo without statistics (for minimal responses)
  /// </summary>
  /// <param name="user">User entity</param>
  /// <returns>Basic user information</returns>
  public static UserInfo BasicProfile(User user)
  {
    return new UserInfo
    {
      Id = user.Id,
      Username = user.Username,
      Email = null,
      Role = user.Role.ToString(),
      CreatedAt = user.CreatedAt
    };
  }
}