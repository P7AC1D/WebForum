using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using WebForum.Api.Models;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Helpers;

/// <summary>
/// Helper utilities for moderation-related testing operations
/// </summary>
public static class ModerationTestHelpers
{
    /// <summary>
    /// Tags a post for moderation
    /// </summary>
    public static async Task<WebForum.Api.Models.Response.ModerationResponse> TagPostAsync(
        HttpClient moderatorClient,
        int postId)
    {
        var response = await moderatorClient.PostAsync($"/api/moderation/posts/{postId}/tag", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var moderationResponse = await response.Content.ReadFromJsonAsync<WebForum.Api.Models.Response.ModerationResponse>();
        moderationResponse.Should().NotBeNull();
        
        return moderationResponse!;
    }

    /// <summary>
    /// Removes a moderation tag from a post
    /// </summary>
    public static async Task<WebForum.Api.Models.Response.ModerationResponse> UntagPostAsync(
        HttpClient moderatorClient,
        int postId)
    {
        var response = await moderatorClient.DeleteAsync($"/api/moderation/posts/{postId}/tag");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var moderationResponse = await response.Content.ReadFromJsonAsync<WebForum.Api.Models.Response.ModerationResponse>();
        moderationResponse.Should().NotBeNull();
        
        return moderationResponse!;
    }

    /// <summary>
    /// Retrieves all tagged posts with pagination
    /// </summary>
    public static async Task<PagedResult<TaggedPost>> GetTaggedPostsAsync(
        HttpClient moderatorClient,
        int page = 1,
        int pageSize = 10)
    {
        var response = await moderatorClient.GetAsync($"/api/moderation/posts/tagged?page={page}&pageSize={pageSize}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var taggedPosts = await response.Content.ReadFromJsonAsync<PagedResult<TaggedPost>>();
        taggedPosts.Should().NotBeNull();
        
        return taggedPosts!;
    }

    /// <summary>
    /// Verifies that a post is tagged and appears in the tagged posts list
    /// </summary>
    public static async Task VerifyPostIsTaggedAsync(
        HttpClient moderatorClient,
        int postId,
        int moderatorId,
        string moderatorUsername)
    {
        var taggedPosts = await GetTaggedPostsAsync(moderatorClient);
        var taggedPost = taggedPosts.Items.FirstOrDefault(p => p.Id == postId);
        
        taggedPost.Should().NotBeNull($"Post {postId} should be in the tagged posts list");
        taggedPost!.TaggedByUserId.Should().Be(moderatorId);
        taggedPost.TaggedByUsername.Should().Be(moderatorUsername);
        taggedPost.TaggedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Verifies that a post is not tagged and does not appear in the tagged posts list
    /// </summary>
    public static async Task VerifyPostIsNotTaggedAsync(
        HttpClient moderatorClient,
        int postId)
    {
        var taggedPosts = await GetTaggedPostsAsync(moderatorClient);
        var taggedPost = taggedPosts.Items.FirstOrDefault(p => p.Id == postId);
        
        taggedPost.Should().BeNull($"Post {postId} should not be in the tagged posts list");
    }

    /// <summary>
    /// Validates that a moderation response contains expected data
    /// </summary>
    public static void ValidateModerationResponse(
        WebForum.Api.Models.Response.ModerationResponse moderationResponse,
        int expectedPostId,
        int expectedModeratorId,
        string expectedAction)
    {
        moderationResponse.Should().NotBeNull();
        moderationResponse.PostId.Should().Be(expectedPostId);
        moderationResponse.ModeratorId.Should().Be(expectedModeratorId);
        moderationResponse.Action.Should().Be(expectedAction);
        moderationResponse.ActionTimestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        
        if (expectedAction == "tagged")
        {
            moderationResponse.Tag.Should().NotBeEmpty();
        }
    }

    /// <summary>
    /// Validates that a tagged post contains expected data
    /// </summary>
    public static void ValidateTaggedPost(
        TaggedPost taggedPost,
        int expectedPostId,
        string expectedTitle,
        string expectedContent,
        int expectedAuthorId,
        int expectedTaggedByUserId,
        string expectedTaggedByUsername)
    {
        taggedPost.Should().NotBeNull();
        taggedPost.Id.Should().Be(expectedPostId);
        taggedPost.Title.Should().Be(expectedTitle);
        taggedPost.Content.Should().Be(expectedContent);
        taggedPost.AuthorId.Should().Be(expectedAuthorId);
        taggedPost.TaggedByUserId.Should().Be(expectedTaggedByUserId);
        taggedPost.TaggedByUsername.Should().Be(expectedTaggedByUsername);
        taggedPost.TaggedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));
        taggedPost.Tag.Should().NotBeEmpty();
    }

    /// <summary>
    /// Creates multiple posts and tags them for testing pagination
    /// </summary>
    public static async Task<List<int>> CreateAndTagMultiplePostsAsync(
        HttpClient userClient,
        HttpClient moderatorClient,
        int count,
        string titlePrefix = "Moderation Test Post")
    {
        var postIds = new List<int>();
        
        for (int i = 0; i < count; i++)
        {
            var post = await ContentTestHelpers.CreateTestPostAsync(
                userClient,
                title: $"{titlePrefix} {i + 1}",
                content: $"Content for moderation test post {i + 1}"
            );
            
            await TagPostAsync(moderatorClient, post.Id);
            postIds.Add(post.Id);
        }
        
        return postIds;
    }

    /// <summary>
    /// Verifies that moderator actions are properly restricted to authorized users
    /// </summary>
    public static async Task VerifyModerationEndpointSecurity(
        HttpClient unauthorizedClient,
        int postId)
    {
        // Verify tag endpoint is restricted
        var tagResponse = await unauthorizedClient.PostAsync($"/api/moderation/posts/{postId}/tag", null);
        tagResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        // Verify untag endpoint is restricted
        var untagResponse = await unauthorizedClient.DeleteAsync($"/api/moderation/posts/{postId}/tag");
        untagResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        // Verify tagged posts list endpoint is restricted
        var taggedPostsResponse = await unauthorizedClient.GetAsync("/api/moderation/posts/tagged");
        taggedPostsResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Tests moderation workflow with error conditions
    /// </summary>
    public static async Task VerifyModerationErrorHandling(
        HttpClient moderatorClient)
    {
        // Test tagging non-existent post
        var nonExistentPostResponse = await moderatorClient.PostAsync("/api/moderation/posts/99999/tag", null);
        nonExistentPostResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test untagging non-existent post
        var nonExistentUntagResponse = await moderatorClient.DeleteAsync("/api/moderation/posts/99999/tag");
        nonExistentUntagResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test invalid post IDs
        var invalidIdResponse = await moderatorClient.PostAsync("/api/moderation/posts/-1/tag", null);
        invalidIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var zeroIdResponse = await moderatorClient.PostAsync("/api/moderation/posts/0/tag", null);
        zeroIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests pagination of tagged posts
    /// </summary>
    public static async Task VerifyTaggedPostsPagination(
        HttpClient moderatorClient,
        int totalTaggedPosts,
        int pageSize = 10)
    {
        var expectedTotalPages = (int)Math.Ceiling((double)totalTaggedPosts / pageSize);

        // Test first page
        var firstPage = await GetTaggedPostsAsync(moderatorClient, 1, pageSize);
        ContentTestHelpers.ValidatePaginationResponse(firstPage, 1, pageSize, totalTaggedPosts, expectedTotalPages);

        if (expectedTotalPages > 1)
        {
            // Test last page
            var lastPage = await GetTaggedPostsAsync(moderatorClient, expectedTotalPages, pageSize);
            var expectedItemsOnLastPage = totalTaggedPosts - (expectedTotalPages - 1) * pageSize;
            lastPage.Items.Count().Should().Be(expectedItemsOnLastPage);

            // Verify no overlap between pages
            var firstPageIds = firstPage.Items.Select(p => p.Id).ToHashSet();
            var lastPageIds = lastPage.Items.Select(p => p.Id).ToHashSet();
            firstPageIds.Should().NotIntersectWith(lastPageIds);
        }

        // Test invalid pagination parameters
        var invalidPageResponse = await moderatorClient.GetAsync("/api/moderation/posts/tagged?page=0");
        invalidPageResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var invalidPageSizeResponse = await moderatorClient.GetAsync("/api/moderation/posts/tagged?pageSize=0");
        invalidPageSizeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
