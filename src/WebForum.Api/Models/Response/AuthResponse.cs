namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for authentication operations including login, registration, and token refresh
/// </summary>
/// <remarks>
/// This model provides comprehensive authentication response data including JWT tokens,
/// expiration information, and user details. Used across all authentication endpoints.
/// </remarks>
public class AuthResponse
{
  /// <summary>
  /// JWT access token for API authentication
  /// </summary>
  public string AccessToken { get; set; } = string.Empty;

  /// <summary>
  /// Token type specification (always "Bearer" for JWT)
  /// </summary>
  public string TokenType { get; set; } = "Bearer";

  /// <summary>
  /// Token expiration time in seconds from issuance
  /// </summary>
  public int ExpiresIn { get; set; }

  /// <summary>
  /// Exact UTC timestamp when the token expires
  /// </summary>
  public DateTimeOffset ExpiresAt { get; set; }

  /// <summary>
  /// Optional refresh token for obtaining new access tokens without re-authentication
  /// </summary>
  public string? RefreshToken { get; set; }

  /// <summary>
  /// Authenticated user information including profile data and permissions
  /// </summary>
  public UserResponse User { get; set; } = new();

  /// <summary>
  /// Creates an AuthResponse from a User domain model and token information
  /// </summary>
  /// <param name="user">User domain model containing profile information</param>
  /// <param name="accessToken">JWT access token string</param>
  /// <param name="expiresIn">Token expiration time in seconds</param>
  /// <param name="refreshToken">Optional refresh token for extended authentication</param>
  /// <returns>Formatted authentication response ready for API consumption</returns>
  public static AuthResponse FromUser(User user, string accessToken, int expiresIn, string? refreshToken = null)
  {
    return new AuthResponse
    {
      AccessToken = accessToken,
      TokenType = "Bearer",
      ExpiresIn = expiresIn,
      ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
      RefreshToken = refreshToken,
      User = UserResponse.FromUser(user)
    };
  }
}
