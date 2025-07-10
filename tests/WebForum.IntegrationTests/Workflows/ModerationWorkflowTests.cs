using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace WebForum.IntegrationTests.Workflows;

/// <summary>
/// Integration tests for moderation workflows including post tagging, untagging, and moderation management
/// </summary>
public class ModerationWorkflowTests : IntegrationTestBase
{
  public ModerationWorkflowTests(WebForumTestFactory factory) : base(factory) { }

  [Fact]
  public async Task FullModerationWorkflow_ShouldCompleteSuccessfully()
  {
    // Arrange
    await InitializeTestAsync();

    // Create test users
    var moderator = await CreateTestUserAsync("moderator", "moderator@test.com", UserRoles.Moderator);
    var regularUser = await CreateTestUserAsync("user", "user@test.com", UserRoles.User);

    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, moderator.Role);
    var userClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, regularUser.Role);

    // Act & Assert - Create content to moderate
    var postRequest = new CreatePostRequest
    {
      Title = "Post with Misinformation",
      Content = "This post contains misleading information that needs moderation."
    };

    var postResponse = await userClient.PostAsJsonAsync("/api/posts", postRequest);
    postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var createdPost = await postResponse.Content.ReadFromJsonAsync<PostResponse>();
    createdPost.Should().NotBeNull();

    // Verify post is not initially tagged
    var moderationClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, UserRoles.Moderator);
    var taggedPostsResponse = await moderationClient.GetAsync("/api/posts/tagged");
    taggedPostsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var taggedPosts = await taggedPostsResponse.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();
    taggedPosts!.Items.Should().NotContain(p => p.Id == createdPost!.Id);

    // Moderator tags the post
    var tagResponse = await moderationClient.PostAsync($"/api/posts/{createdPost!.Id}/tags", null);
    tagResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var moderationResponse = await tagResponse.Content.ReadFromJsonAsync<ModerationResponse>();

    moderationResponse.Should().NotBeNull();
    moderationResponse!.PostId.Should().Be(createdPost.Id);
    moderationResponse.Action.Should().Be("tagged");
    moderationResponse.ModeratorId.Should().Be(moderator.Id);
    moderationResponse.Tag.Should().NotBeEmpty();

    // Verify post appears in tagged posts list
    var taggedPostsAfterTag = await moderationClient.GetAsync("/api/posts/tagged");
    taggedPostsAfterTag.StatusCode.Should().Be(HttpStatusCode.OK);
    var taggedPostsData = await taggedPostsAfterTag.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();
    taggedPostsData!.Items.Should().Contain(p => p.Id == createdPost.Id);

    var taggedPost = taggedPostsData.Items.First(p => p.Id == createdPost.Id);
    taggedPost.Title.Should().Be(postRequest.Title);
    taggedPost.Content.Should().Be(postRequest.Content);
    taggedPost.AuthorId.Should().Be(regularUser.Id);
    taggedPost.TaggedByUserId.Should().Be(moderator.Id);
    taggedPost.TaggedByUsername.Should().Be(moderator.Username);

    // Moderator removes the tag
    var removeTagResponse = await moderationClient.DeleteAsync($"/api/posts/{createdPost.Id}/tags");
    removeTagResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var untagResponse = await removeTagResponse.Content.ReadFromJsonAsync<ModerationResponse>();

    untagResponse.Should().NotBeNull();
    untagResponse!.PostId.Should().Be(createdPost.Id);
    untagResponse.Action.Should().Be("untagged");
    untagResponse.ModeratorId.Should().Be(moderator.Id);

    // Verify post is removed from tagged posts list
    var taggedPostsAfterRemoval = await moderationClient.GetAsync("/api/posts/tagged");
    taggedPostsAfterRemoval.StatusCode.Should().Be(HttpStatusCode.OK);
    var finalTaggedPosts = await taggedPostsAfterRemoval.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();
    finalTaggedPosts!.Items.Should().NotContain(p => p.Id == createdPost.Id);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_RegularUserCannotAccessModerationEndpoints()
  {
    // Arrange
    await InitializeTestAsync();

    var regularUser = await CreateTestUserAsync("user", "user@test.com", UserRoles.User);
    var userClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, regularUser.Role);

    // Create a post first
    var postRequest = new CreatePostRequest
    {
      Title = "Test Post",
      Content = "Test content"
    };

    var postResponse = await userClient.PostAsJsonAsync("/api/posts", postRequest);
    postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var createdPost = await postResponse.Content.ReadFromJsonAsync<PostResponse>();

    // Act & Assert - Regular user cannot tag posts
    var tagResponse = await userClient.PostAsync($"/api/posts/{createdPost!.Id}/tags", null);
    tagResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

    // Regular user cannot remove tags
    var removeTagResponse = await userClient.DeleteAsync($"/api/posts/{createdPost.Id}/tags");
    removeTagResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

    // Regular user cannot view tagged posts
    var taggedPostsResponse = await userClient.GetAsync("/api/posts/tagged");
    taggedPostsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_UnauthenticatedUserCannotAccessEndpoints()
  {
    // Arrange
    await InitializeTestAsync();

    var unauthenticatedClient = Factory.CreateClient();

    // Act & Assert - Unauthenticated requests should be rejected
    var tagResponse = await unauthenticatedClient.PostAsync("/api/posts/1/tags", null);
    tagResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    var removeTagResponse = await unauthenticatedClient.DeleteAsync("/api/posts/1/tags");
    removeTagResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    var taggedPostsResponse = await unauthenticatedClient.GetAsync("/api/posts/tagged");
    taggedPostsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_CannotTagNonExistentPost()
  {
    // Arrange
    await InitializeTestAsync();

    var moderator = await CreateTestUserAsync("moderator", "moderator@test.com", UserRoles.Moderator);
    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, moderator.Role);

    // Act & Assert
    var nonExistentPostId = 99999;
    var tagResponse = await moderatorClient.PostAsync($"/api/posts/{nonExistentPostId}/tags", null);
    tagResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_CannotTagSamePostTwice()
  {
    // Arrange
    await InitializeTestAsync();

    var moderator = await CreateTestUserAsync("moderator", "moderator@test.com", UserRoles.Moderator);
    var regularUser = await CreateTestUserAsync("user", "user@test.com", UserRoles.User);

    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, moderator.Role);
    var userClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, regularUser.Role);

    // Create a post
    var postRequest = new CreatePostRequest
    {
      Title = "Test Post for Double Tagging",
      Content = "This post will be tagged twice to test error handling."
    };

    var postResponse = await userClient.PostAsJsonAsync("/api/posts", postRequest);
    var createdPost = await postResponse.Content.ReadFromJsonAsync<PostResponse>();

    // Tag the post first time
    var firstTagResponse = await moderatorClient.PostAsync($"/api/posts/{createdPost!.Id}/tags", null);
    firstTagResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    // Act & Assert - Try to tag the same post again
    var secondTagResponse = await moderatorClient.PostAsync($"/api/posts/{createdPost.Id}/tags", null);
    secondTagResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_CannotRemoveTagFromUntaggedPost()
  {
    // Arrange
    await InitializeTestAsync();

    var moderator = await CreateTestUserAsync("moderator", "moderator@test.com", UserRoles.Moderator);
    var regularUser = await CreateTestUserAsync("user", "user@test.com", UserRoles.User);

    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, UserRoles.Moderator);
    var userClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, UserRoles.User);

    // Create a post (but don't tag it)
    var postRequest = new CreatePostRequest
    {
      Title = "Untagged Post",
      Content = "This post has no moderation tag."
    };

    var postResponse = await userClient.PostAsJsonAsync("/api/posts", postRequest);
    var createdPost = await postResponse.Content.ReadFromJsonAsync<PostResponse>();

    // Act & Assert - Try to remove tag from untagged post
    var removeTagResponse = await moderatorClient.DeleteAsync($"/api/posts/{createdPost!.Id}/tags");
    removeTagResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_TaggedPostsListPagination()
  {
    // Arrange
    await InitializeTestAsync();

    var moderator = await CreateTestUserAsync("moderator", "moderator@test.com", UserRoles.Moderator);
    var regularUser = await CreateTestUserAsync("user", "user@test.com", UserRoles.User);

    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, UserRoles.Moderator);
    var userClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, UserRoles.User);

    // Create and tag multiple posts
    var postIds = new List<int>();
    for (int i = 1; i <= 15; i++)
    {
      var postRequest = new CreatePostRequest
      {
        Title = $"Test Post {i}",
        Content = $"Content for test post {i} that will be tagged."
      };

      var postResponse = await userClient.PostAsJsonAsync("/api/posts", postRequest);
      var createdPost = await postResponse.Content.ReadFromJsonAsync<PostResponse>();
      postIds.Add(createdPost!.Id);

      // Tag the post
      await moderatorClient.PostAsync($"/api/posts/{createdPost.Id}/tags", null);
    }

    // Act & Assert - Test pagination
    var firstPageResponse = await moderatorClient.GetAsync("/api/posts/tagged?page=1&pageSize=10");
    firstPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();

    firstPage.Should().NotBeNull();
    firstPage!.Page.Should().Be(1);
    firstPage.PageSize.Should().Be(10);
    firstPage.Items.Count().Should().Be(10);
    firstPage.TotalCount.Should().Be(15);
    firstPage.TotalPages.Should().Be(2);

    var secondPageResponse = await moderatorClient.GetAsync("/api/posts/tagged?page=2&pageSize=10");
    secondPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();

    secondPage.Should().NotBeNull();
    secondPage!.Page.Should().Be(2);
    secondPage.Items.Count().Should().Be(5);

    // Verify no overlap between pages
    var firstPageIds = firstPage.Items.Select(p => p.Id).ToHashSet();
    var secondPageIds = secondPage.Items.Select(p => p.Id).ToHashSet();
    firstPageIds.Should().NotIntersectWith(secondPageIds);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_InvalidInputValidation()
  {
    // Arrange
    await InitializeTestAsync();

    var moderator = await CreateTestUserAsync("moderator", "moderator@test.com", UserRoles.Moderator);
    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, UserRoles.Moderator);

    // Act & Assert - Test invalid post IDs
    var negativeIdResponse = await moderatorClient.PostAsync("/api/posts/-1/tags", null);
    negativeIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    var zeroIdResponse = await moderatorClient.PostAsync("/api/posts/0/tags", null);
    zeroIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    // Test invalid pagination parameters
    var invalidPageResponse = await moderatorClient.GetAsync("/api/posts/tagged?page=0&pageSize=10");
    invalidPageResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    var invalidPageSizeResponse = await moderatorClient.GetAsync("/api/posts/tagged?page=1&pageSize=0");
    invalidPageSizeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    var tooLargePageSizeResponse = await moderatorClient.GetAsync("/api/posts/tagged?page=1&pageSize=100");
    tooLargePageSizeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    await CleanupTestAsync();
  }

  [Fact]
  public async Task ModerationWorkflow_MultipleModeratorsCanTagDifferentPosts()
  {
    // Arrange
    await InitializeTestAsync();

    var moderator1 = await CreateTestUserAsync("moderator1", "mod1@test.com", UserRoles.Moderator);
    var moderator2 = await CreateTestUserAsync("moderator2", "mod2@test.com", UserRoles.Moderator);
    var regularUser = await CreateTestUserAsync("user", "user@test.com", UserRoles.User);

    var moderator1Client = CreateAuthenticatedClient(moderator1.Id, moderator1.Username, UserRoles.Moderator);
    var moderator2Client = CreateAuthenticatedClient(moderator2.Id, moderator2.Username, UserRoles.Moderator);
    var userClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, UserRoles.User);

    // Create two posts with unique titles to avoid ID conflicts
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var post1Request = new CreatePostRequest { Title = $"Multiple Moderators Post 1 {timestamp}", Content = "This is test content for post 1 that meets minimum length requirements." };
    var post2Request = new CreatePostRequest { Title = $"Multiple Moderators Post 2 {timestamp}", Content = "This is test content for post 2 that meets minimum length requirements." };

    var post1Response = await userClient.PostAsJsonAsync("/api/posts", post1Request);
    var post2Response = await userClient.PostAsJsonAsync("/api/posts", post2Request);

    post1Response.StatusCode.Should().Be(HttpStatusCode.Created);
    post2Response.StatusCode.Should().Be(HttpStatusCode.Created);

    var post1 = await post1Response.Content.ReadFromJsonAsync<PostResponse>();
    var post2 = await post2Response.Content.ReadFromJsonAsync<PostResponse>();

    // Ensure posts exist before attempting to tag them
    using (var context = GetDbContext())
    {
      // Remove any existing tags from these posts to avoid conflicts
      var existingTags = context.PostTags.Where(pt => pt.PostId == post1!.Id || pt.PostId == post2!.Id);
      context.PostTags.RemoveRange(existingTags);
      await context.SaveChangesAsync();
    }

    // Act - Different moderators tag different posts
    var tag1Response = await moderator1Client.PostAsync($"/api/posts/{post1!.Id}/tags", null);
    var tag2Response = await moderator2Client.PostAsync($"/api/posts/{post2!.Id}/tags", null);

    // Assert
    tag1Response.StatusCode.Should().Be(HttpStatusCode.OK);
    tag2Response.StatusCode.Should().Be(HttpStatusCode.OK);

    var moderation1 = await tag1Response.Content.ReadFromJsonAsync<ModerationResponse>();
    var moderation2 = await tag2Response.Content.ReadFromJsonAsync<ModerationResponse>();

    moderation1!.ModeratorId.Should().Be(moderator1.Id);
    moderation2!.ModeratorId.Should().Be(moderator2.Id);

    // Both moderators should see both tagged posts
    var taggedPosts1 = await moderator1Client.GetAsync("/api/posts/tagged");
    var taggedPosts2 = await moderator2Client.GetAsync("/api/posts/tagged");

    taggedPosts1.StatusCode.Should().Be(HttpStatusCode.OK);
    taggedPosts2.StatusCode.Should().Be(HttpStatusCode.OK);

    var tagged1Data = await taggedPosts1.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();
    var tagged2Data = await taggedPosts2.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();

    tagged1Data!.Items.Count().Should().Be(2);
    tagged2Data!.Items.Count().Should().Be(2);

    await CleanupTestAsync();
  }
}
