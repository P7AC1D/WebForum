using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Implementation of authentication service for user registration, login, and token management
/// </summary>
public class AuthService : IAuthService
{
  private readonly ForumDbContext _context;
  private readonly ISecurityService _securityService;
  private readonly IUserService _userService;
  private readonly ILogger<AuthService> _logger;

  public AuthService(
      ForumDbContext context,
      ISecurityService securityService,
      IUserService userService,
      ILogger<AuthService> logger)
  {
    _context = context;
    _securityService = securityService;
    _userService = userService;
    _logger = logger;
  }

  /// <summary>
  /// Register a new user account
  /// </summary>
  public async Task<Models.Response.AuthResponse> RegisterAsync(RegistrationRequest registration)
  {
    // Validate input first
    if (registration == null)
      throw new ArgumentException("Registration data is required");

    _logger.LogInformation("Starting user registration for email: {Email}", registration.Email);

    try
    {
      // Check if user already exists by email
      var existingUserByEmail = await _userService.GetUserByEmailAsync(registration.Email);
      if (existingUserByEmail != null)
      {
        _logger.LogWarning("Registration failed - email already exists: {Email}", registration.Email);
        throw new InvalidOperationException("A user with this email address already exists");
      }

      // Check if user already exists by username
      var existingUserByUsername = await _userService.GetUserByUsernameAsync(registration.Username);
      if (existingUserByUsername != null)
      {
        _logger.LogWarning("Registration failed - username already exists: {Username}", registration.Username);
        throw new InvalidOperationException("A user with this username already exists");
      }

      // Hash password
      var passwordHash = _securityService.HashPassword(registration.Password);

      // Create new user entity using registration model
      var user = registration.ToUser(passwordHash);
      var userEntity = UserEntity.FromDomainModel(user);

      // Save to database
      _context.Users.Add(userEntity);
      await _context.SaveChangesAsync();

      // Set the generated ID back to the domain model
      user.Id = userEntity.Id;

      _logger.LogInformation("User registered successfully with ID: {UserId}", user.Id);

      // Generate JWT token and refresh token
      var token = _securityService.GenerateJwtToken(user);
      var refreshToken = _securityService.GenerateRefreshToken();
      var expiresIn = _securityService.GetTokenExpirationSeconds();

      // Return authentication response
      return Models.Response.AuthResponse.FromUser(user, token, expiresIn, refreshToken);
    }
    catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
    {
      _logger.LogError(ex, "Error during user registration for email: {Email}", registration.Email);
      throw;
    }
  }

  /// <summary>
  /// Authenticate user and return access token
  /// </summary>
  public async Task<Models.Response.AuthResponse> LoginAsync(LoginRequest login)
  {
    // Validate input first
    if (login == null)
      throw new ArgumentException("Login data is required");

    _logger.LogInformation("Starting user login for username/email: {Email}", login.Email);

    try
    {
      // Try to find user by email first, then by username
      User? user = null;

      // Check if the input looks like an email
      if (login.Email.Contains('@'))
      {
        user = await _userService.GetUserByEmailAsync(login.Email);
      }
      else
      {
        user = await _userService.GetUserByUsernameAsync(login.Email);
      }

      if (user == null)
      {
        _logger.LogWarning("Login failed - user not found: {Email}", login.Email);
        throw new UnauthorizedAccessException("Invalid username/email or password");
      }

      // Verify password
      if (!_securityService.VerifyPassword(login.Password, user.PasswordHash))
      {
        _logger.LogWarning("Login failed - invalid password for user: {Email}", login.Email);
        throw new UnauthorizedAccessException("Invalid username/email or password");
      }

      _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

      // Generate JWT token and refresh token
      var token = _securityService.GenerateJwtToken(user);
      var refreshToken = _securityService.GenerateRefreshToken();
      var expiresIn = _securityService.GetTokenExpirationSeconds();

      // Return authentication response
      return Models.Response.AuthResponse.FromUser(user, token, expiresIn, refreshToken);
    }
    catch (Exception ex) when (!(ex is ArgumentException || ex is UnauthorizedAccessException))
    {
      _logger.LogError(ex, "Error during user login for username/email: {Email}", login.Email);
      throw;
    }
  }

  /// <summary>
  /// Refresh access token using refresh token
  /// </summary>
  public async Task<Models.Response.AuthResponse> RefreshTokenAsync(RefreshToken refreshToken)
  {
    _logger.LogInformation("Starting token refresh");

    try
    {
      // Validate input
      if (refreshToken == null)
        throw new ArgumentException("Refresh token data is required");

      // For now, we'll validate using the access token to extract user info
      // In a production system, you'd store refresh tokens in the database with expiration
      // and validate the RefreshTokenValue against stored tokens

      // Validate and extract user ID from current access token
      int userId;
      try
      {
        // Extract user ID from access token (may be expired, but we can still get user ID)
        userId = _securityService.GetUserIdFromToken(refreshToken.AccessToken);
      }
      catch
      {
        _logger.LogWarning("Token refresh failed - could not extract user ID from token");
        throw new UnauthorizedAccessException("Invalid token format");
      }

      // Basic refresh token validation (in production, validate against stored tokens)
      if (!string.IsNullOrEmpty(refreshToken.RefreshTokenValue) &&
          !_securityService.ValidateRefreshToken(refreshToken.RefreshTokenValue))
      {
        _logger.LogWarning("Token refresh failed - invalid refresh token");
        throw new UnauthorizedAccessException("Invalid or expired refresh token");
      }

      // Get user information
      var user = await _userService.GetUserByIdAsync(userId);
      if (user == null)
      {
        _logger.LogWarning("Token refresh failed - user not found: {UserId}", userId);
        throw new UnauthorizedAccessException("User not found");
      }

      _logger.LogInformation("Token refreshed successfully for user: {UserId}", user.Id);

      // Generate new JWT token and refresh token
      var newToken = _securityService.GenerateJwtToken(user);
      var newRefreshToken = _securityService.GenerateRefreshToken();
      var expiresIn = _securityService.GetTokenExpirationSeconds();

      // Return new authentication response
      return Models.Response.AuthResponse.FromUser(user, newToken, expiresIn, newRefreshToken);
    }
    catch (Exception ex) when (!(ex is ArgumentException || ex is UnauthorizedAccessException))
    {
      _logger.LogError(ex, "Error during token refresh");
      throw;
    }
  }

  /// <summary>
  /// Validate JWT token and extract user information
  /// </summary>
  public async Task<User> ValidateTokenAsync(string token)
  {
    _logger.LogDebug("Validating JWT token");

    try
    {
      // Validate input
      if (string.IsNullOrWhiteSpace(token))
        throw new UnauthorizedAccessException("Token is required");

      // Validate JWT token and extract user ID
      var userId = _securityService.ValidateJwtToken(token);

      // Get user information
      var user = await _userService.GetUserByIdAsync(userId);
      if (user == null)
      {
        _logger.LogWarning("Token validation failed - user not found: {UserId}", userId);
        throw new UnauthorizedAccessException("User not found");
      }

      _logger.LogDebug("Token validated successfully for user: {UserId}", user.Id);
      return user;
    }
    catch (Exception ex) when (!(ex is UnauthorizedAccessException))
    {
      _logger.LogError(ex, "Error during token validation");
      throw new UnauthorizedAccessException("Invalid token");
    }
  }
}
