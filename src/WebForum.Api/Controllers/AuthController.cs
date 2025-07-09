using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Controllers;

/// <summary>
/// Authentication controller for user registration, login, and token management
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
  private readonly IAuthService _authService = authService;
  private readonly ILogger<AuthController> _logger = logger;

  /// <summary>
  /// Register a new user account
  /// </summary>
  /// <param name="registration">User registration details</param>
  /// <returns>Authentication response with token and user information</returns>
  /// <response code="201">User successfully registered</response>
  /// <response code="400">Invalid registration data or user already exists</response>
  /// <response code="500">Internal server error</response>
  [HttpPost("register")]
  [ProducesResponseType(typeof(Models.Response.AuthResponse), 201)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> Register([FromBody] RegistrationRequest registration)
  {
    try
    {
      _logger.LogInformation("User registration attempt for email: {Email}", registration?.Email);

      if (registration == null)
        return BadRequest("Registration data is required");

      // Validate input
      var validationErrors = registration.Validate();
      if (validationErrors.Any())
      {
        _logger.LogWarning("Registration validation failed: {Errors}", string.Join(", ", validationErrors));
        return BadRequest(new { Errors = validationErrors });
      }

      var result = await _authService.RegisterAsync(registration);

      _logger.LogInformation("User registered successfully with ID: {UserId}", result.User.Id);
      return Created($"/api/users/{result.User.Id}", result);
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning("Registration failed: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Registration validation error: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during user registration");
      return StatusCode(500, "An error occurred during registration");
    }
  }

  /// <summary>
  /// Authenticate user and return access token
  /// </summary>
  /// <param name="login">Login credentials</param>
  /// <returns>Authentication response with token and user information</returns>
  /// <response code="200">Successfully authenticated</response>
  /// <response code="401">Invalid credentials</response>
  /// <response code="400">Invalid login data</response>
  /// <response code="500">Internal server error</response>
  [HttpPost("login")]
  [ProducesResponseType(typeof(Models.Response.AuthResponse), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> Login([FromBody] LoginRequest login)
  {
    try
    {
      _logger.LogInformation("User login attempt for username/email: {Email}", login?.Email);

      if (login == null)
        return BadRequest("Login data is required");

      // Validate input
      var validationErrors = login.Validate();
      if (validationErrors.Any())
      {
        _logger.LogWarning("Login validation failed: {Errors}", string.Join(", ", validationErrors));
        return BadRequest(new { Errors = validationErrors });
      }

      var result = await _authService.LoginAsync(login);

      _logger.LogInformation("User logged in successfully: {UserId}", result.User.Id);
      return Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
      _logger.LogWarning("Login failed: {Message}", ex.Message);
      return Unauthorized(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Login validation error: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during user login");
      return StatusCode(500, "An error occurred during login");
    }
  }

  /// <summary>
  /// Refresh access token using refresh token
  /// </summary>
  /// <param name="refreshToken">Refresh token request</param>
  /// <returns>New authentication response with refreshed token</returns>
  /// <response code="200">Token successfully refreshed</response>
  /// <response code="401">Invalid or expired refresh token</response>
  /// <response code="400">Invalid refresh data</response>
  /// <response code="500">Internal server error</response>
  [HttpPost("refresh")]
  [ProducesResponseType(typeof(Models.Response.AuthResponse), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> Refresh([FromBody] RefreshToken refreshToken)
  {
    try
    {
      _logger.LogInformation("Token refresh attempt");

      if (refreshToken == null)
        return BadRequest("Refresh token data is required");

      var result = await _authService.RefreshTokenAsync(refreshToken);

      _logger.LogInformation("Token refreshed successfully for user: {UserId}", result.User.Id);
      return Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
      _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
      return Unauthorized(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Token refresh validation error: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during token refresh");
      return StatusCode(500, "An error occurred during token refresh");
    }
  }
}
