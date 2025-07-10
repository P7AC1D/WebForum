using Microsoft.AspNetCore.Mvc;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Controllers;

/// <summary>
/// Users controller for managing user profiles and user-related data
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
  private readonly IUserService _userService;
  private readonly ILogger<UsersController> _logger;

  public UsersController(IUserService userService, ILogger<UsersController> logger)
  {
    _userService = userService;
    _logger = logger;
  }

  /// <summary>
  /// Get user profile by ID
  /// </summary>
  /// <param name="id">User ID</param>
  /// <returns>User profile information</returns>
  /// <response code="200">User profile retrieved successfully</response>
  /// <response code="404">User not found</response>
  /// <response code="400">Invalid user ID</response>
  /// <response code="500">Internal server error</response>
  [HttpGet("{id:int}")]
  [ProducesResponseType(typeof(UserInfo), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> GetUser(int id)
  {
    try
    {
      _logger.LogInformation("Getting user profile for ID: {UserId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid user ID provided: {UserId}", id);
        return NotFound($"User with ID {id} not found");
      }

      var userProfile = await _userService.GetUserProfileAsync(id);

      _logger.LogInformation("User profile retrieved successfully for ID: {UserId}", id);
      return Ok(userProfile);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("User not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving user profile for ID: {UserId}", id);
      return StatusCode(500, "An error occurred while retrieving the user profile");
    }
  }

  /// <summary>
  /// Get posts created by a specific user with pagination and sorting
  /// </summary>
  /// <param name="id">User ID whose posts to retrieve</param>
  /// <param name="page">Page number for pagination (minimum: 1, default: 1)</param>
  /// <param name="pageSize">Number of posts per page (range: 1-50, default: 10)</param>
  /// <param name="sortOrder">Sort order for posts by creation date ('asc' for oldest first, 'desc' for newest first, default: 'desc')</param>
  /// <returns>Paginated list of user's posts with metadata including like counts and comment counts</returns>
  /// <response code="200">User posts retrieved successfully with pagination metadata</response>
  /// <response code="404">User not found</response>
  /// <response code="400">Invalid user ID or query parameters (invalid page, pageSize, or sortOrder)</response>
  /// <response code="500">Internal server error during posts retrieval</response>
  [HttpGet("{id:int}/posts")]
  [ProducesResponseType(typeof(PagedResult<Post>), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> GetUserPosts(
      int id,
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 10,
      [FromQuery] string sortOrder = "desc")
  {
    try
    {
      _logger.LogInformation("Getting posts for user ID: {UserId}, page: {Page}, pageSize: {PageSize}, sortOrder: {SortOrder}",
          id, page, pageSize, sortOrder);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid user ID provided: {UserId}", id);
        return NotFound($"User with ID {id} not found");
      }

      // Validate pagination parameters
      if (page < 1)
      {
        _logger.LogWarning("Invalid page number: {Page}", page);
        return BadRequest("Page number must be 1 or greater");
      }

      if (pageSize < 1 || pageSize > 50)
      {
        _logger.LogWarning("Invalid page size: {PageSize}", pageSize);
        return BadRequest("Page size must be between 1 and 50");
      }

      // Validate sort order
      var validSortOrders = new[] { "asc", "desc", "oldest", "newest" };
      if (!validSortOrders.Contains(sortOrder.ToLower()))
      {
        _logger.LogWarning("Invalid sort order: {SortOrder}", sortOrder);
        return BadRequest("Sort order must be 'asc', 'desc', 'oldest', or 'newest'");
      }

      var result = await _userService.GetUserPostsAsync(id, page, pageSize, sortOrder);

      _logger.LogInformation("Retrieved {PostCount} posts for user ID: {UserId}", result.Items.Count, id);
      return Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("User not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving posts for user ID: {UserId}", id);
      return StatusCode(500, "An error occurred while retrieving user posts");
    }
  }
}
