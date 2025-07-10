using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;

namespace WebForum.IntegrationTests.Framework;

/// <summary>
/// Framework validation tests to ensure the integration test infrastructure works correctly
/// </summary>
public class FrameworkValidationTests : IntegrationTestBase
{
  public FrameworkValidationTests(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public void TestFactory_ShouldInitializeSuccessfully()
  {
    // Arrange & Act
    // Assert
    Factory.Should().NotBeNull();
    Factory.ConnectionString.Should().NotBeNullOrEmpty();
    Factory.ConnectionString.Should().Contain("webforum_test");
  }

  [Fact]
  public async Task Database_ShouldCreateAndConnect()
  {
    // Arrange & Act
    using var dbContext = GetDbContext();

    // Assert
    var canConnect = await dbContext.Database.CanConnectAsync();
    canConnect.Should().BeTrue();
  }

  [Fact]
  public async Task Database_ShouldApplyMigrations()
  {
    // Arrange & Act
    await Factory.EnsureDatabaseCreatedAsync();
    using var dbContext = GetDbContext();

    // Assert
    // Check if tables exist by querying them
    var usersTableExists = await dbContext.Users.AnyAsync() || !await dbContext.Users.AnyAsync();
    var postsTableExists = await dbContext.Posts.AnyAsync() || !await dbContext.Posts.AnyAsync();
    var commentsTableExists = await dbContext.Comments.AnyAsync() || !await dbContext.Comments.AnyAsync();
    var likesTableExists = await dbContext.Likes.AnyAsync() || !await dbContext.Likes.AnyAsync();

    usersTableExists.Should().BeTrue();
    postsTableExists.Should().BeTrue();
    commentsTableExists.Should().BeTrue();
    likesTableExists.Should().BeTrue();
  }

  [Fact]
  public async Task TestData_ShouldGenerateSuccessfully()
  {
    // Arrange
    // Act
    var testData = await SeedTestDataAsync(userCount: 3, postCount: 5, commentCount: 8, likeCount: 6);

    // Assert
    testData.Should().NotBeNull();
    testData.Users.Should().HaveCount(3);
    testData.Posts.Should().HaveCount(5);
    testData.Comments.Should().HaveCount(8);
    testData.Likes.Should().HaveCount(6);

    // Verify data in database
    using var dbContext = GetDbContext();
    var userCount = await dbContext.Users.CountAsync();
    var postCount = await dbContext.Posts.CountAsync();
    var commentCount = await dbContext.Comments.CountAsync();
    var likeCount = await dbContext.Likes.CountAsync();

    userCount.Should().Be(3);
    postCount.Should().Be(5);
    commentCount.Should().Be(8);
    likeCount.Should().Be(6);
  }

  [Fact]
  public void Authentication_ShouldGenerateValidTokens()
  {
    // Arrange
    var userId = 1;
    var username = "testuser";
    var roles = WebForum.Api.Models.UserRoles.User;

    // Act
    var token = TestAuthenticationHelper.GenerateJwtToken(userId, username, "test@example.com", roles);
    var authClient = CreateAuthenticatedClient(userId, username, roles);

    // Assert
    token.Should().NotBeNullOrEmpty();
    authClient.Should().NotBeNull();
    authClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
    authClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
    authClient.DefaultRequestHeaders.Authorization.Parameter.Should().NotBeNullOrEmpty();
  }

  [Fact]
  public async Task Database_ShouldCleanSuccessfully()
  {
    // Arrange
    await SeedTestDataAsync(userCount: 2, postCount: 3);

    using (var dbContext = GetDbContext())
    {
      var initialUserCount = await dbContext.Users.CountAsync();
      var initialPostCount = await dbContext.Posts.CountAsync();

      initialUserCount.Should().Be(2);
      initialPostCount.Should().Be(3);
    }

    // Act
    await Factory.CleanDatabaseAsync();

    // Assert
    using var cleanDbContext = GetDbContext();
    var userCount = await cleanDbContext.Users.CountAsync();
    var postCount = await cleanDbContext.Posts.CountAsync();
    var commentCount = await cleanDbContext.Comments.CountAsync();
    var likeCount = await cleanDbContext.Likes.CountAsync();

    userCount.Should().Be(0);
    postCount.Should().Be(0);
    commentCount.Should().Be(0);
    likeCount.Should().Be(0);
  }

  [Fact]
  public async Task HttpClient_ShouldMakeSuccessfulRequests()
  {
    // Arrange
    // Act
    var response = await Client.GetAsync("/api/posts");

    // Assert
    AssertSuccessStatusCode(response);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().NotBeNullOrEmpty();
  }

  [Fact]
  public async Task TestUsers_ShouldCreateWithKnownCredentials()
  {
    // Arrange
    // Act
    var user1 = await CreateTestUserAsync("testuser1", "test1@example.com", WebForum.Api.Models.UserRoles.User);
    var user2 = await CreateTestUserAsync("testmod", "mod@example.com", WebForum.Api.Models.UserRoles.User | WebForum.Api.Models.UserRoles.Moderator);

    // Assert
    user1.Should().NotBeNull();
    user1.Username.Should().Be("testuser1");
    user1.Email.Should().Be("test1@example.com");
    user1.Role.Should().Be(WebForum.Api.Models.UserRoles.User);

    user2.Should().NotBeNull();
    user2.Username.Should().Be("testmod");
    user2.Role.Should().Be(WebForum.Api.Models.UserRoles.User | WebForum.Api.Models.UserRoles.Moderator);
  }

  [Fact]
  public async Task TestPosts_ShouldCreateWithKnownData()
  {
    // Arrange
    var user = await CreateTestUserAsync();

    // Act
    var post = await CreateTestPostAsync(user.Id, "Integration Test Post", "This is a test post for integration testing.");

    // Assert
    post.Should().NotBeNull();
    post.Title.Should().Be("Integration Test Post");
    post.Content.Should().Be("This is a test post for integration testing.");
    post.AuthorId.Should().Be(user.Id);
  }

  [Fact]
  public async Task TestComments_ShouldCreateWithKnownData()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var post = await CreateTestPostAsync(user.Id);

    // Act
    var comment = await CreateTestCommentAsync(post.Id, user.Id, "This is a test comment for integration testing.");

    // Assert
    comment.Should().NotBeNull();
    comment.Content.Should().Be("This is a test comment for integration testing.");
    comment.PostId.Should().Be(post.Id);
    comment.AuthorId.Should().Be(user.Id);
  }

  [Fact]
  public async Task MultipleTests_ShouldIsolateDataCorrectly()
  {
    // Arrange
    // Act & Assert - First test operation
    var testData1 = await SeedTestDataAsync(userCount: 2);
    using (var dbContext1 = GetDbContext())
    {
      var userCount1 = await dbContext1.Users.CountAsync();
      userCount1.Should().Be(2);
    }

    // Clean database
    // Act & Assert - Second test operation
    var testData2 = await SeedTestDataAsync(userCount: 3);
    using var dbContext2 = GetDbContext();
    var userCount2 = await dbContext2.Users.CountAsync();
    userCount2.Should().Be(3);
  }
}
