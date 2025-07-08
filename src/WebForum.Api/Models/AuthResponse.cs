namespace WebForum.Api.Models;

/// <summary>
/// Response model for authentication operations (register, login, refresh)
/// </summary>
public class AuthResponse
{
  /// <summary>
  /// JWT access token for API authentication
  /// </summary>
  public string AccessToken { get; set; } = string.Empty;

  /// <summary>
  /// Token type (typically "Bearer")
  /// </summary>
  public string TokenType { get; set; } = "Bearer";

  /// <summary>
  /// Token expiration time in seconds from now
  /// </summary>
  public int ExpiresIn { get; set; }

  /// <summary>
  /// Token expiration as UTC timestamp
  /// </summary>
  public DateTimeOffset ExpiresAt { get; set; }

  /// <summary>
  /// Optional refresh token for token renewal
  /// </summary>
  public string? RefreshToken { get; set; }

  /// <summary>
  /// Authenticated user information
  /// </summary>
  public UserInfo User { get; set; } = new();

  /// <summary>
  /// Creates an AuthResponse from a User entity and token information
  /// </summary>
  /// <param name="user">User entity</param>
  /// <param name="accessToken">JWT access token</param>
  /// <param name="expiresIn">Token expiration in seconds</param>
  /// <param name="refreshToken">Optional refresh token</param>
  /// <returns>Formatted authentication response</returns>
  public static AuthResponse FromUser(User user, string accessToken, int expiresIn, string? refreshToken = null)
  {
    return new AuthResponse
    {
      AccessToken = accessToken,
      TokenType = "Bearer",
      ExpiresIn = expiresIn,
      ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
      RefreshToken = refreshToken,
      User = UserInfo.FromUser(user)
    };
  }
}