using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

/// <summary>
/// Model for token refresh requests
/// </summary>
public class RefreshToken
{
  /// <summary>
  /// Current access token to be refreshed
  /// </summary>
  [Required(ErrorMessage = "Access token is required")]
  [StringLength(2048, ErrorMessage = "Token must not exceed 2048 characters")]
  public string AccessToken { get; set; } = string.Empty;

  /// <summary>
  /// Optional refresh token for extended authentication
  /// </summary>
  [StringLength(2048, ErrorMessage = "Refresh token must not exceed 2048 characters")]
  public string? RefreshTokenValue { get; set; }

  /// <summary>
  /// Validates the refresh token data
  /// </summary>
  /// <returns>List of validation errors, empty if valid</returns>
  public List<string> Validate()
  {
    var errors = new List<string>();

    // Additional business rule validations
    if (string.IsNullOrWhiteSpace(AccessToken))
      errors.Add("Access token cannot be empty or whitespace");

    // Validate token format (basic JWT structure check)
    if (!string.IsNullOrEmpty(AccessToken) && !IsValidJwtFormat(AccessToken))
      errors.Add("Invalid token format");

    return errors;
  }

  /// <summary>
  /// Validates basic JWT token format (header.payload.signature)
  /// </summary>
  /// <param name="token">Token to validate</param>
  /// <returns>True if token has valid JWT structure</returns>
  private static bool IsValidJwtFormat(string token)
  {
    if (string.IsNullOrEmpty(token))
      return false;

    var parts = token.Split('.');
    return parts.Length == 3 && parts.All(part => !string.IsNullOrEmpty(part));
  }
}