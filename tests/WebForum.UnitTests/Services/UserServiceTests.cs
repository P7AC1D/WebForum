using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Services.Implementations;
using WebForum.UnitTests.Helpers;
using Xunit;

namespace WebForum.UnitTests.Services;

public class UserServiceTests : IDisposable
{
  private readonly ForumDbContext _context;
  private readonly UserService _userService;

  public UserServiceTests()
  {
    var options = new DbContextOptionsBuilder<ForumDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    _context = new ForumDbContext(options);
    _userService = new UserService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
  }

  #region Constructor Tests

  [Fact]
  public void Constructor_WithNullContext_ThrowsArgumentNullException()
  {
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() =>
        new UserService(null!));
  }

  #endregion

  #region GetUserProfileAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetUserProfileAsync_WithInvalidUserId_ThrowsArgumentException(int userId)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _userService.GetUserProfileAsync(userId));

    exception.ParamName.Should().Be("userId");
    exception.Message.Should().Contain("User ID must be greater than zero");
  }

  [Fact]
  public async Task GetUserProfileAsync_WithNonExistentUser_ThrowsKeyNotFoundException()
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
        _userService.GetUserProfileAsync(999));

    exception.Message.Should().Contain("User with ID 999 not found");
  }

  [Fact]
  public async Task GetUserProfileAsync_WithExistingUser_ReturnsUserInfoWithStatistics()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1, "testuser", "test@example.com");
    _context.Users.Add(user);

    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1),
            TestHelper.CreateValidPostEntity(2, 1)
        };
    _context.Posts.AddRange(posts);

    var comments = new List<CommentEntity>
        {
            TestHelper.CreateValidCommentEntity(1, 1, 1),
            TestHelper.CreateValidCommentEntity(2, 1, 2),
            TestHelper.CreateValidCommentEntity(3, 1, 1)
        };
    _context.Comments.AddRange(comments);

    var likes = new List<LikeEntity>
        {
            new() { Id = 1, PostId = 1, UserId = 2 },
            new() { Id = 2, PostId = 2, UserId = 3 },
            new() { Id = 3, PostId = 1, UserId = 3 }
        };
    _context.Likes.AddRange(likes);

    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserProfileAsync(1);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(1);
    result.Username.Should().Be("testuser");
    result.Email.Should().BeNull(); // Public profiles exclude email for privacy
    result.PostCount.Should().Be(2);
    result.CommentCount.Should().Be(3);
    result.LikesReceived.Should().Be(3);
  }

  #endregion

  #region GetUserPostsAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetUserPostsAsync_WithInvalidUserId_ThrowsArgumentException(int userId)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _userService.GetUserPostsAsync(userId, 1, 10, "desc"));

    exception.ParamName.Should().Be("userId");
    exception.Message.Should().Contain("User ID must be greater than zero");
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetUserPostsAsync_WithInvalidPage_ThrowsArgumentException(int page)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _userService.GetUserPostsAsync(1, page, 10, "desc"));

    exception.ParamName.Should().Be("page");
    exception.Message.Should().Contain("Page must be greater than zero");
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(101)]
  public async Task GetUserPostsAsync_WithInvalidPageSize_ThrowsArgumentException(int pageSize)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _userService.GetUserPostsAsync(1, 1, pageSize, "desc"));

    exception.ParamName.Should().Be("pageSize");
    exception.Message.Should().Contain("Page size must be between 1 and 100");
  }

  [Fact]
  public async Task GetUserPostsAsync_WithNonExistentUser_ThrowsKeyNotFoundException()
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
        _userService.GetUserPostsAsync(999, 1, 10, "desc"));

    exception.Message.Should().Contain("User with ID 999 not found");
  }

  [Fact]
  public async Task GetUserPostsAsync_WithValidParameters_ReturnsUserPosts()
  {
    // Arrange
    var users = new List<UserEntity>
        {
            TestHelper.CreateValidUserEntity(1),
            TestHelper.CreateValidUserEntity(2)
        };
    _context.Users.AddRange(users);

    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1, DateTimeOffset.UtcNow.AddDays(-2)),
            TestHelper.CreateValidPostEntity(2, 2, DateTimeOffset.UtcNow.AddDays(-1)),
            TestHelper.CreateValidPostEntity(3, 1, DateTimeOffset.UtcNow)
        };
    _context.Posts.AddRange(posts);

    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserPostsAsync(1, 1, 10, "desc");

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(2);
    result.Items.Should().OnlyContain(p => p.AuthorId == 1);
    result.Page.Should().Be(1);
    result.PageSize.Should().Be(10);
    result.TotalCount.Should().Be(2);
  }

  [Theory]
  [InlineData("desc")]
  [InlineData("newest")]
  [InlineData("asc")]
  [InlineData("oldest")]
  public async Task GetUserPostsAsync_WithDifferentSortOrders_SortsCorrectly(string sortOrder)
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1);
    _context.Users.Add(user);

    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1, DateTimeOffset.UtcNow.AddDays(-2)),
            TestHelper.CreateValidPostEntity(2, 1, DateTimeOffset.UtcNow.AddDays(-1)),
            TestHelper.CreateValidPostEntity(3, 1, DateTimeOffset.UtcNow)
        };
    _context.Posts.AddRange(posts);

    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserPostsAsync(1, 1, 10, sortOrder);

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(3);
  }

  [Fact]
  public async Task GetUserPostsAsync_WithPagination_ReturnsCorrectPage()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1);
    _context.Users.Add(user);

    var posts = new List<PostEntity>();
    for (int i = 1; i <= 15; i++)
    {
      posts.Add(TestHelper.CreateValidPostEntity(i, 1, DateTimeOffset.UtcNow.AddDays(-i)));
    }
    _context.Posts.AddRange(posts);

    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserPostsAsync(1, 2, 5, "desc");

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(5);
    result.Page.Should().Be(2);
    result.PageSize.Should().Be(5);
    result.TotalCount.Should().Be(15);
    result.TotalPages.Should().Be(3);
    result.HasPrevious.Should().BeTrue();
    result.HasNext.Should().BeTrue();
  }

  #endregion

  #region UserExistsAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task UserExistsAsync_WithInvalidUserId_ReturnsFalse(int userId)
  {
    // Act
    var result = await _userService.UserExistsAsync(userId);

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public async Task UserExistsAsync_WithNonExistentUser_ReturnsFalse()
  {
    // Act
    var result = await _userService.UserExistsAsync(999);

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public async Task UserExistsAsync_WithExistingUser_ReturnsTrue()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1);
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.UserExistsAsync(1);

    // Assert
    result.Should().BeTrue();
  }

  #endregion

  #region GetUserByIdAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetUserByIdAsync_WithInvalidUserId_ReturnsNull(int userId)
  {
    // Act
    var result = await _userService.GetUserByIdAsync(userId);

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public async Task GetUserByIdAsync_WithNonExistentUser_ReturnsNull()
  {
    // Act
    var result = await _userService.GetUserByIdAsync(999);

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public async Task GetUserByIdAsync_WithExistingUser_ReturnsUser()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1, "testuser", "test@example.com");
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserByIdAsync(1);

    // Assert
    result.Should().NotBeNull();
    result!.Id.Should().Be(1);
    result.Username.Should().Be("testuser");
    result.Email.Should().Be("test@example.com");
  }

  #endregion

  #region GetUserByEmailAsync Tests

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task GetUserByEmailAsync_WithEmptyEmail_ReturnsNull(string email)
  {
    // Act
    var result = await _userService.GetUserByEmailAsync(email);

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public async Task GetUserByEmailAsync_WithNonExistentEmail_ReturnsNull()
  {
    // Act
    var result = await _userService.GetUserByEmailAsync("nonexistent@example.com");

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public async Task GetUserByEmailAsync_WithExistingEmail_ReturnsUser()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1, "testuser", "test@example.com");
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserByEmailAsync("test@example.com");

    // Assert
    result.Should().NotBeNull();
    result!.Id.Should().Be(1);
    result.Username.Should().Be("testuser");
    result.Email.Should().Be("test@example.com");
  }

  [Fact]
  public async Task GetUserByEmailAsync_WithDifferentCase_ReturnsUser()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1, "testuser", "test@example.com");
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserByEmailAsync("TEST@EXAMPLE.COM");

    // Assert
    result.Should().NotBeNull();
    result!.Email.Should().Be("test@example.com");
  }

  #endregion

  #region GetUserByUsernameAsync Tests

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task GetUserByUsernameAsync_WithEmptyUsername_ReturnsNull(string username)
  {
    // Act
    var result = await _userService.GetUserByUsernameAsync(username);

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public async Task GetUserByUsernameAsync_WithNonExistentUsername_ReturnsNull()
  {
    // Act
    var result = await _userService.GetUserByUsernameAsync("nonexistent");

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public async Task GetUserByUsernameAsync_WithExistingUsername_ReturnsUser()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1, "testuser", "test@example.com");
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserByUsernameAsync("testuser");

    // Assert
    result.Should().NotBeNull();
    result!.Id.Should().Be(1);
    result.Username.Should().Be("testuser");
    result.Email.Should().Be("test@example.com");
  }

  [Fact]
  public async Task GetUserByUsernameAsync_WithDifferentCase_ReturnsUser()
  {
    // Arrange
    var user = TestHelper.CreateValidUserEntity(1, "testuser", "test@example.com");
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUserByUsernameAsync("TESTUSER");

    // Assert
    result.Should().NotBeNull();
    result!.Username.Should().Be("testuser");
  }

  #endregion

  #region GetUsernamesByIdsAsync Tests

  [Fact]
  public async Task GetUsernamesByIdsAsync_WithNullUserIds_ReturnsEmptyDictionary()
  {
    // Act
    var result = await _userService.GetUsernamesByIdsAsync(null!);

    // Assert
    result.Should().NotBeNull();
    result.Should().BeEmpty();
  }

  [Fact]
  public async Task GetUsernamesByIdsAsync_WithEmptyUserIds_ReturnsEmptyDictionary()
  {
    // Act
    var result = await _userService.GetUsernamesByIdsAsync(new List<int>());

    // Assert
    result.Should().NotBeNull();
    result.Should().BeEmpty();
  }

  [Fact]
  public async Task GetUsernamesByIdsAsync_WithInvalidUserIds_ReturnsEmptyDictionary()
  {
    // Act
    var result = await _userService.GetUsernamesByIdsAsync(new List<int> { 0, -1, -5 });

    // Assert
    result.Should().NotBeNull();
    result.Should().BeEmpty();
  }

  [Fact]
  public async Task GetUsernamesByIdsAsync_WithValidUserIds_ReturnsUsernameDictionary()
  {
    // Arrange
    var users = new List<UserEntity>
        {
            TestHelper.CreateValidUserEntity(1, "user1", "user1@example.com"),
            TestHelper.CreateValidUserEntity(2, "user2", "user2@example.com"),
            TestHelper.CreateValidUserEntity(3, "user3", "user3@example.com")
        };
    _context.Users.AddRange(users);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUsernamesByIdsAsync(new List<int> { 1, 2, 3 });

    // Assert
    result.Should().NotBeNull();
    result.Should().HaveCount(3);
    result[1].Should().Be("user1");
    result[2].Should().Be("user2");
    result[3].Should().Be("user3");
  }

  [Fact]
  public async Task GetUsernamesByIdsAsync_WithMixedValidAndInvalidUserIds_ReturnsOnlyValidUsers()
  {
    // Arrange
    var users = new List<UserEntity>
        {
            TestHelper.CreateValidUserEntity(1, "user1", "user1@example.com"),
            TestHelper.CreateValidUserEntity(3, "user3", "user3@example.com")
        };
    _context.Users.AddRange(users);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUsernamesByIdsAsync(new List<int> { 1, 2, 3, 999 });

    // Assert
    result.Should().NotBeNull();
    result.Should().HaveCount(2);
    result[1].Should().Be("user1");
    result[3].Should().Be("user3");
    result.Should().NotContainKey(2);
    result.Should().NotContainKey(999);
  }

  [Fact]
  public async Task GetUsernamesByIdsAsync_WithDuplicateUserIds_ReturnsDistinctUsers()
  {
    // Arrange
    var users = new List<UserEntity>
        {
            TestHelper.CreateValidUserEntity(1, "user1", "user1@example.com"),
            TestHelper.CreateValidUserEntity(2, "user2", "user2@example.com")
        };
    _context.Users.AddRange(users);
    await _context.SaveChangesAsync();

    // Act
    var result = await _userService.GetUsernamesByIdsAsync(new List<int> { 1, 2, 1, 2, 1 });

    // Assert
    result.Should().NotBeNull();
    result.Should().HaveCount(2);
    result[1].Should().Be("user1");
    result[2].Should().Be("user2");
  }

  #endregion
}
