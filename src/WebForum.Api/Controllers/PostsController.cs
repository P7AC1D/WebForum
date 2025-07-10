using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Controllers;

/// <summary>
/// Posts controller for managing forum posts with filtering, sorting, and pagination
/// </summary>
[ApiController]
[Route("api/posts")]
[Produces("application/json")]
public class PostsController : ControllerBase
{
  private readonly IPostService _postService;
  private readonly ICommentService _commentService;
  private readonly ILikeService _likeService;
  private readonly IModerationService _moderationService;
  private readonly IUserService _userService;
  private readonly ILogger<PostsController> _logger;

  public PostsController(
      IPostService postService,
      ICommentService commentService,
      ILikeService likeService,
      IModerationService moderationService,
      IUserService userService,
      ILogger<PostsController> logger)
  {
    _postService = postService;
    _commentService = commentService;
    _likeService = likeService;
    _moderationService = moderationService;
    _userService = userService;
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
  [ProducesResponseType(typeof(PagedResult<PostResponse>), 200)]
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
      var validSortBy = new[] { "date", "likecount", "title" };
      if (!validSortBy.Contains(sortBy.ToLower()))
      {
        _logger.LogWarning("Invalid sortBy parameter: {SortBy}", sortBy);
        return BadRequest("SortBy must be 'date', 'likeCount', or 'title'");
      }

      // Validate sortOrder parameter
      var validSortOrders = new[] { "asc", "desc" };
      if (!validSortOrders.Contains(sortOrder.ToLower()))
      {
        _logger.LogWarning("Invalid sortOrder parameter: {SortOrder}", sortOrder);
        return BadRequest("SortOrder must be 'asc' or 'desc'");
      }

      var result = await _postService.GetPostsAsync(page, pageSize, authorId, dateFrom, dateTo, tags, sortBy, sortOrder);

      // Get usernames for all authors in batch for performance
      var authorIds = result.Items.Select(p => p.AuthorId).Distinct();
      var usernames = await _userService.GetUsernamesByIdsAsync(authorIds);

      // Convert to response models with computed properties
      var responseItems = new List<PostResponse>();
      foreach (var post in result.Items)
      {
        var commentCount = await _commentService.GetCommentCountForPostAsync(post.Id);
        var likeCount = await GetLikeCountForPostAsync(post.Id);
        var isTagged = await _moderationService.IsPostTaggedAsync(post.Id);
        var authorUsername = usernames.GetValueOrDefault(post.AuthorId, "Unknown User");

        responseItems.Add(PostResponse.FromPost(post, commentCount, likeCount, isTagged, authorUsername));
      }

      var responseResult = new PagedResult<PostResponse>
      {
        Items = responseItems,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalCount = result.TotalCount,
        TotalPages = result.TotalPages,
        HasPrevious = result.HasPrevious,
        HasNext = result.HasNext
      };

      _logger.LogInformation("Retrieved {PostCount} posts successfully", responseResult.Items.Count);
      return Ok(responseResult);
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
  [ProducesResponseType(typeof(PostResponse), 200)]
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
        return NotFound($"Post with ID {id} not found");
      }

      var post = await _postService.GetPostByIdAsync(id);

      // Convert to response model with computed properties
      var commentCount = await _commentService.GetCommentCountForPostAsync(post.Id);
      var likeCount = await GetLikeCountForPostAsync(post.Id);
      var isTagged = await _moderationService.IsPostTaggedAsync(post.Id);
      
      // Get author username
      var usernames = await _userService.GetUsernamesByIdsAsync(new[] { post.AuthorId });
      var authorUsername = usernames.GetValueOrDefault(post.AuthorId, "Unknown User");

      var response = PostResponse.FromPost(post, commentCount, likeCount, isTagged, authorUsername);

      _logger.LogInformation("Post retrieved successfully: {PostId}", id);
      return Ok(response);
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
  [ProducesResponseType(typeof(PostResponse), 201)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest createPost)
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

      // Get author username for the newly created post
      var usernames = await _userService.GetUsernamesByIdsAsync(new[] { userId });
      var authorUsername = usernames.GetValueOrDefault(userId, "Unknown User");

      // Convert to response model with computed properties (new post has 0 comments/likes and is not tagged)
      var response = PostResponse.FromPost(post, 0, 0, false, authorUsername);

      _logger.LogInformation("Post created successfully with ID: {PostId}", post.Id);
      return CreatedAtAction(nameof(GetPost), new { id = post.Id }, response);
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
  [ProducesResponseType(typeof(Models.Response.LikeResponse), 200)]
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
  [ProducesResponseType(typeof(PagedResult<CommentResponse>), 200)]
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

      // Get usernames for all comment authors in batch for performance
      var commentAuthorIds = result.Items.Select(c => c.AuthorId).Distinct();
      var usernames = await _userService.GetUsernamesByIdsAsync(commentAuthorIds);

      // Convert to response models with author usernames
      var responseItems = result.Items.Select(comment => 
      {
        var authorUsername = usernames.GetValueOrDefault(comment.AuthorId, "Unknown User");
        return CommentResponse.FromComment(comment, authorUsername);
      }).ToList();

      var responseResult = new PagedResult<CommentResponse>
      {
        Items = responseItems,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalCount = result.TotalCount,
        TotalPages = result.TotalPages,
        HasPrevious = result.HasPrevious,
        HasNext = result.HasNext
      };

      _logger.LogInformation("Retrieved {CommentCount} comments for post ID: {PostId}", responseResult.Items.Count, id);
      return Ok(responseResult);
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
  [ProducesResponseType(typeof(CommentResponse), 201)]
  [ProducesResponseType(typeof(ProblemDetails), 400)]
  [ProducesResponseType(typeof(ProblemDetails), 401)]
  [ProducesResponseType(typeof(ProblemDetails), 404)]
  [ProducesResponseType(typeof(ProblemDetails), 500)]
  public async Task<IActionResult> CreateComment(
      int id,
      [FromBody] CreateCommentRequest createComment)
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

      // Get author username for the newly created comment
      var usernames = await _userService.GetUsernamesByIdsAsync(new[] { userId });
      var authorUsername = usernames.GetValueOrDefault(userId, "Unknown User");

      // Convert to response model with author username
      var response = CommentResponse.FromComment(comment, authorUsername);

      _logger.LogInformation("Comment created successfully with ID: {CommentId} for post {PostId}", comment.Id, id);
      return CreatedAtAction(nameof(GetPostComments), new { id = id }, response);
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

  /// <summary>
  /// Helper method to get like count for a post
  /// </summary>
  /// <param name="postId">Post ID to get like count for</param>
  /// <returns>Number of likes for the post</returns>
  private async Task<int> GetLikeCountForPostAsync(int postId)
  {
    return await _likeService.GetLikeCountForPostAsync(postId);
  }

  /// <summary>
  /// Add a moderation tag to a post (Moderator only)
  /// </summary>
  /// <param name="id">Post ID to tag (must be positive integer)</param>
  /// <returns>Confirmation of tag application with post details</returns>
  /// <response code="200">Post tagged successfully</response>
  /// <response code="400">Invalid post ID or post already tagged</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="403">User is not a moderator</response>
  /// <response code="404">Post not found</response>
  /// <response code="500">Internal server error during tag application</response>
  [HttpPost("{id:int}/tags")]
  [Authorize(Roles = "Moderator")]
  [ProducesResponseType(typeof(Models.Response.ModerationResponse), 200)]
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
      return StatusCode(403, ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error tagging post with ID: {PostId}", id);
      return StatusCode(500, "An error occurred while tagging the post");
    }
  }

  /// <summary>
  /// Remove a moderation tag from a post (Moderator only)
  /// </summary>
  /// <param name="id">Post ID to remove tag from (must be positive integer)</param>
  /// <returns>Confirmation of tag removal with post details</returns>
  /// <response code="200">Tag removed successfully</response>
  /// <response code="400">Invalid post ID</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="403">User is not a moderator</response>
  /// <response code="404">Post not found or tag not found</response>
  /// <response code="500">Internal server error during tag removal</response>
  [HttpDelete("{id:int}/tags")]
  [Authorize(Roles = "Moderator")]
  [ProducesResponseType(typeof(Models.Response.ModerationResponse), 200)]
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
      return StatusCode(403, ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error removing tag from post with ID: {PostId}", id);
      return StatusCode(500, "An error occurred while removing the tag from the post");
    }
  }

  /// <summary>
  /// Get all posts that have been tagged for moderation (Moderator only)
  /// </summary>
  /// <param name="page">Page number for pagination (minimum: 1, default: 1)</param>
  /// <param name="pageSize">Number of posts per page (range: 1-50, default: 10)</param>
  /// <returns>Paginated list of tagged posts with moderation details</returns>
  /// <response code="200">Tagged posts retrieved successfully</response>
  /// <response code="400">Invalid pagination parameters</response>
  /// <response code="401">User not authenticated</response>
  /// <response code="403">User is not a moderator</response>
  /// <response code="500">Internal server error during retrieval</response>
  [HttpGet("tagged")]
  [Authorize(Roles = "Moderator")]
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
