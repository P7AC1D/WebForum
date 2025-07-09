using WebForum.Api.Models;

namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for security operations including JWT tokens and password hashing
/// </summary>
public interface ISecurityService
{
  /// <summary>
  /// Generate JWT token for authenticated user
  /// </summary>
  /// <param name="user">User to generate token for</param>
  /// <returns>JWT token string</returns>
  string GenerateJwtToken(User user);

  /// <summary>
  /// Validate JWT token and extract user information
  /// </summary>
  /// <param name="token">JWT token to validate</param>
  /// <returns>User ID if token is valid</returns>
  /// <exception cref="UnauthorizedAccessException">Thrown when token is invalid or expired</exception>
  int ValidateJwtToken(string token);

  /// <summary>
  /// Extract user ID from JWT token claims
  /// </summary>
  /// <param name="token">JWT token</param>
  /// <returns>User ID from token claims</returns>
  /// <exception cref="UnauthorizedAccessException">Thrown when token is invalid</exception>
  int GetUserIdFromToken(string token);

  /// <summary>
  /// Hash password using BCrypt
  /// </summary>
  /// <param name="password">Plain text password</param>
  /// <returns>Hashed password</returns>
  string HashPassword(string password);

  /// <summary>
  /// Verify password against hash using BCrypt
  /// </summary>
  /// <param name="password">Plain text password</param>
  /// <param name="hash">Hashed password</param>
  /// <returns>True if password matches hash</returns>
  bool VerifyPassword(string password, string hash);

  /// <summary>
  /// Generate refresh token
  /// </summary>
  /// <returns>Refresh token string</returns>
  string GenerateRefreshToken();

  /// <summary>
  /// Validate refresh token
  /// </summary>
  /// <param name="refreshToken">Refresh token to validate</param>
  /// <returns>True if refresh token is valid</returns>
  bool ValidateRefreshToken(string refreshToken);

  /// <summary>
  /// Get token expiration time in seconds
  /// </summary>
  /// <returns>Token expiration time</returns>
  int GetTokenExpirationSeconds();
}
