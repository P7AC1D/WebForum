using WebForum.Api.Models;

namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for user authentication and token management
/// </summary>
public interface IAuthService
{
  /// <summary>
  /// Register a new user account
  /// </summary>
  /// <param name="registration">User registration details</param>
  /// <returns>Authentication response with token and user information</returns>
  /// <exception cref="InvalidOperationException">Thrown when username or email already exists</exception>
  /// <exception cref="ArgumentException">Thrown when registration data is invalid</exception>
  Task<AuthResponse> RegisterAsync(Registration registration);

  /// <summary>
  /// Authenticate user and return access token
  /// </summary>
  /// <param name="login">Login credentials</param>
  /// <returns>Authentication response with token and user information</returns>
  /// <exception cref="UnauthorizedAccessException">Thrown when credentials are invalid</exception>
  /// <exception cref="ArgumentException">Thrown when login data is invalid</exception>
  Task<AuthResponse> LoginAsync(Login login);

  /// <summary>
  /// Refresh access token using refresh token
  /// </summary>
  /// <param name="refreshToken">Refresh token request</param>
  /// <returns>New authentication response with refreshed token</returns>
  /// <exception cref="UnauthorizedAccessException">Thrown when refresh token is invalid or expired</exception>
  /// <exception cref="ArgumentException">Thrown when refresh data is invalid</exception>
  Task<AuthResponse> RefreshTokenAsync(RefreshToken refreshToken);

  /// <summary>
  /// Validate JWT token and extract user information
  /// </summary>
  /// <param name="token">JWT token to validate</param>
  /// <returns>User information if token is valid</returns>
  /// <exception cref="UnauthorizedAccessException">Thrown when token is invalid or expired</exception>
  Task<User> ValidateTokenAsync(string token);
}
