using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models.Request;

/// <summary>
/// Request model for user login
/// </summary>
public class LoginRequest
{
  /// <summary>
  /// Username or email address (accepts both 'email' and 'usernameOrEmail' for flexibility)
  /// </summary>
  [Required(ErrorMessage = "Username or email is required")]
  [StringLength(100, ErrorMessage = "Username or email must not exceed 100 characters")]
  public string Email { get; set; } = string.Empty;

  /// <summary>
  /// User password
  /// </summary>
  [Required(ErrorMessage = "Password is required")]
  [StringLength(100, ErrorMessage = "Password must not exceed 100 characters")]
  public string Password { get; set; } = string.Empty;

  /// <summary>
  /// Validates the login request data
  /// </summary>
  /// <returns>List of validation errors, empty if valid</returns>
  public List<string> Validate()
  {
    var errors = new List<string>();

    // Additional business rule validations
    if (string.IsNullOrWhiteSpace(Email))
      errors.Add("Username or email cannot be empty or whitespace");

    if (string.IsNullOrWhiteSpace(Password))
      errors.Add("Password cannot be empty or whitespace");

    // Check for appropriate format
    if (!string.IsNullOrEmpty(Email) && Email.Trim().Length != Email.Length)
      errors.Add("Username or email cannot start or end with whitespace");

    return errors;
  }
}
