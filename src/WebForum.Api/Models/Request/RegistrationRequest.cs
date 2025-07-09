using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using WebForum.Api.Converters;
using WebForum.Api.Models;

namespace WebForum.Api.Models.Request;

/// <summary>
/// Request model for user registration
/// </summary>
public class RegistrationRequest
{
  /// <summary>
  /// Username for the new account (3-50 characters)
  /// </summary>
  [Required(ErrorMessage = "Username is required")]
  [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
  public string Username { get; set; } = string.Empty;

  /// <summary>
  /// Valid email address for the new account
  /// </summary>
  [Required(ErrorMessage = "Email is required")]
  [EmailAddress(ErrorMessage = "Please provide a valid email address")]
  [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
  public string Email { get; set; } = string.Empty;

  /// <summary>
  /// Password for the new account (minimum 6 characters)
  /// </summary>
  [Required(ErrorMessage = "Password is required")]
  [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
  [StringLength(100, ErrorMessage = "Password must not exceed 100 characters")]
  public string Password { get; set; } = string.Empty;

  /// <summary>
  /// Optional role for the new account (defaults to User)
  /// </summary>
  [JsonConverter(typeof(NullableUserRolesJsonConverter))]
  public UserRoles? Role { get; set; }

  /// <summary>
  /// Validates the registration request data
  /// </summary>
  /// <returns>List of validation errors, empty if valid</returns>
  public List<string> Validate()
  {
    var errors = new List<string>();

    // Additional business rule validations
    if (string.IsNullOrWhiteSpace(Username))
      errors.Add("Username cannot be empty or whitespace");

    if (string.IsNullOrWhiteSpace(Email))
      errors.Add("Email cannot be empty or whitespace");

    if (string.IsNullOrWhiteSpace(Password))
      errors.Add("Password cannot be empty or whitespace");

    // Check for appropriate format
    if (!string.IsNullOrEmpty(Username) && Username.Trim().Length != Username.Length)
      errors.Add("Username cannot start or end with whitespace");

    if (!string.IsNullOrEmpty(Email) && Email.Trim().Length != Email.Length)
      errors.Add("Email cannot start or end with whitespace");

    return errors;
  }

  /// <summary>
  /// Converts the registration request to a User domain model
  /// </summary>
  /// <param name="passwordHash">The hashed password</param>
  /// <returns>User domain model ready for database insertion</returns>
  public User ToUser(string passwordHash)
  {
    return new User
    {
      Username = Username.Trim(),
      Email = Email.Trim().ToLower(),
      PasswordHash = passwordHash,
      Role = Role ?? UserRoles.User, // Use provided role or default to User
      CreatedAt = DateTimeOffset.UtcNow
    };
  }
}
