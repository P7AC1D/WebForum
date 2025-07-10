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

    // Validate email format if it contains @ (indicating it's meant to be an email)
    if (!string.IsNullOrEmpty(Email) && Email.Contains('@'))
    {
      if (!IsValidEmail(Email))
        errors.Add("Invalid email format");
    }
    // Special case: if it contains "email" but no @, it's probably a malformed email attempt
    else if (!string.IsNullOrEmpty(Email) && Email.ToLower().Contains("email"))
    {
      errors.Add("Invalid email format");
    }

    return errors;
  }

  /// <summary>
  /// Validate email format using regex pattern
  /// </summary>
  private static bool IsValidEmail(string email)
  {
    if (string.IsNullOrWhiteSpace(email))
      return false;

    // Strict email regex pattern
    var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
    return System.Text.RegularExpressions.Regex.IsMatch(email, emailPattern);
  }
}
