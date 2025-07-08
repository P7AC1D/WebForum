using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebForum.Api.Data;
using WebForum.Api.Models;

namespace WebForum.Api.Controllers;

/// <summary>
/// Comments controller for managing individual comments
/// </summary>
[ApiController]
[Route("api/comments")]
[Produces("application/json")]
public class CommentsController : ControllerBase
{
  private readonly ForumDbContext _context;
  private readonly ILogger<CommentsController> _logger;

  public CommentsController(ForumDbContext context, ILogger<CommentsController> logger)
  {
    _context = context;
    _logger = logger;
  }

  /// <summary>
  /// Get a specific comment by ID
  /// </summary>
  /// <param name="id">Comment ID to retrieve</param>
  /// <returns>Comment details with author information</returns>
  /// <response code="200">Comment found and returned</response>
  /// <response code="404">Comment not found</response>
  /// <response code="400">Invalid comment ID</response>
  /// <response code="500">Internal server error</response>
  [HttpGet("{id:int}")]
  [ProducesResponseType(typeof(Comment), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public Task<IActionResult> GetComment(int id)
  {
    // TODO: Implement get comment logic
    // - Validate comment ID (id > 0, return 400 if invalid)
    // - Find comment by ID using _context.Comments.FirstOrDefaultAsync(c => c.Id == id)
    // - Return 404 if comment not found
    // - Include author information from User table
    // - Return Comment entity with 200 status
    // - Handle exceptions and return 500 with ProblemDetails
    // - Log information for monitoring

    throw new NotImplementedException("Get comment logic not yet implemented");
  }
}
