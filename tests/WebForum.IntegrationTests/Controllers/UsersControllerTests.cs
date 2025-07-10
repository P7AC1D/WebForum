using FluentAssertions;
using System.Net;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Utilities;
using WebForum.Api.Models;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for UsersController API endpoints
/// Focuses on user profile operations, privacy, and user-related data retrieval
/// </summary>
public class UsersControllerTests : IntegrationTestBase
{
    public UsersControllerTests(WebForumTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUser_WithValidId_ShouldReturnUserInfo()
    {
        // Arrange
        await InitializeTestAsync();
        var testUser = await CreateTestUserAsync("testuser", "test@example.com");

        // Act
        var response = await Client.GetAsync($"/api/users/{testUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var userResponse = await HttpUtilities.ReadAsAsync<UserResponse>(response);
        userResponse.Should().NotBeNull();
        userResponse.Id.Should().Be(testUser.Id);
        userResponse.Username.Should().Be(testUser.Username);
        
        // Email should not be exposed in public profile for privacy
        userResponse.Email.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetUser_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        await InitializeTestAsync();

        var invalidIds = new[] { 0, -1, 999999, int.MaxValue };

        foreach (var invalidId in invalidIds)
        {
            // Act
            var response = await Client.GetAsync($"/api/users/{invalidId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, 
                $"User ID {invalidId} should return NotFound");
        }
    }

    [Fact]
    public async Task GetUser_AuthenticatedRequest_ShouldReturnUserInfo()
    {
        // Arrange
        await InitializeTestAsync();
        var testUser = await CreateTestUserAsync("authuser", "auth@example.com");
        var authenticatedClient = CreateAuthenticatedClient(testUser.Id, testUser.Username);

        // Act
        var response = await authenticatedClient.GetAsync($"/api/users/{testUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var userResponse = await HttpUtilities.ReadAsAsync<UserResponse>(response);
        userResponse.Should().NotBeNull();
        userResponse.Id.Should().Be(testUser.Id);
        userResponse.Username.Should().Be(testUser.Username);
    }

    [Fact]
    public async Task GetUserPosts_WithValidUserId_ShouldReturnUserPosts()
    {
        // Arrange
        await InitializeTestAsync();
        var author = await CreateTestUserAsync("author", "author@example.com");
        var otherUser = await CreateTestUserAsync("other", "other@example.com");

        // Create posts for the author
        var authorPosts = await Task.WhenAll(
            CreateTestPostAsync(author.Id, "Author Post 1", "Content 1"),
            CreateTestPostAsync(author.Id, "Author Post 2", "Content 2"),
            CreateTestPostAsync(author.Id, "Author Post 3", "Content 3")
        );

        // Create posts for other user (should not be included)
        await CreateTestPostAsync(otherUser.Id, "Other User Post", "Other content");

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var postsResult = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
        postsResult.Should().NotBeNull();
        postsResult.Items.Should().HaveCount(3);
        postsResult.TotalCount.Should().Be(3);
        postsResult.Items.Should().AllSatisfy(post => 
        {
            post.AuthorId.Should().Be(author.Id);
            post.Title.Should().StartWith("Author Post");
        });

        // Verify posts are in the correct order (usually newest first)
        var postIds = postsResult.Items.Select(p => p.Id).ToList();
        var expectedIds = authorPosts.OrderByDescending(p => p.CreatedAt).Select(p => p.Id).ToList();
        postIds.Should().BeEquivalentTo(expectedIds, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetUserPosts_WithPagination_ShouldRespectPaginationParameters()
    {
        // Arrange
        await InitializeTestAsync();
        var author = await CreateTestUserAsync("prolificauthor", "prolific@example.com");

        // Create many posts
        var posts = await Task.WhenAll(Enumerable.Range(1, 15).Select(i =>
            CreateTestPostAsync(author.Id, $"Post {i:D2}", $"Content for post {i}")
        ));

        // Act - Get first page
        var page1Response = await Client.GetAsync($"/api/users/{author.Id}/posts?page=1&pageSize=5");
        
        // Act - Get second page
        var page2Response = await Client.GetAsync($"/api/users/{author.Id}/posts?page=2&pageSize=5");

        // Assert
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page1Result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(page1Response);
        var page2Result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(page2Response);

        // Page 1 assertions
        page1Result.Items.Should().HaveCount(5);
        page1Result.Page.Should().Be(1);
        page1Result.PageSize.Should().Be(5);
        page1Result.TotalCount.Should().Be(15);
        page1Result.TotalPages.Should().Be(3);

        // Page 2 assertions
        page2Result.Items.Should().HaveCount(5);
        page2Result.Page.Should().Be(2);
        page2Result.PageSize.Should().Be(5);
        page2Result.TotalCount.Should().Be(15);

        // Posts should be different between pages
        var page1Ids = page1Result.Items.Select(p => p.Id).ToList();
        var page2Ids = page2Result.Items.Select(p => p.Id).ToList();
        page1Ids.Should().NotIntersectWith(page2Ids);

        // All posts should be from the same author
        page1Result.Items.Should().AllSatisfy(post => post.AuthorId.Should().Be(author.Id));
        page2Result.Items.Should().AllSatisfy(post => post.AuthorId.Should().Be(author.Id));
    }

    [Fact]
    public async Task GetUserPosts_WithInvalidUserId_ShouldReturnNotFound()
    {
        // Arrange
        await InitializeTestAsync();

        // Act
        var response = await Client.GetAsync("/api/users/999999/posts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserPosts_ForUserWithNoPosts_ShouldReturnEmptyList()
    {
        // Arrange
        await InitializeTestAsync();
        var userWithNoPosts = await CreateTestUserAsync("nopostuser", "noposts@example.com");

        // Act
        var response = await Client.GetAsync($"/api/users/{userWithNoPosts.Id}/posts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var postsResult = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
        postsResult.Should().NotBeNull();
        postsResult.Items.Should().BeEmpty();
        postsResult.TotalCount.Should().Be(0);
        postsResult.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetUserPosts_WithInvalidPagination_ShouldHandleGracefully()
    {
        // Arrange
        await InitializeTestAsync();
        var author = await CreateTestUserAsync("testauthor", "testauthor@example.com");
        await CreateTestPostAsync(author.Id, "Test Post", "Test Content");

        var testCases = new[]
        {
            $"/api/users/{author.Id}/posts?page=0&pageSize=10",        // Invalid page
            $"/api/users/{author.Id}/posts?page=-1&pageSize=10",       // Negative page
            $"/api/users/{author.Id}/posts?page=1&pageSize=0",         // Invalid page size
            $"/api/users/{author.Id}/posts?page=1&pageSize=-5",        // Negative page size
            $"/api/users/{author.Id}/posts?page=999&pageSize=10"       // Page beyond available data
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
    public async Task GetUserPosts_WithDateFiltering_ShouldFilterCorrectly()
    {
        // Arrange
        await InitializeTestAsync();
        var author = await CreateTestUserAsync("dateauthor", "date@example.com");

        // Create posts (they will have current timestamps)
        await CreateTestPostAsync(author.Id, "Recent Post 1", "Recent content 1");
        await CreateTestPostAsync(author.Id, "Recent Post 2", "Recent content 2");

        var dateFrom = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var dateTo = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Act
        var response = await Client.GetAsync(
            $"/api/users/{author.Id}/posts?dateFrom={dateFrom}&dateTo={dateTo}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(post => 
        {
            post.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(-2));
            post.CreatedAt.Should().BeBefore(DateTimeOffset.UtcNow.AddDays(2));
            post.AuthorId.Should().Be(author.Id);
        });
    }

    [Fact]
    public async Task GetUserPosts_WithSorting_ShouldSortCorrectly()
    {
        // Arrange
        await InitializeTestAsync();
        var author = await CreateTestUserAsync("sortauthor", "sort@example.com");

        // Create posts with known titles for sorting
        await CreateTestPostAsync(author.Id, "Alpha Post", "Content A");
        await CreateTestPostAsync(author.Id, "Beta Post", "Content B");
        await CreateTestPostAsync(author.Id, "Charlie Post", "Content C");

        // Act
        var ascResponse = await Client.GetAsync($"/api/users/{author.Id}/posts?sortBy=title&sortOrder=asc");
        var descResponse = await Client.GetAsync($"/api/users/{author.Id}/posts?sortBy=title&sortOrder=desc");

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

        // All posts should still be from the correct author
        ascResult.Items.Should().AllSatisfy(post => post.AuthorId.Should().Be(author.Id));
        descResult.Items.Should().AllSatisfy(post => post.AuthorId.Should().Be(author.Id));
    }

    [Fact]
    public async Task GetUser_MultipleConcurrentRequests_ShouldHandleCorrectly()
    {
        // Arrange
        await InitializeTestAsync();
        var testUser = await CreateTestUserAsync("concurrentuser", "concurrent@example.com");

        // Act - Make multiple concurrent requests for the same user
        var tasks = Enumerable.Range(1, 10).Select(_ => 
            Client.GetAsync($"/api/users/{testUser.Id}"));
        
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response => 
            response.StatusCode.Should().Be(HttpStatusCode.OK));

        var userResponses = await Task.WhenAll(responses.Select(r => 
            HttpUtilities.ReadAsAsync<UserResponse>(r)));

        // All responses should return the same user data
        userResponses.Should().AllSatisfy(userResponse => 
        {
            userResponse.Id.Should().Be(testUser.Id);
            userResponse.Username.Should().Be(testUser.Username);
        });
    }

    [Fact]
    public async Task GetUserPosts_WithAuthentication_ShouldNotExposePrivateData()
    {
        // Arrange
        await InitializeTestAsync();
        var author = await CreateTestUserAsync("privateauthor", "private@example.com");
        var viewer = await CreateTestUserAsync("viewer", "viewer@example.com");
        
        await CreateTestPostAsync(author.Id, "Public Post", "This is public content");
        
        var viewerClient = CreateAuthenticatedClient(viewer.Id, viewer.Username);

        // Act
        var response = await viewerClient.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(response);
        result.Items.Should().HaveCount(1);
        
        var post = result.Items.First();
        post.AuthorId.Should().Be(author.Id);
        post.Title.Should().Be("Public Post");
        
        // Post should not contain any sensitive author information beyond what's public
        post.Content.Should().NotContain("private");
        post.Content.Should().NotContain("email");
    }

    [Fact]
    public async Task UserEndpoints_PerformanceTest_ShouldHandleManyUsers()
    {
        // Arrange
        await InitializeTestAsync();
        var users = await Task.WhenAll(Enumerable.Range(1, 20).Select(i =>
            CreateTestUserAsync($"perfuser{i}", $"perf{i}@example.com")));

        // Create posts for each user
        var postTasks = users.SelectMany(user => 
            Enumerable.Range(1, 3).Select(j =>
                CreateTestPostAsync(user.Id, $"Post {j} by {user.Username}", $"Content {j}")
            )
        );
        await Task.WhenAll(postTasks);

        // Act - Get user info for all users concurrently
        var userInfoTasks = users.Select(user => 
            Client.GetAsync($"/api/users/{user.Id}"));
        var userPostTasks = users.Select(user =>
            Client.GetAsync($"/api/users/{user.Id}/posts"));

        var userInfoResponses = await Task.WhenAll(userInfoTasks);
        var userPostResponses = await Task.WhenAll(userPostTasks);

        // Assert
        userInfoResponses.Should().AllSatisfy(response => 
            response.StatusCode.Should().Be(HttpStatusCode.OK));
        
        userPostResponses.Should().AllSatisfy(response => 
            response.StatusCode.Should().Be(HttpStatusCode.OK));

        // Verify post counts are correct
        var postResults = await Task.WhenAll(userPostResponses.Select(r =>
            HttpUtilities.ReadAsAsync<PagedResult<PostResponse>>(r)));

        postResults.Should().AllSatisfy(result => 
        {
            result.Items.Should().HaveCount(3);
            result.TotalCount.Should().Be(3);
        });
    }
}
