using FluentAssertions;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;

namespace WebForum.IntegrationTests.CrossCutting;

/// <summary>
/// Integration tests for API performance characteristics and load handling
/// </summary>
public class PerformanceTests : IntegrationTestBase
{
  public PerformanceTests(WebForumTestFactory factory) : base(factory) { }

  [Fact]
  public async Task Api_ShouldHandleBasicOperationsWithinReasonableTime()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    // Act & Assert - Registration should complete quickly
    var registrationStopwatch = Stopwatch.StartNew();
    var newUser = await CreateTestUserAsync();
    registrationStopwatch.Stop();
    registrationStopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 seconds max

    // Act & Assert - Post creation should complete quickly
    var postCreationStopwatch = Stopwatch.StartNew();
    var postRequest = new CreatePostRequest
    {
      Title = "Performance Test Post",
      Content = "This post is created for performance testing purposes."
    };
    var postResponse = await authenticatedClient.PostAsJsonAsync("/api/posts", postRequest);
    postCreationStopwatch.Stop();

    postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    postCreationStopwatch.ElapsedMilliseconds.Should().BeLessThan(3000); // 3 seconds max

    // Act & Assert - Post retrieval should complete quickly
    var postRetrievalStopwatch = Stopwatch.StartNew();
    var getPostsResponse = await authenticatedClient.GetAsync("/api/posts?page=1&pageSize=10");
    postRetrievalStopwatch.Stop();

    getPostsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    postRetrievalStopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // 2 seconds max

  }

  [Fact]
  public async Task Api_ShouldHandleConcurrentRegistrations()
  {
    // Arrange
    const int concurrentRequests = 10;

    // Act
    var tasks = Enumerable.Range(0, concurrentRequests)
        .Select(i => CreateTestUserAsync($"concurrentuser{i}", $"concurrent{i}@test.com"))
        .ToArray();

    var stopwatch = Stopwatch.StartNew();
    var results = await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    results.Should().HaveCount(concurrentRequests);
    results.Should().AllSatisfy(user => user.Should().NotBeNull());

    // All users should be created within reasonable time
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000); // 15 seconds for 10 concurrent registrations

    // All usernames should be unique
    var usernames = results.Select(r => r.Username).ToHashSet();
    usernames.Should().HaveCount(concurrentRequests);

  }

  [Fact]
  public async Task Api_ShouldHandleConcurrentPostCreations()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    const int concurrentPosts = 5;

    // Act
    var tasks = Enumerable.Range(0, concurrentPosts)
        .Select(async i =>
        {
          var request = new CreatePostRequest
          {
            Title = $"Concurrent Post {i}",
            Content = $"Content for concurrent post {i} created during load testing."
          };
          return await authenticatedClient.PostAsJsonAsync("/api/posts", request);
        })
        .ToArray();

    var stopwatch = Stopwatch.StartNew();
    var responses = await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    responses.Should().HaveCount(concurrentPosts);
    responses.Should().AllSatisfy(response =>
        response.StatusCode.Should().Be(HttpStatusCode.Created));

    // Should complete within reasonable time
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // 10 seconds for 5 concurrent posts

  }

  [Fact]
  public async Task Api_ShouldHandleLargeDataSetsWithPagination()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    // Create a moderate number of posts for pagination testing
    const int numberOfPosts = 25;
    for (int i = 0; i < numberOfPosts; i++)
    {
      var request = new CreatePostRequest
      {
        Title = $"Test Post {i + 1}",
        Content = $"Content for test post {i + 1} used for pagination performance testing."
      };
      await authenticatedClient.PostAsJsonAsync("/api/posts", request);
    }

    // Act & Assert - Test pagination performance
    var pageRetrievalStopwatch = Stopwatch.StartNew();

    // Retrieve first page
    var firstPageResponse = await authenticatedClient.GetAsync("/api/posts?page=1&pageSize=10");
    var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PagedResult<PostResponse>>();

    // Retrieve middle page
    var middlePageResponse = await authenticatedClient.GetAsync("/api/posts?page=2&pageSize=10");
    var middlePage = await middlePageResponse.Content.ReadFromJsonAsync<PagedResult<PostResponse>>();

    // Retrieve last page
    var lastPageResponse = await authenticatedClient.GetAsync("/api/posts?page=3&pageSize=10");
    var lastPage = await lastPageResponse.Content.ReadFromJsonAsync<PagedResult<PostResponse>>();

    pageRetrievalStopwatch.Stop();

    // Assert
    firstPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    middlePageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    lastPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    firstPage!.Items.Should().HaveCount(10);
    middlePage!.Items.Should().HaveCount(10);
    lastPage!.Items.Should().HaveCount(5); // Remaining posts

    // Pagination should be fast even with multiple pages
    pageRetrievalStopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 seconds for 3 page requests

  }

  [Fact]
  public async Task Api_ShouldHandleRepeatedRequests()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    // Create a post for repeated retrieval
    var postRequest = new CreatePostRequest
    {
      Title = "Post for Repeated Access",
      Content = "This post will be accessed multiple times to test performance."
    };
    var postResponse = await authenticatedClient.PostAsJsonAsync("/api/posts", postRequest);
    var post = await postResponse.Content.ReadFromJsonAsync<PostResponse>();

    // Act - Make repeated requests to the same endpoint
    const int numberOfRequests = 20;
    var stopwatch = Stopwatch.StartNew();

    var tasks = Enumerable.Range(0, numberOfRequests)
        .Select(_ => authenticatedClient.GetAsync($"/api/posts/{post!.Id}"))
        .ToArray();

    var responses = await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    responses.Should().HaveCount(numberOfRequests);
    responses.Should().AllSatisfy(response =>
        response.StatusCode.Should().Be(HttpStatusCode.OK));

    // Repeated requests should complete quickly (caching, etc.)
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(8000); // 8 seconds for 20 requests

  }

  [Fact]
  public async Task Api_ShouldHandleSearchOperationsEfficiently()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    // Create posts with searchable content
    var searchTerms = new[] { "technology", "programming", "database", "security", "performance" };

    for (int i = 0; i < searchTerms.Length; i++)
    {
      var request = new CreatePostRequest
      {
        Title = $"Post about {searchTerms[i]}",
        Content = $"This is a detailed post about {searchTerms[i]} and related topics. " +
                   $"It contains information relevant to {searchTerms[i]} professionals."
      };
      await authenticatedClient.PostAsJsonAsync("/api/posts", request);
    }

    // Act - Perform search operations
    var searchStopwatch = Stopwatch.StartNew();

    var searchTasks = searchTerms.Select(term =>
        authenticatedClient.GetAsync($"/api/posts?search={term}&page=1&pageSize=10")
    ).ToArray();

    var searchResponses = await Task.WhenAll(searchTasks);
    searchStopwatch.Stop();

    // Assert
    searchResponses.Should().HaveCount(searchTerms.Length);
    searchResponses.Should().AllSatisfy(response =>
        response.StatusCode.Should().Be(HttpStatusCode.OK));

    // Search operations should complete within reasonable time
    searchStopwatch.ElapsedMilliseconds.Should().BeLessThan(6000); // 6 seconds for 5 search operations

  }

  [Fact]
  public async Task Api_ShouldMaintainResponseTimesUnderLoad()
  {
    // Arrange
    // Create multiple users for load testing
    var users = new List<UserInfo>();
    for (int i = 0; i < 5; i++)
    {
      users.Add(await CreateTestUserAsync($"loaduser{i}", $"load{i}@test.com"));
    }

    // Act - Simulate mixed load with different operations
    var loadTasks = new List<Task<HttpResponseMessage>>();

    foreach (var user in users)
    {
      var userClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

      // Each user performs multiple operations
      loadTasks.Add(userClient.PostAsJsonAsync("/api/posts", new CreatePostRequest
      {
        Title = $"Load Test Post by {user.Username}",
        Content = "Content for load testing"
      }));

      loadTasks.Add(userClient.GetAsync("/api/posts?page=1&pageSize=5"));
      loadTasks.Add(userClient.GetAsync($"/api/users/{user.Id}"));
    }

    var loadStopwatch = Stopwatch.StartNew();
    var loadResponses = await Task.WhenAll(loadTasks);
    loadStopwatch.Stop();

    // Assert
    loadResponses.Should().HaveCount(users.Count * 3); // 3 operations per user

    // Most responses should be successful (some might fail due to race conditions, but most should succeed)
    var successfulResponses = loadResponses.Count(r => r.IsSuccessStatusCode);
    successfulResponses.Should().BeGreaterThan((int)(loadResponses.Length * 0.8)); // At least 80% success rate

    // Load test should complete within reasonable time
    loadStopwatch.ElapsedMilliseconds.Should().BeLessThan(20000); // 20 seconds for mixed load

  }

  /// <summary>
  /// Helper method to create a test user with unique credentials
  /// </summary>
  private async Task<UserInfo> CreateTestUserAsync(string? username = null, string? email = null)
  {
    var uniqueId = Guid.NewGuid().ToString("N")[..8];
    var registrationRequest = new RegistrationRequest
    {
      Username = username ?? $"user_{uniqueId}",
      Email = email ?? $"user_{uniqueId}@test.com",
      Password = "Test123!@#"
    };

    var response = await Client.PostAsJsonAsync("/api/auth/register", registrationRequest);
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
    return new UserInfo
    {
      Id = authResponse!.User.Id,
      Username = authResponse.User.Username,
      Email = authResponse.User.Email,
      Role = UserRoles.User.ToString()
    };
  }
}
