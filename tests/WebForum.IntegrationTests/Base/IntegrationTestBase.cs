using FluentAssertions;
using WebForum.IntegrationTests.Infrastructure;

namespace WebForum.IntegrationTests.Base;

/// <summary>
/// Base class for integration tests providing common setup and utilities
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<WebForumTestFactory>
{
  protected readonly WebForumTestFactory Factory;
  protected readonly HttpClient Client;

  protected IntegrationTestBase(WebForumTestFactory factory)
  {
    Factory = factory;
    Client = factory.CreateClient();
  }

  /// <summary>
  /// Sets up a clean database for the test
  /// </summary>
  protected async Task InitializeTestAsync()
  {
    // First ensure database is created and migrations are applied
    await Factory.EnsureDatabaseCreatedAsync();
    
    // Then clean any existing data
    await Factory.CleanDatabaseAsync();
  }

  /// <summary>
  /// Cleans up after the test
  /// </summary>
  protected async Task CleanupTestAsync()
  {
    await Factory.CleanDatabaseAsync();
  }

  /// <summary>
  /// Creates an authenticated HTTP client for testing
  /// </summary>
  /// <param name="userId">User ID for authentication</param>
  /// <param name="username">Username for authentication</param>
  /// <param name="roles">User roles for authorization</param>
  /// <returns>Authenticated HTTP client</returns>
  protected HttpClient CreateAuthenticatedClient(
      int userId,
      string username,
      WebForum.Api.Models.UserRoles roles = WebForum.Api.Models.UserRoles.User)
  {
    return TestAuthenticationHelper.CreateAuthenticatedClient(Factory, userId, username, roles);
  }

  /// <summary>
  /// Gets a database context for direct database operations
  /// </summary>
  protected WebForum.Api.Data.ForumDbContext GetDbContext()
  {
    return Factory.GetDbContext();
  }

  /// <summary>
  /// Seeds the database with test data
  /// </summary>
  /// <param name="userCount">Number of users to create</param>
  /// <param name="postCount">Number of posts to create</param>
  /// <param name="commentCount">Number of comments to create</param>
  /// <param name="likeCount">Number of likes to create</param>
  /// <returns>Generated test data</returns>
  protected async Task<TestDataSet> SeedTestDataAsync(
      int userCount = 5,
      int postCount = 10,
      int commentCount = 20,
      int likeCount = 15)
  {
    return await TestDataHelper.SeedDatabaseAsync(Factory, userCount, postCount, commentCount, likeCount);
  }

  /// <summary>
  /// Creates a test user in the database
  /// </summary>
  /// <param name="username">Username for the user</param>
  /// <param name="email">Email for the user</param>
  /// <param name="roles">User roles</param>
  /// <returns>Created user entity</returns>
  protected async Task<WebForum.Api.Data.DTOs.UserEntity> CreateTestUserAsync(
      string username = "testuser",
      string email = "test@example.com",
      WebForum.Api.Models.UserRoles roles = WebForum.Api.Models.UserRoles.User)
  {
    using var dbContext = GetDbContext();
    return await TestDataHelper.CreateTestUserAsync(dbContext, username, email, roles);
  }

  /// <summary>
  /// Creates a test post in the database
  /// </summary>
  /// <param name="authorId">Author user ID</param>
  /// <param name="title">Post title</param>
  /// <param name="content">Post content</param>
  /// <returns>Created post entity</returns>
  protected async Task<WebForum.Api.Data.DTOs.PostEntity> CreateTestPostAsync(
      int authorId,
      string title = "Test Post",
      string content = "This is a test post content.")
  {
    using var dbContext = GetDbContext();
    return await TestDataHelper.CreateTestPostAsync(dbContext, authorId, title, content);
  }

  /// <summary>
  /// Creates a test comment in the database
  /// </summary>
  /// <param name="postId">Post ID to comment on</param>
  /// <param name="authorId">Author user ID</param>
  /// <param name="content">Comment content</param>
  /// <returns>Created comment entity</returns>
  protected async Task<WebForum.Api.Data.DTOs.CommentEntity> CreateTestCommentAsync(
      int postId,
      int authorId,
      string content = "This is a test comment.")
  {
    using var dbContext = GetDbContext();
    return await TestDataHelper.CreateTestCommentAsync(dbContext, postId, authorId, content);
  }

  /// <summary>
  /// Asserts that an HTTP response has the expected status code
  /// </summary>
  /// <param name="response">HTTP response to check</param>
  /// <param name="expectedStatusCode">Expected status code</param>
  protected static void AssertStatusCode(HttpResponseMessage response, System.Net.HttpStatusCode expectedStatusCode)
  {
    response.StatusCode.Should().Be(expectedStatusCode,
        $"Expected status code {expectedStatusCode} but got {response.StatusCode}. Response: {response.Content.ReadAsStringAsync().Result}");
  }

  /// <summary>
  /// Asserts that an HTTP response is successful (2xx status code)
  /// </summary>
  /// <param name="response">HTTP response to check</param>
  protected static void AssertSuccessStatusCode(HttpResponseMessage response)
  {
    response.IsSuccessStatusCode.Should().BeTrue(
        $"Expected successful status code but got {response.StatusCode}. Response: {response.Content.ReadAsStringAsync().Result}");
  }

  /// <summary>
  /// Deserializes JSON response content to the specified type
  /// </summary>
  /// <typeparam name="T">Type to deserialize to</typeparam>
  /// <param name="response">HTTP response containing JSON</param>
  /// <returns>Deserialized object</returns>
  protected static async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response)
  {
    var content = await response.Content.ReadAsStringAsync();
    var result = System.Text.Json.JsonSerializer.Deserialize<T>(content, new System.Text.Json.JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true
    });

    result.Should().NotBeNull($"Failed to deserialize response content: {content}");
    return result!;
  }

  /// <summary>
  /// Serializes an object to JSON for HTTP requests
  /// </summary>
  /// <param name="obj">Object to serialize</param>
  /// <returns>JSON string content</returns>
  protected static StringContent SerializeToJson(object obj)
  {
    var json = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions
    {
      PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    });
    return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
  }
}
