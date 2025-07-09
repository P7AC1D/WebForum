using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Controllers;

/// <summary>
/// Moderation controller for moderator-specific actions like tagging posts
/// </summary>
[ApiController]
[Route("api/moderation")]
[Produces("application/json")]
[Authorize(Roles = "Moderator")]
public class ModerationController : ControllerBase
{
  private readonly IModerationService _moderationService;
  private readonly ILogger<ModerationController> _logger;

  public ModerationController(IModerationService moderationService, ILogger<ModerationController> logger)
  {
    _moderationService = moderationService;
    _logger = logger;
  }

  /// <summary>
  /// Tag a post as "misleading or false information" for regulatory reasons
  /// </summary>
  /// <param name="id">Post ID to tag (must be positive integer)</param>
  /// <returns>Confirmation of tag application with post details</returns>
  /// <response code="200">Post tagged successfully</response>
  /// <response code="400">Invalid post ID or post already tagged</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="403">User is not a moderator</response>
  /// <response code="404">Post not found</response>
  /// <response code="500">Internal server error during tag application</response>
  [HttpPost("posts/{id:int}/tag")]
  [ProducesResponseType(typeof(ModerationResponse), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 403)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> TagPost(int id)
  {
    try
    {
      _logger.LogInformation("Tagging post with ID: {PostId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid post ID: {PostId}", id);
        return BadRequest("Post ID must be greater than zero");
      }

      // Extract moderator user ID from JWT claims
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int moderatorId))
      {
        _logger.LogWarning("Could not extract moderator ID from JWT claims");
        return Unauthorized("Invalid authentication token");
      }

      var response = await _moderationService.TagPostAsync(id, moderatorId);

      _logger.LogInformation("Post {PostId} tagged successfully by moderator {ModeratorId}", id, moderatorId);
      return Ok(response);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Post not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning("Invalid operation: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
      _logger.LogWarning("Unauthorized access: {Message}", ex.Message);
      return Forbid(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error tagging post with ID: {PostId}", id);
      return StatusCode(500, "An error occurred while tagging the post");
    }
  }

  /// <summary>
  /// Remove "misleading or false information" tag from a post
  /// </summary>
  /// <param name="id">Post ID to remove tag from (must be positive integer)</param>
  /// <returns>Confirmation of tag removal with post details</returns>
  /// <response code="200">Tag removed successfully</response>
  /// <response code="400">Invalid post ID</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="403">User is not a moderator</response>
  /// <response code="404">Post not found or tag not found</response>
  /// <response code="500">Internal server error during tag removal</response>
  [HttpDelete("posts/{id:int}/tag")]
  [ProducesResponseType(typeof(ModerationResponse), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 403)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> RemoveTagFromPost(int id)
  {
    try
    {
      _logger.LogInformation("Removing tag from post with ID: {PostId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid post ID: {PostId}", id);
        return BadRequest("Post ID must be greater than zero");
      }

      // Extract moderator user ID from JWT claims
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int moderatorId))
      {
        _logger.LogWarning("Could not extract moderator ID from JWT claims");
        return Unauthorized("Invalid authentication token");
      }

      var response = await _moderationService.RemoveTagFromPostAsync(id, moderatorId);

      _logger.LogInformation("Tag removed from post {PostId} by moderator {ModeratorId}", id, moderatorId);
      return Ok(response);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Post or tag not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
      _logger.LogWarning("Unauthorized access: {Message}", ex.Message);
      return Forbid(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error removing tag from post with ID: {PostId}", id);
      return StatusCode(500, "An error occurred while removing the tag from the post");
    }
  }

  /// <summary>
  /// Get all posts that have been tagged as "misleading or false information"
  /// </summary>
  /// <param name="page">Page number for pagination (minimum: 1, default: 1)</param>
  /// <param name="pageSize">Number of posts per page (range: 1-50, default: 10)</param>
  /// <returns>Paginated list of tagged posts with moderation details</returns>
  /// <response code="200">Tagged posts retrieved successfully</response>
  /// <response code="400">Invalid pagination parameters</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="403">User is not a moderator</response>
  /// <response code="500">Internal server error during retrieval</response>
  [HttpGet("posts/tagged")]
  [ProducesResponseType(typeof(PagedResult<TaggedPost>), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 403)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> GetTaggedPosts(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 10)
  {
    try
    {
      _logger.LogInformation("Getting tagged posts, page: {Page}, pageSize: {PageSize}", page, pageSize);

      // Validate pagination parameters
      var validationErrors = PagedResult<TaggedPost>.ValidatePaginationParameters(page, pageSize, 50);
      if (validationErrors.Any())
      {
        _logger.LogWarning("Pagination validation failed: {Errors}", string.Join(", ", validationErrors));
        return BadRequest(new { Errors = validationErrors });
      }

      var result = await _moderationService.GetTaggedPostsAsync(page, pageSize);

      _logger.LogInformation("Retrieved {Count} tagged posts (page {Page} of {TotalPages})",
          result.Items.Count(), page, result.TotalPages);
      return Ok(result);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving tagged posts");
      return StatusCode(500, "An error occurred while retrieving tagged posts");
    }
  }
}
