using FluentAssertions;
using System.Net;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Utilities;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.Api.Models;

namespace WebForum.IntegrationTests.Workflows;

/// <summary>
/// Integration tests for complete content creation workflows
/// Covers post creation → commenting → liking → tagging scenarios
/// </summary>
public class ContentCreationWorkflowTests : IntegrationTestBase
{
  public ContentCreationWorkflowTests(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task CompleteContentLifecycle_CreatePostAddCommentsLike_ShouldWork()
  {
    // Arrange
    var author = await CreateTestUserAsync("author", "author@example.com");
    var commenter = await CreateTestUserAsync("commenter", "commenter@example.com");
    var liker = await CreateTestUserAsync("liker", "liker@example.com");

    var authorClient = CreateAuthenticatedClient(author.Id, author.Username);
    var commenterClient = CreateAuthenticatedClient(commenter.Id, commenter.Username);
    var likerClient = CreateAuthenticatedClient(liker.Id, liker.Username);

    // Act & Assert - Create Post
    var createPostRequest = new CreatePostRequest
    {
      Title = "Complete Workflow Test Post",
      Content = "This post will be used to test the complete content lifecycle including comments and likes."
    };

    var createPostResponse = await HttpUtilities.PostAsync(authorClient, "/api/posts", createPostRequest);
    createPostResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var createdPost = await HttpUtilities.ReadAsAsync<PostResponse>(createPostResponse);
    createdPost.Should().NotBeNull();
    createdPost.Title.Should().Be(createPostRequest.Title);
    createdPost.AuthorId.Should().Be(author.Id);

    // Act & Assert - Add Comments
    var commentRequest = new CreateCommentRequest
    {
      Content = "This is a great post! Thanks for sharing."
    };

    var createCommentResponse = await HttpUtilities.PostAsync(
        commenterClient,
        $"/api/posts/{createdPost.Id}/comments",
        commentRequest);
    createCommentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var createdComment = await HttpUtilities.ReadAsAsync<CommentResponse>(createCommentResponse);
    createdComment.Should().NotBeNull();
    createdComment.Content.Should().Be(commentRequest.Content);
    createdComment.AuthorId.Should().Be(commenter.Id);
    createdComment.PostId.Should().Be(createdPost.Id);

    // Act & Assert - Like Post
    var likeResponse = await likerClient.PostAsync($"/api/posts/{createdPost.Id}/like", null);
    likeResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

    // Act & Assert - Verify like was created
    var getPostResponse = await Client.GetAsync($"/api/posts/{createdPost.Id}");
    getPostResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var updatedPost = await HttpUtilities.ReadAsAsync<PostResponse>(getPostResponse);
    updatedPost.LikeCount.Should().Be(1);

    // Act & Assert - Get Comments
    var getCommentsResponse = await Client.GetAsync($"/api/posts/{createdPost.Id}/comments");
    getCommentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var commentsResult = await HttpUtilities.ReadAsAsync<PagedResult<CommentResponse>>(getCommentsResponse);
    commentsResult.Should().NotBeNull();
    commentsResult.Items.Should().HaveCount(1);
    commentsResult.Items.First().Content.Should().Be(commentRequest.Content);
  }

  [Fact]
  public async Task CreatePostWithTagging_ShouldAllowTagManagement()
  {
    // Arrange
    var author = await CreateTestUserAsync("tagger", "tagger@example.com");
    var moderator = await CreateTestUserAsync("moderator", "moderator@example.com", UserRoles.Moderator);

    var authorClient = CreateAuthenticatedClient(author.Id, author.Username);
    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, moderator.Role);

    // Act & Assert - Create Post
    var createPostRequest = new CreatePostRequest
    {
      Title = "Post About Programming",
      Content = "This is a post about programming concepts and best practices."
    };

    var createPostResponse = await HttpUtilities.PostAsync(authorClient, "/api/posts", createPostRequest);
    createPostResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var post = await HttpUtilities.ReadAsAsync<PostResponse>(createPostResponse);

    // Act & Assert - Add Moderation Tag (requires moderator)
    var tagResponse1 = await moderatorClient.PostAsync($"/api/posts/{post.Id}/tags", null);
    tagResponse1.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

    // Act & Assert - Get Tagged Posts (requires moderator)
    var getTaggedPostsResponse = await moderatorClient.GetAsync("/api/posts/tagged");
    getTaggedPostsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var taggedPostsResult = await HttpUtilities.ReadAsAsync<PagedResult<TaggedPost>>(getTaggedPostsResponse);
    taggedPostsResult.Should().NotBeNull();
    taggedPostsResult.Items.Should().Contain(p => p.Id == post.Id);

    // Act & Assert - Remove Tag (requires moderator)
    var removeTagResponse = await moderatorClient.DeleteAsync($"/api/posts/{post.Id}/tags");
    removeTagResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

    // Verify tag was removed by checking tagged posts again
    var getTaggedAfterRemovalResponse = await moderatorClient.GetAsync("/api/posts/tagged");
    getTaggedAfterRemovalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var taggedAfterRemovalResult = await HttpUtilities.ReadAsAsync<PagedResult<TaggedPost>>(getTaggedAfterRemovalResponse);
    taggedAfterRemovalResult.Items.Should().NotContain(p => p.Id == post.Id);
  }

  [Fact]
  public async Task CreatePost_WithInvalidData_ShouldReturnBadRequest()
  {
    // Arrange
    var author = await CreateTestUserAsync();
    var authorClient = CreateAuthenticatedClient(author.Id, author.Username);

    // Test cases for invalid data
    var invalidRequests = new[]
    {
            new CreatePostRequest { Title = "", Content = "Valid content here" }, // Empty title
            new CreatePostRequest { Title = "Valid Title", Content = "" }, // Empty content
            new CreatePostRequest { Title = "Hi", Content = "Valid content" }, // Title too short
            new CreatePostRequest { Title = "Valid Title", Content = "Short" }, // Content too short
            new CreatePostRequest { Title = new string('x', 201), Content = "Valid content" }, // Title too long
            new CreatePostRequest { Title = "Valid Title", Content = new string('x', 10001) } // Content too long
        };

    foreach (var invalidRequest in invalidRequests)
    {
      // Act
      var response = await HttpUtilities.PostAsync(authorClient, "/api/posts", invalidRequest);

      // Assert
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
          $"Request with Title='{(string.IsNullOrEmpty(invalidRequest.Title) ? "null/empty" : invalidRequest.Title.Substring(0, Math.Min(50, invalidRequest.Title.Length)))}...' " +
          $"and Content length={invalidRequest.Content?.Length} should return BadRequest");
    }
  }

  [Fact]
  public async Task CreateComment_OnNonexistentPost_ShouldReturnNotFound()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var userClient = CreateAuthenticatedClient(user.Id, user.Username);

    var commentRequest = new CreateCommentRequest
    {
      Content = "This comment is on a post that doesn't exist."
    };

    // Act
    var response = await HttpUtilities.PostAsync(userClient, "/api/posts/999999/comments", commentRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task LikePost_MultipleTimes_ShouldToggleLike()
  {
    // Arrange
    var author = await CreateTestUserAsync("author", "author@example.com");
    var liker = await CreateTestUserAsync("liker", "liker@example.com");

    var authorClient = CreateAuthenticatedClient(author.Id, author.Username);
    var likerClient = CreateAuthenticatedClient(liker.Id, liker.Username);

    // Create a post
    var post = await CreateTestPostAsync(author.Id, "Test Post", "Content for like testing");

    // Act & Assert - First like should add like
    var firstLikeResponse = await likerClient.PostAsync($"/api/posts/{post.Id}/like", null);
    firstLikeResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

    var postAfterFirstLike = await HttpUtilities.GetAsync<PostResponse>(Client, $"/api/posts/{post.Id}");
    postAfterFirstLike.LikeCount.Should().Be(1);

    // Act & Assert - Second like should remove like (toggle)
    var secondLikeResponse = await likerClient.PostAsync($"/api/posts/{post.Id}/like", null);
    secondLikeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var postAfterSecondLike = await HttpUtilities.GetAsync<PostResponse>(Client, $"/api/posts/{post.Id}");
    postAfterSecondLike.LikeCount.Should().Be(0);
  }

  [Fact]
  public async Task CreateComment_WithInvalidData_ShouldReturnBadRequest()
  {
    // Arrange
    var author = await CreateTestUserAsync();
    var post = await CreateTestPostAsync(author.Id);
    var commenterClient = CreateAuthenticatedClient(author.Id, author.Username);

    var invalidCommentRequests = new[]
    {
            new CreateCommentRequest { Content = "" }, // Empty content
            new CreateCommentRequest { Content = "x" }, // Too short
            new CreateCommentRequest { Content = new string('x', 5001) } // Too long (assuming 5000 char limit)
        };

    foreach (var invalidRequest in invalidCommentRequests)
    {
      // Act
      var response = await HttpUtilities.PostAsync(commenterClient, $"/api/posts/{post.Id}/comments", invalidRequest);

      // Assert
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
          $"Comment with content length {invalidRequest.Content?.Length} should return BadRequest");
    }
  }

  [Fact]
  public async Task MultipleUsersInteraction_ShouldMaintainDataIntegrity()
  {
    // Arrange
    var users = await Task.WhenAll(Enumerable.Range(1, 5).Select(i =>
        CreateTestUserAsync($"user{i}", $"user{i}@example.com")));

    var userClients = users.Select(u => CreateAuthenticatedClient(u.Id, u.Username)).ToArray();

    // Act - Create post with first user
    var post = await CreateTestPostAsync(users[0].Id, "Multi-User Test Post", "Content for testing multiple user interactions");

    // Act - Multiple users comment on the post
    var commentTasks = userClients.Skip(1).Select(async (client, index) =>
    {
      var commentRequest = new CreateCommentRequest
      {
        Content = $"Comment from user {index + 2}: This is a great post!"
      };
      return await HttpUtilities.PostAsync(client, $"/api/posts/{post.Id}/comments", commentRequest);
    });

    var commentResponses = await Task.WhenAll(commentTasks);

    // Act - Multiple users like the post (excluding the author)
    var likeTasks = userClients.Skip(1).Select(client => client.PostAsync($"/api/posts/{post.Id}/like", null));
    var likeResponses = await Task.WhenAll(likeTasks);

    // Act - Author tries to like their own post (should fail)
    var authorLikeResponse = await userClients[0].PostAsync($"/api/posts/{post.Id}/like", null);

    // Assert - All comments should be created successfully
    commentResponses.Should().AllSatisfy(response =>
        response.StatusCode.Should().Be(HttpStatusCode.Created));

    // Assert - Other users' likes should be processed successfully
    likeResponses.Should().AllSatisfy(response =>
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created));

    // Assert - Author's attempt to like own post should fail
    authorLikeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    // Assert - Final state should be correct
    var finalPost = await HttpUtilities.GetAsync<PostResponse>(Client, $"/api/posts/{post.Id}");
    finalPost.LikeCount.Should().Be(4); // Only 4 users liked the post (excluding author)

    var finalCommentsResponse = await Client.GetAsync($"/api/posts/{post.Id}/comments");
    var finalCommentsResult = await HttpUtilities.ReadAsAsync<PagedResult<CommentResponse>>(finalCommentsResponse);
    finalCommentsResult.Items.Should().HaveCount(4); // 4 users commented (excluding post author)
  }
}
