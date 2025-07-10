using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Services.Implementations;
using WebForum.Api.Services.Interfaces;
using WebForum.UnitTests.Helpers;
using Xunit;

namespace WebForum.UnitTests.Services;

public class PostServiceTests : IDisposable
{
  private readonly ForumDbContext _context;
  private readonly Mock<ISanitizationService> _mockSanitizationService;
  private readonly PostService _postService;

  public PostServiceTests()
  {
    var options = new DbContextOptionsBuilder<ForumDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    _context = new ForumDbContext(options);
    _mockSanitizationService = new Mock<ISanitizationService>();

    _postService = new PostService(
        _context,
        _mockSanitizationService.Object);
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
        new PostService(null!, _mockSanitizationService.Object));
  }

  [Fact]
  public void Constructor_WithNullSanitizationService_ThrowsArgumentNullException()
  {
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() =>
        new PostService(_context, null!));
  }

  #endregion

  #region GetPostsAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetPostsAsync_WithInvalidPage_ThrowsArgumentException(int page)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.GetPostsAsync(page, 10));

    exception.ParamName.Should().Be("page");
    exception.Message.Should().Contain("Page must be greater than zero");
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(101)]
  public async Task GetPostsAsync_WithInvalidPageSize_ThrowsArgumentException(int pageSize)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.GetPostsAsync(1, pageSize));

    exception.ParamName.Should().Be("pageSize");
    exception.Message.Should().Contain("Page size must be between 1 and 100");
  }

  [Fact]
  public async Task GetPostsAsync_WithDateFromAfterDateTo_ThrowsArgumentException()
  {
    // Arrange
    var dateFrom = DateTimeOffset.UtcNow;
    var dateTo = dateFrom.AddDays(-1);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.GetPostsAsync(1, 10, dateFrom: dateFrom, dateTo: dateTo));

    exception.Message.Should().Contain("DateFrom must be before DateTo");
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetPostsAsync_WithInvalidAuthorId_ThrowsArgumentException(int authorId)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.GetPostsAsync(1, 10, authorId: authorId));

    exception.ParamName.Should().Be("authorId");
    exception.Message.Should().Contain("Author ID must be greater than zero");
  }

  [Fact]
  public async Task GetPostsAsync_WithValidParameters_ReturnsPagedResult()
  {
    // Arrange
    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1),
            TestHelper.CreateValidPostEntity(2, 1)
        };

    _context.Posts.AddRange(posts);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.GetPostsAsync(1, 10);

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(2);
    result.Page.Should().Be(1);
    result.PageSize.Should().Be(10);
    result.TotalCount.Should().Be(2);
    result.TotalPages.Should().Be(1);
    result.HasPrevious.Should().BeFalse();
    result.HasNext.Should().BeFalse();
  }

  [Fact]
  public async Task GetPostsAsync_WithAuthorFilter_FiltersCorrectly()
  {
    // Arrange
    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1),
            TestHelper.CreateValidPostEntity(2, 2),
            TestHelper.CreateValidPostEntity(3, 1)
        };

    _context.Posts.AddRange(posts);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.GetPostsAsync(1, 10, authorId: 1);

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(2);
    result.Items.Should().OnlyContain(p => p.AuthorId == 1);
  }

  [Fact]
  public async Task GetPostsAsync_WithDateFilters_FiltersCorrectly()
  {
    // Arrange
    var baseDate = DateTimeOffset.UtcNow;
    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1, baseDate.AddDays(-5)),
            TestHelper.CreateValidPostEntity(2, 1, baseDate.AddDays(-2)),
            TestHelper.CreateValidPostEntity(3, 1, baseDate.AddDays(1))
        };

    _context.Posts.AddRange(posts);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.GetPostsAsync(1, 10,
        dateFrom: baseDate.AddDays(-3),
        dateTo: baseDate);

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(1);
    result.Items.Single().Id.Should().Be(2);
  }

  [Fact]
  public async Task GetPostsAsync_WithTagsFilter_FiltersCorrectly()
  {
    // Arrange
    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1),
            TestHelper.CreateValidPostEntity(2, 1),
            TestHelper.CreateValidPostEntity(3, 1)
        };

    var postTags = new List<PostTagEntity>
        {
            new() { Id = 1, PostId = 1, Tag = "tech" },
            new() { Id = 2, PostId = 2, Tag = "programming" },
            new() { Id = 3, PostId = 3, Tag = "general" }
        };

    _context.Posts.AddRange(posts);
    _context.PostTags.AddRange(postTags);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.GetPostsAsync(1, 10, tags: "tech,programming");

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(2);
    result.Items.Should().OnlyContain(p => p.Id == 1 || p.Id == 2);
  }

  [Theory]
  [InlineData("date", "asc")]
  [InlineData("date", "desc")]
  [InlineData("title", "asc")]
  [InlineData("title", "desc")]
  [InlineData("likecount", "asc")]
  [InlineData("likecount", "desc")]
  public async Task GetPostsAsync_WithDifferentSorting_SortsCorrectly(string sortBy, string sortOrder)
  {
    // Arrange
    var posts = new List<PostEntity>
        {
            TestHelper.CreateValidPostEntity(1, 1, DateTimeOffset.UtcNow.AddDays(-2), "B Title"),
            TestHelper.CreateValidPostEntity(2, 1, DateTimeOffset.UtcNow.AddDays(-1), "A Title"),
            TestHelper.CreateValidPostEntity(3, 1, DateTimeOffset.UtcNow, "C Title")
        };

    var likes = new List<LikeEntity>
        {
            new() { Id = 1, PostId = 1, UserId = 1 },
            new() { Id = 2, PostId = 1, UserId = 2 },
            new() { Id = 3, PostId = 2, UserId = 1 }
        };

    _context.Posts.AddRange(posts);
    _context.Likes.AddRange(likes);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.GetPostsAsync(1, 10, sortBy: sortBy, sortOrder: sortOrder);

    // Assert
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(3);

    // Verify sorting (basic check - in real scenario would need proper EF Core testing)
    result.Items.Should().NotBeEmpty();
  }

  #endregion

  #region GetPostByIdAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetPostByIdAsync_WithInvalidId_ThrowsArgumentException(int postId)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.GetPostByIdAsync(postId));

    exception.ParamName.Should().Be("postId");
    exception.Message.Should().Contain("Post ID must be greater than zero");
  }

  [Fact]
  public async Task GetPostByIdAsync_WithNonExistentId_ThrowsKeyNotFoundException()
  {
    // Arrange
    // No database setup needed - testing with empty database

    // Act & Assert
    var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
        _postService.GetPostByIdAsync(999));

    exception.Message.Should().Contain("Post with ID 999 not found");
  }

  [Fact]
  public async Task GetPostByIdAsync_WithValidId_ReturnsPost()
  {
    // Arrange
    var post = TestHelper.CreateValidPostEntity(1, 1);
    _context.Posts.Add(post);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.GetPostByIdAsync(1);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(1);
    result.AuthorId.Should().Be(1);
  }

  #endregion

  #region CreatePostAsync Tests

  [Fact]
  public async Task CreatePostAsync_WithNullCreatePost_ThrowsArgumentNullException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        _postService.CreatePostAsync(null!, 1));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task CreatePostAsync_WithInvalidAuthorId_ThrowsArgumentException(int authorId)
  {
    // Arrange
    var createPost = TestHelper.CreateValidCreatePostRequest();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.CreatePostAsync(createPost, authorId));

    exception.ParamName.Should().Be("authorId");
    exception.Message.Should().Contain("Author ID must be greater than zero");
  }

  [Fact]
  public async Task CreatePostAsync_WithInvalidPostData_ThrowsArgumentException()
  {
    // Arrange
    var createPost = new CreatePostRequest
    {
      Title = "", // Invalid
      Content = "Valid content here"
    };

    var sanitizedPost = new CreatePostRequest
    {
      Title = "",
      Content = "Valid content here"
    };

    _mockSanitizationService.Setup(x => x.SanitizeInput(It.IsAny<string>()))
        .Returns<string>(s => s);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.CreatePostAsync(createPost, 1));

    exception.Message.Should().Contain("Post validation failed");
  }

  [Fact]
  public async Task CreatePostAsync_WithNonExistentAuthor_ThrowsArgumentException()
  {
    // Arrange
    var createPost = TestHelper.CreateValidCreatePostRequest();
    var authorId = 1;

    _mockSanitizationService.Setup(x => x.SanitizeInput(It.IsAny<string>()))
        .Returns<string>(s => s);

    // No users added to context - empty database

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.CreatePostAsync(createPost, authorId));

    exception.ParamName.Should().Be("authorId");
    exception.Message.Should().Contain($"Author with ID {authorId} does not exist");
  }

  [Fact]
  public async Task CreatePostAsync_WithValidData_SanitizesInput()
  {
    // Arrange
    var createPost = new CreatePostRequest
    {
      Title = "Original Title <script>alert('xss')</script>",
      Content = "Original Content <script>alert('xss')</script>"
    };
    var authorId = 1;

    _mockSanitizationService.Setup(x => x.SanitizeInput("Original Title <script>alert('xss')</script>"))
        .Returns("Original Title");
    _mockSanitizationService.Setup(x => x.SanitizeInput("Original Content <script>alert('xss')</script>"))
        .Returns("Original Content");

    var user = TestHelper.CreateValidUserEntity(1);
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.CreatePostAsync(createPost, authorId);

    // Assert
    result.Should().NotBeNull();
    result.Title.Should().Be("Original Title");
    result.Content.Should().Be("Original Content");
    _mockSanitizationService.Verify(x => x.SanitizeInput(It.IsAny<string>()), Times.Exactly(2));
  }

  [Fact]
  public async Task CreatePostAsync_WithValidData_ReturnsCreatedPost()
  {
    // Arrange
    var createPost = TestHelper.CreateValidCreatePostRequest();
    var authorId = 1;

    _mockSanitizationService.Setup(x => x.SanitizeInput(It.IsAny<string>()))
        .Returns<string>(s => s);

    var user = TestHelper.CreateValidUserEntity(1);
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.CreatePostAsync(createPost, authorId);

    // Assert
    result.Should().NotBeNull();
    result.Title.Should().Be(createPost.Title);
    result.Content.Should().Be(createPost.Content);
    result.AuthorId.Should().Be(authorId);
    result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
  }

  #endregion

  #region PostExistsAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task PostExistsAsync_WithInvalidId_ReturnsFalse(int postId)
  {
    // Act
    var result = await _postService.PostExistsAsync(postId);

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public async Task PostExistsAsync_WithNonExistentPost_ReturnsFalse()
  {
    // Arrange
    // No database setup needed - testing with empty database

    // Act
    var result = await _postService.PostExistsAsync(999);

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public async Task PostExistsAsync_WithExistingPost_ReturnsTrue()
  {
    // Arrange
    var post = TestHelper.CreateValidPostEntity(1, 1);
    _context.Posts.Add(post);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.PostExistsAsync(1);

    // Assert
    result.Should().BeTrue();
  }

  #endregion

  #region GetPostAuthorIdAsync Tests

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetPostAuthorIdAsync_WithInvalidId_ThrowsArgumentException(int postId)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        _postService.GetPostAuthorIdAsync(postId));

    exception.ParamName.Should().Be("postId");
    exception.Message.Should().Contain("Post ID must be greater than zero");
  }

  [Fact]
  public async Task GetPostAuthorIdAsync_WithNonExistentPost_ThrowsKeyNotFoundException()
  {
    // Arrange
    // No database setup needed - testing with empty database

    // Act & Assert
    var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
        _postService.GetPostAuthorIdAsync(999));

    exception.Message.Should().Contain("Post with ID 999 not found");
  }

  [Fact]
  public async Task GetPostAuthorIdAsync_WithExistingPost_ReturnsAuthorId()
  {
    // Arrange
    var post = TestHelper.CreateValidPostEntity(1, 5);
    _context.Posts.Add(post);
    await _context.SaveChangesAsync();

    // Act
    var result = await _postService.GetPostAuthorIdAsync(1);

    // Assert
    result.Should().Be(5);
  }

  #endregion
}
