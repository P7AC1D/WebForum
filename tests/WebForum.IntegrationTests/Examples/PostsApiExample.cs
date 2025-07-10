using FluentAssertions;
using System.Net;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Utilities;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Examples;

/// <summary>
/// Example integration tests for the Posts API demonstrating how to use the test framework
/// </summary>
public class PostsApiExample : IntegrationTestBase
{
  public PostsApiExample(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task GetPosts_ShouldReturnPaginatedResults()
  {
    // Arrange
    await InitializeTestAsync();
    await TestDataHelper.SeedDatabaseAsync(Factory, userCount: 3, postCount: 5);

    // Act
    var response = await Client.GetAsync("/api/posts?page=1&pageSize=3");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var pagedResult = await HttpUtilities.ReadAsAsync<PagedPostResponse>(response);
    pagedResult.Should().NotBeNull();
    pagedResult.Items.Should().HaveCountLessOrEqualTo(3);
    pagedResult.TotalCount.Should().Be(5);
    pagedResult.CurrentPage.Should().Be(1);
  }

  [Fact]
  public async Task CreatePost_WithValidData_ShouldCreatePost()
  {
    // Arrange
    await InitializeTestAsync();
    var testUser = await TestDataHelper.CreateTestUserAsync(GetDbContext());
    var authenticatedClient = CreateAuthenticatedClient(testUser.Id, testUser.Username);

    var createRequest = new CreatePostRequest
    {
      Title = "Test Post Title",
      Content = "This is a test post content for integration testing."
    };

    // Act
    var response = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var createdPost = await HttpUtilities.ReadAsAsync<PostResponse>(response);
    createdPost.Should().NotBeNull();
    createdPost.Title.Should().Be(createRequest.Title);
    createdPost.Content.Should().Be(createRequest.Content);
    createdPost.AuthorId.Should().Be(testUser.Id);
  }

  [Fact]
  public async Task CreatePost_WithoutAuthentication_ShouldReturnUnauthorized()
  {
    // Arrange
    await InitializeTestAsync();

    var createRequest = new CreatePostRequest
    {
      Title = "Test Post Title",
      Content = "This is a test post content."
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/posts", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetPost_WithValidId_ShouldReturnPost()
  {
    // Arrange
    await InitializeTestAsync();
    var testData = await TestDataHelper.SeedDatabaseAsync(Factory, userCount: 1, postCount: 1);
    var post = testData.Posts.First();

    // Act
    var response = await Client.GetAsync($"/api/posts/{post.Id}");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var returnedPost = await HttpUtilities.ReadAsAsync<PostResponse>(response);
    returnedPost.Should().NotBeNull();
    returnedPost.Id.Should().Be(post.Id);
    returnedPost.Title.Should().Be(post.Title);
    returnedPost.Content.Should().Be(post.Content);
  }

  [Fact]
  public async Task GetPost_WithInvalidId_ShouldReturnNotFound()
  {
    // Arrange
    await InitializeTestAsync();

    // Act
    var response = await Client.GetAsync("/api/posts/999999");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }
}

/// <summary>
/// Response models for testing (these should match the actual API responses)
/// </summary>
public class PagedPostResponse
{
  public List<PostResponse> Items { get; set; } = new();
  public int TotalCount { get; set; }
  public int CurrentPage { get; set; }
  public int PageSize { get; set; }
  public int TotalPages { get; set; }
}
