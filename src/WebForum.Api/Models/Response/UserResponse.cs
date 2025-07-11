using System.Text.Json.Serialization;
using WebForum.Api.Converters;

namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for user information excluding sensitive data like password hashes
/// </summary>
/// <remarks>
/// This model is used for API responses where user information needs to be displayed
/// but sensitive authentication data must be protected. Supports flexible JSON
/// serialization for role values.
/// </remarks>
public class UserResponse
{
  /// <summary>
  /// Unique identifier for the user
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// User's display name (3-50 characters)
  /// </summary>
  public string Username { get; set; } = string.Empty;

  /// <summary>
  /// User's email address (used for notifications and account recovery)
  /// </summary>
  public string Email { get; set; } = string.Empty;

  /// <summary>
  /// User's role in the system with flexible JSON serialization
  /// </summary>
  /// <remarks>
  /// Supports both string and integer role values in JSON for API compatibility.
  /// </remarks>
  [JsonConverter(typeof(UserRolesJsonConverter))]
  public UserRoles Role { get; set; } = UserRoles.User;

  /// <summary>
  /// When the user account was created (UTC timestamp)
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>
  /// Creates a UserResponse from a User domain model, filtering out sensitive data
  /// </summary>
  /// <param name="user">User domain model containing full user information</param>
  /// <returns>UserResponse safe for API consumption (excludes password hash and other sensitive data)</returns>
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
