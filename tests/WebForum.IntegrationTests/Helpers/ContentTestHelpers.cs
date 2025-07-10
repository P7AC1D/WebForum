using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Helpers;

/// <summary>
/// Helper utilities for content creation and management testing operations
/// </summary>
public static class ContentTestHelpers
{
  /// <summary>
  /// Creates a test post with the specified content
  /// </summary>
  public static async Task<PostResponse> CreateTestPostAsync(
      HttpClient authenticatedClient,
      string? title = null,
      string? content = null)
  {
    var uniqueId = Guid.NewGuid().ToString("N")[..8];
    var request = new CreatePostRequest
    {
      Title = title ?? $"Test Post {uniqueId}",
      Content = content ?? $"Test content for post {uniqueId}"
    };

    var response = await authenticatedClient.PostAsJsonAsync("/api/posts", request);
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var post = await response.Content.ReadFromJsonAsync<PostResponse>();
    post.Should().NotBeNull();

    return post!;
  }

  /// <summary>
  /// Creates multiple test posts with unique content
  /// </summary>
  public static async Task<List<PostResponse>> CreateMultipleTestPostsAsync(
      HttpClient authenticatedClient,
      int count,
      string? titlePrefix = null,
      string? contentPrefix = null)
  {
    var posts = new List<PostResponse>();

    for (int i = 0; i < count; i++)
    {
      var uniqueId = Guid.NewGuid().ToString("N")[..8];
      var post = await CreateTestPostAsync(
          authenticatedClient,
          title: $"{titlePrefix ?? "Test Post"} {i + 1} {uniqueId}",
          content: $"{contentPrefix ?? "Test content"} for post {i + 1} {uniqueId}"
      );
      posts.Add(post);
    }

    return posts;
  }

  /// <summary>
  /// Creates a test comment on the specified post
  /// </summary>
  public static async Task<CommentResponse> CreateTestCommentAsync(
      HttpClient authenticatedClient,
      int postId,
      string? content = null)
  {
    var uniqueId = Guid.NewGuid().ToString("N")[..8];
    var request = new CreateCommentRequest
    {
      Content = content ?? $"Test comment {uniqueId}"
    };

    var response = await authenticatedClient.PostAsJsonAsync($"/api/posts/{postId}/comments", request);
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
    comment.Should().NotBeNull();

    return comment!;
  }

  /// <summary>
  /// Creates multiple test comments on the specified post
  /// </summary>
  public static async Task<List<CommentResponse>> CreateMultipleTestCommentsAsync(
      HttpClient authenticatedClient,
      int postId,
      int count,
      string? contentPrefix = null)
  {
    var comments = new List<CommentResponse>();

    for (int i = 0; i < count; i++)
    {
      var uniqueId = Guid.NewGuid().ToString("N")[..8];
      var comment = await CreateTestCommentAsync(
          authenticatedClient,
          postId,
          content: $"{contentPrefix ?? "Test comment"} {i + 1} {uniqueId}"
      );
      comments.Add(comment);
    }

    return comments;
  }

  /// <summary>
  /// Likes a post and returns the like response
  /// </summary>
  public static async Task<WebForum.Api.Models.Response.LikeResponse> LikePostAsync(
      HttpClient authenticatedClient,
      int postId)
  {
    var response = await authenticatedClient.PostAsync($"/api/posts/{postId}/like", null);
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var likeResponse = await response.Content.ReadFromJsonAsync<WebForum.Api.Models.Response.LikeResponse>();
    likeResponse.Should().NotBeNull();

    return likeResponse!;
  }

  /// <summary>
  /// Unlikes a post and returns the like response
  /// </summary>
  public static async Task<WebForum.Api.Models.Response.LikeResponse> UnlikePostAsync(
      HttpClient authenticatedClient,
      int postId)
  {
    var response = await authenticatedClient.DeleteAsync($"/api/posts/{postId}/like");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var likeResponse = await response.Content.ReadFromJsonAsync<WebForum.Api.Models.Response.LikeResponse>();
    likeResponse.Should().NotBeNull();

    return likeResponse!;
  }

  /// <summary>
  /// Retrieves posts with pagination
  /// </summary>
  public static async Task<PagedResult<PostResponse>> GetPostsAsync(
      HttpClient client,
      int page = 1,
      int pageSize = 10,
      string? search = null,
      string? sortBy = null,
      string? sortDirection = null,
      string? author = null,
      string? tag = null)
  {
    var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

    if (!string.IsNullOrEmpty(search))
      queryParams.Add($"search={Uri.EscapeDataString(search)}");

    if (!string.IsNullOrEmpty(sortBy))
      queryParams.Add($"sortBy={sortBy}");

    if (!string.IsNullOrEmpty(sortDirection))
      queryParams.Add($"sortDirection={sortDirection}");

    if (!string.IsNullOrEmpty(author))
      queryParams.Add($"author={Uri.EscapeDataString(author)}");

    if (!string.IsNullOrEmpty(tag))
      queryParams.Add($"tag={Uri.EscapeDataString(tag)}");

    var queryString = string.Join("&", queryParams);
    var response = await client.GetAsync($"/api/posts?{queryString}");

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var posts = await response.Content.ReadFromJsonAsync<PagedResult<PostResponse>>();
    posts.Should().NotBeNull();

    return posts!;
  }

  /// <summary>
  /// Retrieves a specific post by ID
  /// </summary>
  public static async Task<PostResponse> GetPostByIdAsync(
      HttpClient client,
      int postId)
  {
    var response = await client.GetAsync($"/api/posts/{postId}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var post = await response.Content.ReadFromJsonAsync<PostResponse>();
    post.Should().NotBeNull();

    return post!;
  }

  /// <summary>
  /// Retrieves comments for a specific post
  /// </summary>
  public static async Task<PagedResult<CommentResponse>> GetCommentsForPostAsync(
      HttpClient client,
      int postId,
      int page = 1,
      int pageSize = 10)
  {
    var response = await client.GetAsync($"/api/posts/{postId}/comments?page={page}&pageSize={pageSize}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var comments = await response.Content.ReadFromJsonAsync<PagedResult<CommentResponse>>();
    comments.Should().NotBeNull();

    return comments!;
  }

  /// <summary>
  /// Validates that a post response contains expected data
  /// </summary>
  public static void ValidatePostResponse(
      PostResponse post,
      string expectedTitle,
      string expectedContent,
      int expectedAuthorId,
      string expectedAuthorUsername)
  {
    post.Should().NotBeNull();
    post.Id.Should().BeGreaterThan(0);
    post.Title.Should().Be(expectedTitle);
    post.Content.Should().Be(expectedContent);
    post.AuthorId.Should().Be(expectedAuthorId);
    post.AuthorUsername.Should().Be(expectedAuthorUsername);
    post.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    post.LikeCount.Should().BeGreaterOrEqualTo(0);
    post.CommentCount.Should().BeGreaterOrEqualTo(0);
  }

  /// <summary>
  /// Validates that a comment response contains expected data
  /// </summary>
  public static void ValidateCommentResponse(
      CommentResponse comment,
      string expectedContent,
      int expectedPostId,
      int expectedAuthorId,
      string expectedAuthorUsername)
  {
    comment.Should().NotBeNull();
    comment.Id.Should().BeGreaterThan(0);
    comment.Content.Should().Be(expectedContent);
    comment.PostId.Should().Be(expectedPostId);
    comment.AuthorId.Should().Be(expectedAuthorId);
    comment.AuthorUsername.Should().Be(expectedAuthorUsername);
    comment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
  }

  /// <summary>
  /// Validates pagination metadata
  /// </summary>
  public static void ValidatePaginationResponse<T>(
      PagedResult<T> pagedResult,
      int expectedPage,
      int expectedPageSize,
      int? expectedTotalItems = null,
      int? expectedTotalPages = null)
  {
    pagedResult.Should().NotBeNull();
    pagedResult.Page.Should().Be(expectedPage);
    pagedResult.PageSize.Should().Be(expectedPageSize);
    pagedResult.Items.Should().NotBeNull();
    pagedResult.Items.Count().Should().BeLessOrEqualTo(expectedPageSize);

    if (expectedTotalItems.HasValue)
      pagedResult.TotalCount.Should().Be(expectedTotalItems.Value);

    if (expectedTotalPages.HasValue)
      pagedResult.TotalPages.Should().Be(expectedTotalPages.Value);
  }

  /// <summary>
  /// Creates a valid post creation request
  /// </summary>
  public static CreatePostRequest CreateValidPostRequest(
      string? title = null,
      string? content = null)
  {
    var uniqueId = Guid.NewGuid().ToString("N")[..8];
    return new CreatePostRequest
    {
      Title = title ?? $"Test Post {uniqueId}",
      Content = content ?? $"Test content {uniqueId}"
    };
  }

  /// <summary>
  /// Creates a valid comment creation request
  /// </summary>
  public static CreateCommentRequest CreateValidCommentRequest(
      string? content = null)
  {
    var uniqueId = Guid.NewGuid().ToString("N")[..8];
    return new CreateCommentRequest
    {
      Content = content ?? $"Test comment {uniqueId}"
    };
  }
}
