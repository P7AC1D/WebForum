using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebForum.Api.Controllers;

/// <summary>
/// Debug controller for troubleshooting authentication and claims in test environment
/// </summary>
[ApiController]
[Route("debug")]
public class SimpleDebugController : ControllerBase
{
  /// <summary>
  /// Returns the current authentication state without requiring authentication
  /// </summary>
  [HttpGet("auth-status")]
  public IActionResult GetAuthStatus()
  {
    return Ok(new
    {
      IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
      AuthenticationType = User.Identity?.AuthenticationType,
      NameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
      Name = User.FindFirst(ClaimTypes.Name)?.Value,
      Email = User.FindFirst(ClaimTypes.Email)?.Value,
      Role = User.FindFirst(ClaimTypes.Role)?.Value,
      AllClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
    });
  }

  /// <summary>
  /// Returns the current authentication state with authentication required
  /// </summary>
  [HttpGet("auth-required")]
  [Authorize]
  public IActionResult GetAuthRequired()
  {
    return Ok(new
    {
      IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
      AuthenticationType = User.Identity?.AuthenticationType,
      NameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
      Name = User.FindFirst(ClaimTypes.Name)?.Value,
      Email = User.FindFirst(ClaimTypes.Email)?.Value,
      Role = User.FindFirst(ClaimTypes.Role)?.Value,
      AllClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
    });
  }
}
