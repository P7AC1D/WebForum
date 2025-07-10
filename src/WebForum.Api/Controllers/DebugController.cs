using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebForum.Api.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
  private readonly ILogger<DebugController> _logger;

  public DebugController(ILogger<DebugController> logger)
  {
    _logger = logger;
  }

  [HttpGet("claims")]
  public IActionResult GetClaims()
  {
    var claims = User.Claims.Select(c => new { Type = c.Type, Value = c.Value }).ToList();
    _logger.LogInformation("Claims from unauthenticated request: {ClaimsCount}", claims.Count);
    return Ok(new { IsAuthenticated = User.Identity?.IsAuthenticated, Claims = claims });
  }

  [HttpGet("auth-claims")]
  [Authorize]
  public IActionResult GetAuthenticatedClaims()
  {
    var claims = User.Claims.Select(c => new { Type = c.Type, Value = c.Value }).ToList();
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

    int userId = 0;
    var parseSuccess = userIdClaim != null && int.TryParse(userIdClaim.Value, out userId);

    _logger.LogInformation("Claims from authenticated request: {ClaimsCount}, UserIdClaim found: {UserIdFound}, Parse success: {ParseSuccess}",
        claims.Count, userIdClaim != null, parseSuccess);

    return Ok(new
    {
      IsAuthenticated = User.Identity?.IsAuthenticated,
      UserIdClaim = userIdClaim?.Value,
      ParsedUserId = parseSuccess ? (int?)userId : null,
      Claims = claims
    });
  }
}
