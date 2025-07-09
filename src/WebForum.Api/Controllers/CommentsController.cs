using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Controllers;

/// <summary>
/// Comments controller for managing individual comments
/// </summary>
[ApiController]
[Route("api/comments")]
[Produces("application/json")]
public class CommentsController : ControllerBase
{
  private readonly ICommentService _commentService;
  private readonly ILogger<CommentsController> _logger;

  public CommentsController(ICommentService commentService, ILogger<CommentsController> logger)
  {
    _commentService = commentService;
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
  public async Task<IActionResult> GetComment(int id)
  {
    try
    {
      _logger.LogInformation("Getting comment with ID: {CommentId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid comment ID: {CommentId}", id);
        return BadRequest("Comment ID must be greater than zero");
      }

      var comment = await _commentService.GetCommentByIdAsync(id);

      _logger.LogInformation("Comment retrieved successfully: {CommentId}", id);
      return Ok(comment);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Comment not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving comment with ID: {CommentId}", id);
      return StatusCode(500, "An error occurred while retrieving the comment");
    }
  }
}
