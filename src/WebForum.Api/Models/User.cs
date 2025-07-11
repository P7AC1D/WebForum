using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Domain model representing a forum user with authentication and profile information
/// </summary>
public class User
{
  /// <summary>
  /// Unique identifier for the user
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// User's unique username (3-50 characters, required)
  /// </summary>
  [Required]
  [StringLength(50, MinimumLength = 3)]
  public string Username { get; set; } = string.Empty;

  /// <summary>
  /// User's email address (valid email format, max 100 characters, required)
  /// </summary>
  [Required]
  [EmailAddress]
  [StringLength(100)]
  public string Email { get; set; } = string.Empty;

  /// <summary>
  /// BCrypt hashed password for authentication (required)
  /// </summary>
  [Required]
  public string PasswordHash { get; set; } = string.Empty;

  /// <summary>
  /// User's role in the system (User or Moderator, defaults to User)
  /// </summary>
  [Required]
  public UserRoles Role { get; set; } = UserRoles.User;

  /// <summary>
  /// When the user account was created (UTC timestamp)
  /// </summary>
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
