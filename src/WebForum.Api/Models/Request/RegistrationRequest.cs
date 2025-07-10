using System.ComponentModel.DataAnnotations;
using System.Linq;
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
  /// Password for the new account (8+ characters)
  /// </summary>
  [Required(ErrorMessage = "Password is required")]
  [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
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

    // Enhanced email validation
    if (!string.IsNullOrWhiteSpace(Email))
    {
      var emailValidationErrors = ValidateEmailFormat(Email);
      errors.AddRange(emailValidationErrors);
    }

    // Enhanced password validation
    if (!string.IsNullOrWhiteSpace(Password))
    {
      var passwordValidationErrors = ValidatePasswordComplexity(Password);
      errors.AddRange(passwordValidationErrors);
    }

    return errors;
  }

  /// <summary>
  /// Validates email format using strict rules
  /// </summary>
  /// <param name="email">Email to validate</param>
  /// <returns>List of email validation errors</returns>
  private List<string> ValidateEmailFormat(string email)
  {
    var errors = new List<string>();

    // Check for basic email format issues
    if (!email.Contains('@'))
    {
      errors.Add("Email must contain @ symbol");
      return errors;
    }

    var parts = email.Split('@');
    if (parts.Length != 2)
    {
      errors.Add("Email must contain exactly one @ symbol");
      return errors;
    }

    var localPart = parts[0];
    var domainPart = parts[1];

    // Validate local part (before @)
    if (string.IsNullOrWhiteSpace(localPart))
    {
      errors.Add("Email local part cannot be empty");
    }

    // Validate domain part (after @)
    if (string.IsNullOrWhiteSpace(domainPart))
    {
      errors.Add("Email domain part cannot be empty");
    }
    else
    {
      if (!domainPart.Contains('.'))
      {
        errors.Add("Email domain must contain at least one dot");
      }
      else
      {
        var domainParts = domainPart.Split('.');
        if (domainParts.Any(part => string.IsNullOrWhiteSpace(part)))
        {
          errors.Add("Email domain parts cannot be empty");
        }
      }
    }

    // Check for consecutive dots
    if (email.Contains(".."))
    {
      errors.Add("Email cannot contain consecutive dots");
    }

    // Check for invalid starting/ending characters
    if (email.StartsWith('.') || email.EndsWith('.') || email.StartsWith('@') || email.EndsWith('@'))
    {
      errors.Add("Email cannot start or end with dot or @ symbol");
    }

    return errors;
  }

  /// <summary>
  /// Validates password length requirements
  /// </summary>
  /// <param name="password">Password to validate</param>
  /// <returns>List of password validation errors</returns>
  private List<string> ValidatePasswordComplexity(string password)
  {
    var errors = new List<string>();

    if (password.Length < 8)
    {
      errors.Add("Password must be at least 8 characters long");
    }

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
