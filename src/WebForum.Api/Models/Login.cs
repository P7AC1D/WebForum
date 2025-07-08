using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Login model for user authentication
/// </summary>
public class Login
{
  /// <summary>
  /// Email address for authentication
  /// </summary>
  [Required(ErrorMessage = "Email is required")]
  [EmailAddress(ErrorMessage = "Please provide a valid email address")]
  [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
  public string Email { get; set; } = string.Empty;

  /// <summary>
  /// Password for authentication
  /// </summary>
  [Required(ErrorMessage = "Password is required")]
  [StringLength(100, ErrorMessage = "Password must not exceed 100 characters")]
  public string Password { get; set; } = string.Empty;

  /// <summary>
  /// Validates the login data
  /// </summary>
  /// <returns>List of validation errors, empty if valid</returns>
  public List<string> Validate()
  {
    var errors = new List<string>();

    // Additional business rule validations
    if (string.IsNullOrWhiteSpace(Email))
      errors.Add("Email cannot be empty or whitespace");

    if (string.IsNullOrWhiteSpace(Password))
      errors.Add("Password cannot be empty or whitespace");

    // Validate email format
    if (!string.IsNullOrEmpty(Email))
    {
      var emailAttribute = new EmailAddressAttribute();
      if (!emailAttribute.IsValid(Email))
        errors.Add("Invalid email format");
    }

    return errors;
  }
}
