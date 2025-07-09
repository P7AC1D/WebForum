using System.Text.Json.Serialization;
using WebForum.Api.Converters;

namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for user information (without sensitive data)
/// </summary>
public class UserResponse
{
  /// <summary>
  /// Unique identifier for the user
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Username
  /// </summary>
  public string Username { get; set; } = string.Empty;

  /// <summary>
  /// Email address
  /// </summary>
  public string Email { get; set; } = string.Empty;

  /// <summary>
  /// User role
  /// </summary>
  [JsonConverter(typeof(UserRolesJsonConverter))]
  public UserRoles Role { get; set; } = UserRoles.User;

  /// <summary>
  /// When the user account was created
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>
  /// Creates a UserResponse from a User domain model
  /// </summary>
  /// <param name="user">User domain model</param>
  /// <returns>UserResponse for API consumption (excludes sensitive data)</returns>
  public static UserResponse FromUser(User user)
  {
    return new UserResponse
    {
      Id = user.Id,
      Username = user.Username,
      Email = user.Email,
      Role = user.Role,
      CreatedAt = user.CreatedAt
    };
  }
}
