namespace WebForum.Api.Models.Response;

/// <summary>
/// Response model for authentication operations (register, login, refresh)
/// </summary>
public class AuthResponse
{
  /// <summary>
  /// JWT access token
  /// </summary>
  public string AccessToken { get; set; } = string.Empty;

  /// <summary>
  /// Token type (always "Bearer")
  /// </summary>
  public string TokenType { get; set; } = "Bearer";

  /// <summary>
  /// Token expiration time in seconds
  /// </summary>
  public int ExpiresIn { get; set; }

  /// <summary>
  /// Exact timestamp when the token expires
  /// </summary>
  public DateTimeOffset ExpiresAt { get; set; }

  /// <summary>
  /// Refresh token for obtaining new access tokens (optional)
  /// </summary>
  public string? RefreshToken { get; set; }

  /// <summary>
  /// Authenticated user information
  /// </summary>
  public UserResponse User { get; set; } = new();

  /// <summary>
  /// Creates an AuthResponse from a User domain model and token information
  /// </summary>
  /// <param name="user">User domain model</param>
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
      User = UserResponse.FromUser(user)
    };
  }
}
