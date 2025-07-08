using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Registration model for new user account creation
/// </summary>
public class Registration
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
  /// Converts the registration model to a User entity
  /// </summary>
  /// <param name="passwordHash">The hashed password</param>
  /// <returns>User entity ready for database insertion</returns>
  public User ToUser(string passwordHash)
  {
    return new User
    {
      Username = Username,
      Email = Email,
      PasswordHash = passwordHash,
      Role = UserRoles.User, // Default role for new registrations
      CreatedAt = DateTimeOffset.UtcNow,
      UpdatedAt = DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Validates the registration data
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

    // Username validation - alphanumeric and underscore only
    if (!string.IsNullOrEmpty(Username) && !System.Text.RegularExpressions.Regex.IsMatch(Username, @"^[a-zA-Z0-9_]+$"))
      errors.Add("Username can only contain letters, numbers, and underscores");

    return errors;
  }
}
