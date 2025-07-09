using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Service implementation for security operations including JWT tokens and password hashing
/// </summary>
public class SecurityService : ISecurityService
{
  private readonly IConfiguration _configuration;
  private readonly string _secretKey;
  private readonly string _issuer;
  private readonly string _audience;
  private readonly int _expirationMinutes;

  public SecurityService(IConfiguration configuration)
  {
    _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    var jwtSettings = _configuration.GetSection("JwtSettings");
    _secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
    _issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
    _audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");

    if (!int.TryParse(jwtSettings["ExpirationInMinutes"], out _expirationMinutes))
    {
      throw new InvalidOperationException("JWT ExpirationInMinutes is not configured or invalid");
    }
  }

  /// <summary>
  /// Generate JWT token for authenticated user
  /// </summary>
  /// <param name="user">User to generate token for</param>
  /// <returns>JWT token string</returns>
  public string GenerateJwtToken(User user)
  {
    if (user == null)
      throw new ArgumentNullException(nameof(user));

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_secretKey);

    var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

    var tokenDescriptor = new SecurityTokenDescriptor
    {
      Subject = new ClaimsIdentity(claims),
      Expires = DateTime.UtcNow.AddMinutes(_expirationMinutes),
      Issuer = _issuer,
      Audience = _audience,
      SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
  }

  /// <summary>
  /// Validate JWT token and extract user information
  /// </summary>
  /// <param name="token">JWT token to validate</param>
  /// <returns>User ID if token is valid</returns>
  /// <exception cref="UnauthorizedAccessException">Thrown when token is invalid or expired</exception>
  public int ValidateJwtToken(string token)
  {
    if (string.IsNullOrWhiteSpace(token))
      throw new UnauthorizedAccessException("Token is required");

    try
    {
      var tokenHandler = new JwtSecurityTokenHandler();
      var key = Encoding.ASCII.GetBytes(_secretKey);

      var validationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _issuer,
        ValidAudience = _audience,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
      };

      var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

      var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
      {
        throw new UnauthorizedAccessException("Invalid token: User ID not found");
      }

      return userId;
    }
    catch (SecurityTokenException ex)
    {
      throw new UnauthorizedAccessException($"Invalid token: {ex.Message}", ex);
    }
    catch (Exception ex)
    {
      throw new UnauthorizedAccessException($"Token validation failed: {ex.Message}", ex);
    }
  }

  /// <summary>
  /// Extract user ID from JWT token claims
  /// </summary>
  /// <param name="token">JWT token</param>
  /// <returns>User ID from token claims</returns>
  /// <exception cref="UnauthorizedAccessException">Thrown when token is invalid</exception>
  public int GetUserIdFromToken(string token)
  {
    return ValidateJwtToken(token);
  }

  /// <summary>
  /// Hash password using BCrypt
  /// </summary>
  /// <param name="password">Plain text password</param>
  /// <returns>Hashed password</returns>
  public string HashPassword(string password)
  {
    if (string.IsNullOrWhiteSpace(password))
      throw new ArgumentException("Password cannot be null or empty", nameof(password));

    return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
  }

  /// <summary>
  /// Verify password against hash using BCrypt
  /// </summary>
  /// <param name="password">Plain text password</param>
  /// <param name="hash">Hashed password</param>
  /// <returns>True if password matches hash</returns>
  public bool VerifyPassword(string password, string hash)
  {
    if (string.IsNullOrWhiteSpace(password))
      return false;

    if (string.IsNullOrWhiteSpace(hash))
      return false;

    try
    {
      return BCrypt.Net.BCrypt.Verify(password, hash);
    }
    catch (Exception)
    {
      // BCrypt can throw exceptions for malformed hashes
      return false;
    }
  }

  /// <summary>
  /// Generate refresh token
  /// </summary>
  /// <returns>Refresh token string</returns>
  public string GenerateRefreshToken()
  {
    var randomBytes = new byte[64];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(randomBytes);
    return Convert.ToBase64String(randomBytes);
  }

  /// <summary>
  /// Validate refresh token
  /// </summary>
  /// <param name="refreshToken">Refresh token to validate</param>
  /// <returns>True if refresh token is valid</returns>
  public bool ValidateRefreshToken(string refreshToken)
  {
    if (string.IsNullOrWhiteSpace(refreshToken))
      return false;

    try
    {
      // Basic validation - check if it's a valid base64 string of expected length
      var bytes = Convert.FromBase64String(refreshToken);
      return bytes.Length == 64; // Expected length for our refresh tokens
    }
    catch (FormatException)
    {
      return false;
    }
  }

  /// <summary>
  /// Get token expiration time in seconds
  /// </summary>
  /// <returns>Token expiration time</returns>
  public int GetTokenExpirationSeconds()
  {
    return _expirationMinutes * 60;
  }
}
