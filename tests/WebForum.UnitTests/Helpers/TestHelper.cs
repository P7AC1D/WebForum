using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;

namespace WebForum.UnitTests.Helpers;

/// <summary>
/// Helper class for creating test objects and mock configurations
/// </summary>
public static class TestHelper
{
  /// <summary>
  /// Creates a mock IConfiguration with test JWT settings
  /// </summary>
  public static Mock<IConfiguration> CreateMockConfiguration()
  {
    var mockConfig = new Mock<IConfiguration>();
    var mockJwtSection = new Mock<IConfigurationSection>();

    // Setup JWT settings
    mockJwtSection.Setup(x => x["SecretKey"]).Returns("TestSecretKeyThatIsAtLeast32CharactersLongForTesting!");
    mockJwtSection.Setup(x => x["Issuer"]).Returns("WebForumTestApi");
    mockJwtSection.Setup(x => x["Audience"]).Returns("WebForumTestUsers");
    mockJwtSection.Setup(x => x["ExpirationInMinutes"]).Returns("60");

    mockConfig.Setup(x => x.GetSection("JwtSettings")).Returns(mockJwtSection.Object);

    return mockConfig;
  }

  /// <summary>
  /// Creates a mock logger for testing
  /// </summary>
  public static Mock<ILogger<T>> CreateMockLogger<T>()
  {
    return new Mock<ILogger<T>>();
  }

  /// <summary>
  /// Creates a test user with default values
  /// </summary>
  public static User CreateTestUser(
      int id = 1,
      string username = "testuser",
      string email = "test@example.com",
      UserRoles role = UserRoles.User)
  {
    return new User
    {
      Id = id,
      Username = username,
      Email = email,
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("testpassword"),
      Role = role,
      CreatedAt = DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Creates a test post with default values
  /// </summary>
  public static Post CreateTestPost(
      int id = 1,
      int authorId = 1,
      string title = "Test Post",
      string content = "Test content")
  {
    return new Post
    {
      Id = id,
      Title = title,
      Content = content,
      AuthorId = authorId,
      CreatedAt = DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Verifies that a logger was called with the expected log level and message
  /// </summary>
  public static void VerifyLog<T>(Mock<ILogger<T>> mockLogger, LogLevel level, string expectedMessage)
  {
    mockLogger.Verify(
        x => x.Log(
            level,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);
  }

  /// <summary>
  /// Creates a valid user for testing
  /// </summary>
  public static User CreateValidUser(
      int id = 1,
      string username = "testuser",
      string email = "test@example.com",
      UserRoles role = UserRoles.User)
  {
    return new User
    {
      Id = id,
      Username = username,
      Email = email,
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("testpassword"),
      Role = role,
      CreatedAt = DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Creates a valid UserEntity for testing
  /// </summary>
  public static UserEntity CreateValidUserEntity(
      int id = 1,
      string username = "testuser",
      string email = "test@example.com",
      UserRoles role = UserRoles.User)
  {
    return new UserEntity
    {
      Id = id,
      Username = username,
      Email = email,
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("testpassword"),
      Role = role,
      CreatedAt = DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Creates a valid PostEntity for testing
  /// </summary>
  public static PostEntity CreateValidPostEntity(
      int id = 1,
      int authorId = 1,
      DateTimeOffset? createdAt = null,
      string title = "Test Post Title")
  {
    return new PostEntity
    {
      Id = id,
      Title = title,
      Content = "This is a test post content with enough characters to meet the minimum requirements.",
      AuthorId = authorId,
      CreatedAt = createdAt ?? DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Creates a valid CommentEntity for testing
  /// </summary>
  public static CommentEntity CreateValidCommentEntity(
      int id = 1,
      int authorId = 1,
      int postId = 1,
      DateTimeOffset? createdAt = null)
  {
    return new CommentEntity
    {
      Id = id,
      Content = "This is a test comment with sufficient content.",
      AuthorId = authorId,
      PostId = postId,
      CreatedAt = createdAt ?? DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Creates a valid RegistrationRequest for testing
  /// </summary>
  public static RegistrationRequest CreateValidRegistrationRequest(
      string username = "testuser",
      string email = "test@example.com",
      string password = "TestPassword123!")
  {
    return new RegistrationRequest
    {
      Username = username,
      Email = email,
      Password = password
    };
  }

  /// <summary>
  /// Creates a valid CreatePostRequest for testing
  /// </summary>
  public static CreatePostRequest CreateValidCreatePostRequest(
      string title = "Valid Test Post Title",
      string content = "This is a valid test post content with enough characters to meet validation requirements.")
  {
    return new CreatePostRequest
    {
      Title = title,
      Content = content
    };
  }

  /// <summary>
  /// Sets up a mock DbContext with queryable DbSets
  /// </summary>
  public static void SetupMockDbContext(
      Mock<ForumDbContext> mockContext,
      List<UserEntity>? users = null,
      List<PostEntity>? posts = null,
      List<CommentEntity>? comments = null,
      List<LikeEntity>? likes = null,
      List<PostTagEntity>? postTags = null)
  {
    users ??= new List<UserEntity>();
    posts ??= new List<PostEntity>();
    comments ??= new List<CommentEntity>();
    likes ??= new List<LikeEntity>();
    postTags ??= new List<PostTagEntity>();

    var mockUserSet = CreateMockDbSet(users);
    var mockPostSet = CreateMockDbSet(posts);
    var mockCommentSet = CreateMockDbSet(comments);
    var mockLikeSet = CreateMockDbSet(likes);
    var mockPostTagSet = CreateMockDbSet(postTags);

    mockContext.Setup(x => x.Users).Returns(mockUserSet.Object);
    mockContext.Setup(x => x.Posts).Returns(mockPostSet.Object);
    mockContext.Setup(x => x.Comments).Returns(mockCommentSet.Object);
    mockContext.Setup(x => x.Likes).Returns(mockLikeSet.Object);
    mockContext.Setup(x => x.PostTags).Returns(mockPostTagSet.Object);

    // Setup SaveChangesAsync to return a successful result
    mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(1);
  }

  /// <summary>
  /// Creates a mock DbSet from a list of entities
  /// </summary>
  private static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
  {
    var queryable = data.AsQueryable();
    var mockDbSet = new Mock<DbSet<T>>();

    mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
    mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
    mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
    mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

    // Setup Add method
    mockDbSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(data.Add);

    return mockDbSet;
  }
}
