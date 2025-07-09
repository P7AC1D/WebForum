using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Controllers;

/// <summary>
/// Posts controller for managing forum posts with filtering, sorting, and pagination
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PostsController : ControllerBase
{
  private readonly IPostService _postService;
  private readonly ICommentService _commentService;
  private readonly ILikeService _likeService;
  private readonly ILogger<PostsController> _logger;

  public PostsController(
      IPostService postService,
      ICommentService commentService,
      ILikeService likeService,
      ILogger<PostsController> logger)
  {
    _postService = postService;
    _commentService = commentService;
    _likeService = likeService;
    _logger = logger;
  }

  /// <summary>
  /// Get posts with comprehensive filtering, sorting, and pagination support
  /// </summary>
  /// <param name="page">Page number for pagination (minimum: 1, default: 1)</param>
  /// <param name="pageSize">Number of posts per page (range: 1-50, default: 10)</param>
  /// <param name="authorId">Filter posts by specific author ID (optional)</param>
  /// <param name="dateFrom">Filter posts created from this date onwards (ISO 8601 format, optional)</param>
  /// <param name="dateTo">Filter posts created until this date (ISO 8601 format, optional)</param>
  /// <param name="tags">Filter posts by tags (comma-separated list, case-insensitive, optional)</param>
  /// <param name="sortBy">Sort field: 'date' for creation date or 'likeCount' for popularity (default: 'date')</param>
  /// <param name="sortOrder">Sort order: 'asc' for ascending or 'desc' for descending (default: 'desc')</param>
  /// <returns>Paginated list of posts with like counts and author information</returns>
  /// <response code="200">Posts retrieved successfully with pagination metadata</response>
  /// <response code="400">Invalid query parameters (invalid page, pageSize, dates, sortBy, or sortOrder)</response>
  /// <response code="500">Internal server error during posts retrieval</response>
  [HttpGet]
  [ProducesResponseType(typeof(PagedResult<Post>), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> GetPosts(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 10,
      [FromQuery] int? authorId = null,
      [FromQuery] DateTimeOffset? dateFrom = null,
      [FromQuery] DateTimeOffset? dateTo = null,
      [FromQuery] string? tags = null,
      [FromQuery] string sortBy = "date",
      [FromQuery] string sortOrder = "desc")
  {
    try
    {
      _logger.LogInformation("Getting posts with filters - page: {Page}, pageSize: {PageSize}, authorId: {AuthorId}, sortBy: {SortBy}, sortOrder: {SortOrder}",
          page, pageSize, authorId, sortBy, sortOrder);

      // Validate pagination parameters
      var validationErrors = PagedResult<Post>.ValidatePaginationParameters(page, pageSize, 50);
      if (validationErrors.Any())
      {
        _logger.LogWarning("Pagination validation failed: {Errors}", string.Join(", ", validationErrors));
        return BadRequest(new { Errors = validationErrors });
      }

      // Validate date range
      if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
      {
        _logger.LogWarning("Invalid date range: dateFrom {DateFrom} is after dateTo {DateTo}", dateFrom, dateTo);
        return BadRequest("DateFrom must be before or equal to DateTo");
      }

      // Validate sortBy parameter
      var validSortBy = new[] { "date", "likecount" };
      if (!validSortBy.Contains(sortBy.ToLower()))
      {
        _logger.LogWarning("Invalid sortBy parameter: {SortBy}", sortBy);
        return BadRequest("SortBy must be 'date' or 'likeCount'");
      }

      // Validate sortOrder parameter
      var validSortOrders = new[] { "asc", "desc" };
      if (!validSortOrders.Contains(sortOrder.ToLower()))
      {
        _logger.LogWarning("Invalid sortOrder parameter: {SortOrder}", sortOrder);
        return BadRequest("SortOrder must be 'asc' or 'desc'");
      }

      var result = await _postService.GetPostsAsync(page, pageSize, authorId, dateFrom, dateTo, tags, sortBy, sortOrder);

      _logger.LogInformation("Retrieved {PostCount} posts successfully", result.Items.Count);
      return Ok(result);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving posts");
      return StatusCode(500, "An error occurred while retrieving posts");
    }
  }

  /// <summary>
  /// Get a specific post by ID with comments
  /// </summary>
  /// <param name="id">Post ID to retrieve</param>
  /// <returns>Post details with author information, like count, comment count, and comments</returns>
  /// <response code="200">Post found and returned with complete details</response>
  /// <response code="404">Post not found</response>
  /// <response code="400">Invalid post ID (must be positive integer)</response>
  /// <response code="500">Internal server error during post retrieval</response>
  [HttpGet("{id:int}")]
  [ProducesResponseType(typeof(Post), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> GetPost(int id)
  {
    try
    {
      _logger.LogInformation("Getting post with ID: {PostId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid post ID: {PostId}", id);
        return BadRequest("Post ID must be greater than zero");
      }

      var post = await _postService.GetPostByIdAsync(id);

      _logger.LogInformation("Post retrieved successfully: {PostId}", id);
      return Ok(post);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Post not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving post with ID: {PostId}", id);
      return StatusCode(500, "An error occurred while retrieving the post");
    }
  }

  /// <summary>
  /// Create a new forum post
  /// </summary>
  /// <param name="createPost">Post creation data including title and content</param>
  /// <returns>Created post information with generated ID and timestamps</returns>
  /// <response code="201">Post created successfully</response>
  /// <response code="400">Invalid post data (missing title, content, or validation failures)</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="500">Internal server error during post creation</response>
  [HttpPost]
  [Authorize]
  [ProducesResponseType(typeof(Post), 201)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> CreatePost([FromBody] CreatePost createPost)
  {
    try
    {
      _logger.LogInformation("Creating new post");

      if (createPost == null)
        return BadRequest("Post data is required");

      // Validate input data
      var validationErrors = createPost.Validate();
      if (validationErrors.Any())
      {
        _logger.LogWarning("Post validation failed: {Errors}", string.Join(", ", validationErrors));
        return BadRequest(new { Errors = validationErrors });
      }

      // Extract user ID from JWT claims
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
      {
        _logger.LogWarning("Could not extract user ID from JWT claims");
        return Unauthorized("Invalid authentication token");
      }

      var post = await _postService.CreatePostAsync(createPost, userId);

      _logger.LogInformation("Post created successfully with ID: {PostId}", post.Id);
      return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Post creation validation error: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating post");
      return StatusCode(500, "An error occurred while creating the post");
    }
  }

  /// <summary>
  /// Like or unlike a post
  /// </summary>
  /// <param name="id">Post ID to like</param>
  /// <returns>Like status and updated like count</returns>
  /// <response code="200">Post liked/unliked successfully</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="404">Post not found</response>
  /// <response code="400">Cannot like own post or invalid post ID</response>
  /// <response code="500">Internal server error</response>
  [HttpPost("{id:int}/like")]
  [Authorize]
  [ProducesResponseType(typeof(LikeResponse), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> LikePost(int id)
  {
    try
    {
      _logger.LogInformation("Processing like/unlike for post ID: {PostId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid post ID: {PostId}", id);
        return BadRequest("Post ID must be greater than zero");
      }

      // Extract user ID from JWT claims
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
      {
        _logger.LogWarning("Could not extract user ID from JWT claims");
        return Unauthorized("Invalid authentication token");
      }

      var result = await _likeService.ToggleLikeAsync(id, userId);

      _logger.LogInformation("Like/unlike processed successfully for post {PostId}, isLiked: {IsLiked}, likeCount: {LikeCount}",
          id, result.IsLiked, result.LikeCount);
      return Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Post not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning("Invalid like operation: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing like for post ID: {PostId}", id);
      return StatusCode(500, "An error occurred while processing the like");
    }
  }

  /// <summary>
  /// Remove like from a post
  /// </summary>
  /// <param name="id">Post ID to unlike</param>
  /// <returns>Updated like status and count</returns>
  /// <response code="200">Like removed successfully</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="404">Post not found or like not found</response>
  /// <response code="400">Invalid post ID</response>
  /// <response code="500">Internal server error</response>
  [HttpDelete("{id:int}/like")]
  [Authorize]
  [ProducesResponseType(typeof(LikeResponse), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> UnlikePost(int id)
  {
    try
    {
      _logger.LogInformation("Processing unlike for post ID: {PostId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid post ID: {PostId}", id);
        return BadRequest("Post ID must be greater than zero");
      }

      // Extract user ID from JWT claims
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
      {
        _logger.LogWarning("Could not extract user ID from JWT claims");
        return Unauthorized("Invalid authentication token");
      }

      var result = await _likeService.UnlikePostAsync(id, userId);

      _logger.LogInformation("Unlike processed successfully for post {PostId}, likeCount: {LikeCount}",
          id, result.LikeCount);
      return Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Post or like not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing unlike for post ID: {PostId}", id);
      return StatusCode(500, "An error occurred while processing the unlike");
    }
  }

  /// <summary>
  /// Get comments for a specific post with pagination
  /// </summary>
  /// <param name="id">Post ID whose comments to retrieve</param>
  /// <param name="page">Page number for pagination (minimum: 1, default: 1)</param>
  /// <param name="pageSize">Number of comments per page (range: 1-50, default: 10)</param>
  /// <param name="sortOrder">Sort order by creation date: 'asc' for chronological or 'desc' for reverse chronological (default: 'asc')</param>
  /// <returns>Paginated list of comments for the specified post</returns>
  /// <response code="200">Comments retrieved successfully with pagination metadata</response>
  /// <response code="404">Post not found</response>
  /// <response code="400">Invalid post ID or query parameters</response>
  /// <response code="500">Internal server error during comments retrieval</response>
  [HttpGet("{id:int}/comments")]
  [ProducesResponseType(typeof(PagedResult<Comment>), 200)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> GetPostComments(
      int id,
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 10,
      [FromQuery] string sortOrder = "asc")
  {
    try
    {
      _logger.LogInformation("Getting comments for post ID: {PostId}, page: {Page}, pageSize: {PageSize}, sortOrder: {SortOrder}",
          id, page, pageSize, sortOrder);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid post ID: {PostId}", id);
        return BadRequest("Post ID must be greater than zero");
      }

      // Validate pagination parameters
      var validationErrors = PagedResult<Comment>.ValidatePaginationParameters(page, pageSize, 50);
      if (validationErrors.Any())
      {
        _logger.LogWarning("Pagination validation failed: {Errors}", string.Join(", ", validationErrors));
        return BadRequest(new { Errors = validationErrors });
      }

      // Validate sort order
      var validSortOrders = new[] { "asc", "desc", "oldest", "newest" };
      if (!validSortOrders.Contains(sortOrder.ToLower()))
      {
        _logger.LogWarning("Invalid sort order: {SortOrder}", sortOrder);
        return BadRequest("Sort order must be 'asc', 'desc', 'oldest', or 'newest'");
      }

      var result = await _commentService.GetPostCommentsAsync(id, page, pageSize, sortOrder);

      _logger.LogInformation("Retrieved {CommentCount} comments for post ID: {PostId}", result.Items.Count, id);
      return Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Post not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Invalid argument: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving comments for post ID: {PostId}", id);
      return StatusCode(500, "An error occurred while retrieving comments");
    }
  }

  /// <summary>
  /// Add a comment to a specific post
  /// </summary>
  /// <param name="id">Post ID to comment on (must be positive integer)</param>
  /// <param name="createComment">Comment creation data including content</param>
  /// <returns>Created comment information with author details and timestamps</returns>
  /// <response code="201">Comment created successfully</response>
  /// <response code="400">Invalid post ID or comment data (validation failures)</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="404">Post not found</response>
  /// <response code="500">Internal server error during comment creation</response>
  [HttpPost("{id:int}/comments")]
  [Authorize]
  [ProducesResponseType(typeof(Comment), 201)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> CreateComment(
      int id,
      [FromBody] CreateComment createComment)
  {
    try
    {
      _logger.LogInformation("Creating comment for post ID: {PostId}", id);

      if (id <= 0)
      {
        _logger.LogWarning("Invalid post ID: {PostId}", id);
        return BadRequest("Post ID must be greater than zero");
      }

      if (createComment == null)
        return BadRequest("Comment data is required");

      // Validate input data
      var validationErrors = createComment.Validate();
      if (validationErrors.Any())
      {
        _logger.LogWarning("Comment validation failed: {Errors}", string.Join(", ", validationErrors));
        return BadRequest(new { Errors = validationErrors });
      }

      // Extract user ID from JWT claims
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
      {
        _logger.LogWarning("Could not extract user ID from JWT claims");
        return Unauthorized("Invalid authentication token");
      }

      var comment = await _commentService.CreateCommentAsync(id, createComment, userId);

      _logger.LogInformation("Comment created successfully with ID: {CommentId} for post {PostId}", comment.Id, id);
      return CreatedAtAction(nameof(GetPostComments), new { id = id }, comment);
    }
    catch (KeyNotFoundException ex)
    {
      _logger.LogWarning("Post not found: {Message}", ex.Message);
      return NotFound(ex.Message);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning("Comment creation validation error: {Message}", ex.Message);
      return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating comment for post ID: {PostId}", id);
      return StatusCode(500, "An error occurred while creating the comment");
    }
  }
}
