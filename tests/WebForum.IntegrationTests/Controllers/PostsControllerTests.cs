using FluentAssertions;
using System.Net;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Utilities;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for PostsController API endpoints
/// Focuses on API contracts, validation, edge cases, and technical scenarios
/// </summary>
public class PostsControllerTests : IntegrationTestBase
{
  public PostsControllerTests(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task GetPosts_WithDefaultParameters_ShouldReturnPagedResults()
  {
    // Arrange
    await SeedTestDataAsync(userCount: 2, postCount: 10);

    // Act
    var response = await Client.GetAsync("/api/posts");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
    result.Should().NotBeNull();
    result.Items.Should().HaveCountLessOrEqualTo(10); // Default page size
    result.TotalCount.Should().Be(10);
    result.Page.Should().Be(1);
    result.PageSize.Should().BeGreaterThan(0);
  }

  [Fact]
  public async Task GetPosts_WithCustomPagination_ShouldRespectParameters()
  {
    // Arrange
    await SeedTestDataAsync(userCount: 2, postCount: 25);

    // Act
    var response = await Client.GetAsync("/api/posts?page=2&pageSize=5");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
    result.Page.Should().Be(2);
    result.PageSize.Should().Be(5);
    result.Items.Should().HaveCount(5);
    result.TotalCount.Should().Be(25);
    result.TotalPages.Should().Be(5);
  }

  [Fact]
  public async Task GetPosts_WithInvalidPagination_ShouldHandleGracefully()
  {
    // Arrange
    var testCases = new[]
    {
            "/api/posts?page=0&pageSize=10",        // Invalid page (too low)
            "/api/posts?page=-1&pageSize=10",       // Negative page
            "/api/posts?page=1&pageSize=0",         // Invalid page size
            "/api/posts?page=1&pageSize=-5",        // Negative page size
            "/api/posts?page=1&pageSize=1000"       // Page size too large
        };

    foreach (var testCase in testCases)
    {
      // Act
      var response = await Client.GetAsync(testCase);

      // Assert
      response.StatusCode.Should().BeOneOf(
          HttpStatusCode.BadRequest,  // If validation rejects the request
          HttpStatusCode.OK          // If API corrects invalid values
      );

      if (response.StatusCode == HttpStatusCode.OK)
      {
        var result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
        result.Page.Should().BeGreaterThan(0);
        result.PageSize.Should().BeGreaterThan(0);
      }
    }
  }

  [Fact]
  public async Task GetPost_WithValidId_ShouldReturnPost()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var post = await CreateTestPostAsync(user.Id, "Test Post", "Test content");

    // Act
    var response = await Client.GetAsync($"/api/posts/{post.Id}");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var returnedPost = await HttpUtilities.ReadAsAsync<PostResponse>(response);
    returnedPost.Should().NotBeNull();
    returnedPost.Id.Should().Be(post.Id);
    returnedPost.Title.Should().Be(post.Title);
    returnedPost.Content.Should().Be(post.Content);
    returnedPost.AuthorId.Should().Be(post.AuthorId);
  }

  [Fact]
  public async Task GetPost_WithInvalidId_ShouldReturnNotFound()
  {
    // Arrange
    var invalidIds = new[] { 0, -1, 999999, int.MaxValue };

    foreach (var invalidId in invalidIds)
    {
      // Act
      var response = await Client.GetAsync($"/api/posts/{invalidId}");

      // Assert
      response.StatusCode.Should().Be(HttpStatusCode.NotFound,
          $"Post ID {invalidId} should return NotFound");
    }
  }

  [Fact]
  public async Task CreatePost_WithValidData_ShouldCreatePost()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    var createRequest = new CreatePostRequest
    {
      Title = "Integration Test Post",
      Content = "This is content for the integration test post with sufficient length."
    };

    // Act
    var response = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var createdPost = await HttpUtilities.ReadAsAsync<PostResponse>(response);
    createdPost.Should().NotBeNull();
    createdPost.Id.Should().BeGreaterThan(0);
    createdPost.Title.Should().Be(createRequest.Title);
    createdPost.Content.Should().Be(createRequest.Content);
    createdPost.AuthorId.Should().Be(user.Id);
    createdPost.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

    // Verify post can be retrieved
    var getResponse = await Client.GetAsync($"/api/posts/{createdPost.Id}");
    getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task CreatePost_WithoutAuthentication_ShouldReturnUnauthorized()
  {
    // Arrange
    var createRequest = new CreatePostRequest
    {
      Title = "Unauthorized Post",
      Content = "This post should not be created without authentication."
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/posts", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Theory]
  [InlineData("", "Valid content with sufficient length")]  // Empty title
  [InlineData("Hi", "Valid content with sufficient length")] // Title too short
  [InlineData("Valid Title", "")] // Empty content  
  [InlineData("Valid Title", "Short")] // Content too short
  public async Task CreatePost_WithInvalidData_ShouldReturnBadRequest(string title, string content)
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    var createRequest = new CreatePostRequest
    {
      Title = title,
      Content = content
    };

    // Act
    var response = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task CreatePost_WithMaxLengthData_ShouldSucceed()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    var createRequest = new CreatePostRequest
    {
      Title = new string('T', 200), // Max title length
      Content = new string('C', 10000) // Max content length
    };

    // Act
    var response = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var createdPost = await HttpUtilities.ReadAsAsync<PostResponse>(response);
    createdPost.Title.Should().HaveLength(200);
    createdPost.Content.Should().HaveLength(10000);
  }

  [Fact]
  public async Task CreatePost_WithOverMaxLengthData_ShouldReturnBadRequest()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    var createRequest = new CreatePostRequest
    {
      Title = new string('T', 201), // Over max title length
      Content = new string('C', 10001) // Over max content length
    };

    // Act
    var response = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task LikePost_ShouldToggleLikeStatus()
  {
    // Arrange
    var author = await CreateTestUserAsync("author", "author@example.com");
    var liker = await CreateTestUserAsync("liker", "liker@example.com");
    var post = await CreateTestPostAsync(author.Id);

    var likerClient = CreateAuthenticatedClient(liker.Id, liker.Username);

    // Act & Assert - First like
    var firstLikeResponse = await likerClient.PostAsync($"/api/posts/{post.Id}/like", null);
    firstLikeResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

    var postAfterLike = await HttpUtilities.GetAsync<PostResponse>(Client, $"/api/posts/{post.Id}");
    postAfterLike.LikeCount.Should().Be(1);

    // Act & Assert - Second like (unlike)
    var secondLikeResponse = await likerClient.PostAsync($"/api/posts/{post.Id}/like", null);
    secondLikeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var postAfterUnlike = await HttpUtilities.GetAsync<PostResponse>(Client, $"/api/posts/{post.Id}");
    postAfterUnlike.LikeCount.Should().Be(0);
  }

  [Fact]
  public async Task LikePost_WithInvalidPostId_ShouldReturnNotFound()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    // Act
    var response = await authenticatedClient.PostAsync("/api/posts/999999/like", null);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task LikePost_WithoutAuthentication_ShouldReturnUnauthorized()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var post = await CreateTestPostAsync(user.Id);

    // Act
    var response = await Client.PostAsync($"/api/posts/{post.Id}/like", null);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetPostComments_ShouldReturnCommentsWithPagination()
  {
    // Arrange
    var author = await CreateTestUserAsync("author", "author@example.com");
    var post = await CreateTestPostAsync(author.Id);

    // Create multiple comments
    var commenters = await Task.WhenAll(Enumerable.Range(1, 5).Select(i =>
        CreateTestUserAsync($"commenter{i}", $"commenter{i}@example.com")));

    foreach (var commenter in commenters)
    {
      await CreateTestCommentAsync(post.Id, commenter.Id, $"Comment from {commenter.Username}");
    }

    // Act
    var response = await Client.GetAsync($"/api/posts/{post.Id}/comments?page=1&pageSize=3");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var comments = await HttpUtilities.ReadAsAsync<PagedResult<CommentResponse>>(response);
    comments.Should().NotBeNull();
    comments.Items.Should().HaveCount(3);
    comments.TotalCount.Should().Be(5);
    comments.Page.Should().Be(1);
    comments.Items.Should().AllSatisfy(comment => comment.PostId.Should().Be(post.Id));
  }

  [Fact]
  public async Task CreateComment_WithValidData_ShouldCreateComment()
  {
    // Arrange
    var author = await CreateTestUserAsync("author", "author@example.com");
    var commenter = await CreateTestUserAsync("commenter", "commenter@example.com");
    var post = await CreateTestPostAsync(author.Id);

    var commenterClient = CreateAuthenticatedClient(commenter.Id, commenter.Username);
    var commentRequest = new CreateCommentRequest
    {
      Content = "This is a valid comment with sufficient content length."
    };

    // Act
    var response = await HttpUtilities.PostAsync(commenterClient, $"/api/posts/{post.Id}/comments", commentRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var createdComment = await HttpUtilities.ReadAsAsync<CommentResponse>(response);
    createdComment.Should().NotBeNull();
    createdComment.Id.Should().BeGreaterThan(0);
    createdComment.Content.Should().Be(commentRequest.Content);
    createdComment.AuthorId.Should().Be(commenter.Id);
    createdComment.PostId.Should().Be(post.Id);
    createdComment.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
  }

  [Fact]
  public async Task GetPosts_WithAuthorFilter_ShouldReturnAuthorPosts()
  {
    // Arrange
    var author1 = await CreateTestUserAsync("author1", "author1@example.com");
    var author2 = await CreateTestUserAsync("author2", "author2@example.com");

    await CreateTestPostAsync(author1.Id, "Post by Author 1", "Content 1");
    await CreateTestPostAsync(author1.Id, "Another Post by Author 1", "Content 2");
    await CreateTestPostAsync(author2.Id, "Post by Author 2", "Content 3");

    // Act
    var response = await Client.GetAsync($"/api/posts?authorId={author1.Id}");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
    result.Items.Should().HaveCount(2);
    result.Items.Should().AllSatisfy(post => post.AuthorId.Should().Be(author1.Id));
    result.TotalCount.Should().Be(2);
  }

  [Fact]
  public async Task GetPosts_WithDateFilter_ShouldFilterByDateRange()
  {
    // Arrange
    var user = await CreateTestUserAsync();

    // Create posts (these will have current timestamp)
    await CreateTestPostAsync(user.Id, "Recent Post 1", "Content 1");
    await CreateTestPostAsync(user.Id, "Recent Post 2", "Content 2");

    var dateFrom = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
    var dateTo = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ");

    // Act
    var response = await Client.GetAsync($"/api/posts?dateFrom={dateFrom}&dateTo={dateTo}");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
    result.Items.Should().HaveCount(2);
    result.Items.Should().AllSatisfy(post =>
    {
      post.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(-2));
      post.CreatedAt.Should().BeBefore(DateTimeOffset.UtcNow.AddDays(2));
    });
  }

  [Fact]
  public async Task GetPosts_WithSorting_ShouldSortCorrectly()
  {
    // Arrange
    var user = await CreateTestUserAsync();

    // Create posts with known titles for sorting
    await CreateTestPostAsync(user.Id, "Alpha Post", "Content A");
    await CreateTestPostAsync(user.Id, "Beta Post", "Content B");
    await CreateTestPostAsync(user.Id, "Charlie Post", "Content C");

    // Act - Sort by title ascending
    var ascResponse = await Client.GetAsync("/api/posts?sortBy=title&sortOrder=asc");
    var descResponse = await Client.GetAsync("/api/posts?sortBy=title&sortOrder=desc");

    // Assert
    ascResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    descResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var ascResult = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(ascResponse);
    var descResult = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(descResponse);

    var ascTitles = ascResult.Items.Select(p => p.Title).ToList();
    var descTitles = descResult.Items.Select(p => p.Title).ToList();

    ascTitles.Should().BeInAscendingOrder();
    descTitles.Should().BeInDescendingOrder();
    descTitles.Should().BeEquivalentTo(ascTitles.AsEnumerable().Reverse());
  }
}
