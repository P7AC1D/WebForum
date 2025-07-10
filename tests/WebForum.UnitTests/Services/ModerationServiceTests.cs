using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Models.Response;
using WebForum.Api.Services.Implementations;
using WebForum.UnitTests.Helpers;

namespace WebForum.UnitTests.Services;

/// <summary>
/// Unit tests for ModerationService - Critical content moderation and compliance functionality
/// Tests post tagging logic, moderator authorization checks, and compliance features
/// </summary>
public class ModerationServiceTests : IDisposable
{
    private readonly ForumDbContext _context;
    private readonly ModerationService _moderationService;
    private const string MisinformationTag = "misleading or false information";

    public ModerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ForumDbContext(options);
        _moderationService = new ModerationService(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var action = () => new ModerationService(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithValidContext_ShouldCreateInstance()
    {
        // Arrange, Act & Assert
        var service = new ModerationService(_context);
        service.Should().NotBeNull();
    }

    #endregion

    #region TagPostAsync Tests

    [Fact]
    public async Task TagPostAsync_WithValidInputs_ShouldTagPost()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.TagPostAsync(post.Id, moderator.Id);

        // Assert
        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.Action.Should().Be("tagged");
        result.Tag.Should().Be(MisinformationTag);
        result.ModeratorId.Should().Be(moderator.Id);
        result.ModeratorUsername.Should().Be(moderator.Username);
        result.ActionTimestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Verify tag was created in database
        var tagInDb = await _context.PostTags
            .FirstOrDefaultAsync(pt => pt.PostId == post.Id && pt.Tag == MisinformationTag);
        tagInDb.Should().NotBeNull();
        tagInDb!.CreatedByUserId.Should().Be(moderator.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task TagPostAsync_WithInvalidPostId_ShouldThrowArgumentException(int invalidPostId)
    {
        // Arrange
        var moderatorId = 1;

        // Act & Assert
        var action = () => _moderationService.TagPostAsync(invalidPostId, moderatorId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("postId")
            .WithMessage("Post ID must be greater than zero*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task TagPostAsync_WithInvalidModeratorId_ShouldThrowArgumentException(int invalidModeratorId)
    {
        // Arrange
        var postId = 1;

        // Act & Assert
        var action = () => _moderationService.TagPostAsync(postId, invalidModeratorId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("moderatorId")
            .WithMessage("Moderator ID must be greater than zero*");
    }

    [Fact]
    public async Task TagPostAsync_WithNonModeratorUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var regularUser = TestHelper.CreateTestUser(role: UserRoles.User);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(regularUser));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act & Assert
        var action = () => _moderationService.TagPostAsync(post.Id, regularUser.Id);
        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User is not authorized to perform moderation actions");
    }

    [Fact]
    public async Task TagPostAsync_WithNonExistentPost_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.SaveChangesAsync();

        var nonExistentPostId = 99999;

        // Act & Assert
        var action = () => _moderationService.TagPostAsync(nonExistentPostId, moderator.Id);
        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Post with ID {nonExistentPostId} not found");
    }

    [Fact]
    public async Task TagPostAsync_WithAlreadyTaggedPost_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add existing tag
        var existingTag = new PostTagEntity
        {
            PostId = post.Id,
            Tag = MisinformationTag,
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.PostTags.AddAsync(existingTag);
        await _context.SaveChangesAsync();

        // Act & Assert
        var action = () => _moderationService.TagPostAsync(post.Id, moderator.Id);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Post {post.Id} is already tagged as '{MisinformationTag}'");
    }

    [Fact]
    public async Task TagPostAsync_WithNonExistentModerator_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var post = TestHelper.CreateTestPost();
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        var nonExistentModeratorId = 99999;

        // Act & Assert
        // Note: Service checks moderator authorization before existence, so UnauthorizedAccessException is thrown first
        var action = () => _moderationService.TagPostAsync(post.Id, nonExistentModeratorId);
        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User is not authorized to perform moderation actions");
    }

    #endregion

    #region RemoveTagFromPostAsync Tests

    [Fact]
    public async Task RemoveTagFromPostAsync_WithValidInputs_ShouldRemoveTag()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add existing tag
        var existingTag = new PostTagEntity
        {
            PostId = post.Id,
            Tag = MisinformationTag,
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.PostTags.AddAsync(existingTag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.RemoveTagFromPostAsync(post.Id, moderator.Id);

        // Assert
        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.Action.Should().Be("untagged");
        result.Tag.Should().Be(MisinformationTag);
        result.ModeratorId.Should().Be(moderator.Id);
        result.ModeratorUsername.Should().Be(moderator.Username);
        result.ActionTimestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Verify tag was removed from database
        var tagInDb = await _context.PostTags
            .FirstOrDefaultAsync(pt => pt.PostId == post.Id && pt.Tag == MisinformationTag);
        tagInDb.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task RemoveTagFromPostAsync_WithInvalidPostId_ShouldThrowArgumentException(int invalidPostId)
    {
        // Arrange
        var moderatorId = 1;

        // Act & Assert
        var action = () => _moderationService.RemoveTagFromPostAsync(invalidPostId, moderatorId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("postId")
            .WithMessage("Post ID must be greater than zero*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task RemoveTagFromPostAsync_WithInvalidModeratorId_ShouldThrowArgumentException(int invalidModeratorId)
    {
        // Arrange
        var postId = 1;

        // Act & Assert
        var action = () => _moderationService.RemoveTagFromPostAsync(postId, invalidModeratorId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("moderatorId")
            .WithMessage("Moderator ID must be greater than zero*");
    }

    [Fact]
    public async Task RemoveTagFromPostAsync_WithNonModeratorUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var regularUser = TestHelper.CreateTestUser(role: UserRoles.User);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(regularUser));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act & Assert
        var action = () => _moderationService.RemoveTagFromPostAsync(post.Id, regularUser.Id);
        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User is not authorized to perform moderation actions");
    }

    [Fact]
    public async Task RemoveTagFromPostAsync_WithNonExistentPost_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.SaveChangesAsync();

        var nonExistentPostId = 99999;

        // Act & Assert
        var action = () => _moderationService.RemoveTagFromPostAsync(nonExistentPostId, moderator.Id);
        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Post with ID {nonExistentPostId} not found");
    }

    [Fact]
    public async Task RemoveTagFromPostAsync_WithUntaggedPost_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act & Assert (no existing tag)
        var action = () => _moderationService.RemoveTagFromPostAsync(post.Id, moderator.Id);
        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"No '{MisinformationTag}' tag found for post {post.Id}");
    }

    #endregion

    #region RemoveSpecificTagFromPostAsync Tests

    [Fact]
    public async Task RemoveSpecificTagFromPostAsync_WithValidInputs_ShouldRemoveSpecificTag()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();
        var customTag = "custom-moderation-tag";

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add existing tag
        var existingTag = new PostTagEntity
        {
            PostId = post.Id,
            Tag = customTag,
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.PostTags.AddAsync(existingTag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.RemoveSpecificTagFromPostAsync(post.Id, customTag, moderator.Id);

        // Assert
        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.Action.Should().Be("untagged");
        result.Tag.Should().Be(customTag);
        result.ModeratorId.Should().Be(moderator.Id);
        result.ModeratorUsername.Should().Be(moderator.Username);
        result.ActionTimestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Verify specific tag was removed from database
        var tagInDb = await _context.PostTags
            .FirstOrDefaultAsync(pt => pt.PostId == post.Id && pt.Tag == customTag);
        tagInDb.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task RemoveSpecificTagFromPostAsync_WithInvalidPostId_ShouldThrowArgumentException(int invalidPostId)
    {
        // Arrange
        var tagName = "test-tag";
        var moderatorId = 1;

        // Act & Assert
        var action = () => _moderationService.RemoveSpecificTagFromPostAsync(invalidPostId, tagName, moderatorId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("postId")
            .WithMessage("Post ID must be greater than zero*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task RemoveSpecificTagFromPostAsync_WithInvalidTagName_ShouldThrowArgumentException(string invalidTagName)
    {
        // Arrange
        var postId = 1;
        var moderatorId = 1;

        // Act & Assert
        var action = () => _moderationService.RemoveSpecificTagFromPostAsync(postId, invalidTagName, moderatorId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("tagName")
            .WithMessage("Tag name cannot be null or empty*");
    }

    [Fact]
    public async Task RemoveSpecificTagFromPostAsync_WithNullTagName_ShouldThrowArgumentException()
    {
        // Arrange
        var postId = 1;
        var moderatorId = 1;

        // Act & Assert
        var action = () => _moderationService.RemoveSpecificTagFromPostAsync(postId, null!, moderatorId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("tagName")
            .WithMessage("Tag name cannot be null or empty*");
    }

    [Fact]
    public async Task RemoveSpecificTagFromPostAsync_WithNonExistentTag_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();
        var nonExistentTag = "non-existent-tag";

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act & Assert
        var action = () => _moderationService.RemoveSpecificTagFromPostAsync(post.Id, nonExistentTag, moderator.Id);
        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"No '{nonExistentTag}' tag found for post {post.Id}");
    }

    #endregion

    #region GetTaggedPostsAsync Tests

    [Fact]
    public async Task GetTaggedPostsAsync_WithValidInputs_ShouldReturnTaggedPosts()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(id: 100, role: UserRoles.Moderator);
        var author = TestHelper.CreateTestUser(id: 101, username: "author", email: "author@test.com");
        var post1 = TestHelper.CreateTestPost(id: 201, authorId: author.Id, title: "Tagged Post 1");
        var post2 = TestHelper.CreateTestPost(id: 202, authorId: author.Id, title: "Tagged Post 2");
        var untaggedPost = TestHelper.CreateTestPost(id: 203, authorId: author.Id, title: "Untagged Post");

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post1));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post2));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(untaggedPost));

        // Add tags for post1 and post2
        await _context.PostTags.AddAsync(new PostTagEntity
        {
            PostId = post1.Id,
            Tag = MisinformationTag,
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await _context.PostTags.AddAsync(new PostTagEntity
        {
            PostId = post2.Id,
            Tag = MisinformationTag,
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.GetTaggedPostsAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(1);
        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeFalse();

        // Verify posts are ordered by tagged time (most recent first)
        result.Items.First().Title.Should().Be("Tagged Post 2");
        result.Items.Last().Title.Should().Be("Tagged Post 1");

        // Verify tagged post details
        var taggedPost = result.Items.First();
        taggedPost.Id.Should().Be(post2.Id);
        taggedPost.AuthorId.Should().Be(author.Id);
        taggedPost.Tag.Should().Be(MisinformationTag);
        taggedPost.TaggedByUserId.Should().Be(moderator.Id);
        taggedPost.TaggedByUsername.Should().Be(moderator.Username);
    }

    [Fact]
    public async Task GetTaggedPostsAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(id: 110, role: UserRoles.Moderator);
        var author = TestHelper.CreateTestUser(id: 111, username: "author", email: "author@test.com");

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));

        // Create 15 tagged posts
        for (int i = 1; i <= 15; i++)
        {
            var post = TestHelper.CreateTestPost(id: 300 + i, authorId: author.Id, title: $"Tagged Post {i}");
            await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
            await _context.PostTags.AddAsync(new PostTagEntity
            {
                PostId = post.Id,
                Tag = MisinformationTag,
                CreatedByUserId = moderator.Id,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }
        await _context.SaveChangesAsync();

        // Act - Get page 2 with 10 items per page
        var result = await _moderationService.GetTaggedPostsAsync(2, 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5); // 15 total, page 2 should have 5 items
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(15);
        result.TotalPages.Should().Be(2);
        result.HasPrevious.Should().BeTrue();
        result.HasNext.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetTaggedPostsAsync_WithInvalidPage_ShouldThrowArgumentException(int invalidPage)
    {
        // Act & Assert
        var action = () => _moderationService.GetTaggedPostsAsync(invalidPage, 10);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("page")
            .WithMessage("Page must be greater than zero*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)] // Greater than max allowed
    public async Task GetTaggedPostsAsync_WithInvalidPageSize_ShouldThrowArgumentException(int invalidPageSize)
    {
        // Act & Assert
        var action = () => _moderationService.GetTaggedPostsAsync(1, invalidPageSize);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pageSize")
            .WithMessage("Page size must be between 1 and 100*");
    }

    [Fact]
    public async Task GetTaggedPostsAsync_WithNoTaggedPosts_ShouldReturnEmptyResult()
    {
        // Act
        var result = await _moderationService.GetTaggedPostsAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeFalse();
    }

    #endregion

    #region IsPostTaggedAsync Tests

    [Fact]
    public async Task IsPostTaggedAsync_WithTaggedPost_ShouldReturnTrue()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.PostTags.AddAsync(new PostTagEntity
        {
            PostId = post.Id,
            Tag = MisinformationTag,
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.IsPostTaggedAsync(post.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPostTaggedAsync_WithUntaggedPost_ShouldReturnFalse()
    {
        // Arrange
        var post = TestHelper.CreateTestPost();
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.IsPostTaggedAsync(post.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task IsPostTaggedAsync_WithInvalidPostId_ShouldReturnFalse(int invalidPostId)
    {
        // Act
        var result = await _moderationService.IsPostTaggedAsync(invalidPostId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPostTaggedAsync_WithNonExistentPost_ShouldReturnFalse()
    {
        // Act
        var result = await _moderationService.IsPostTaggedAsync(99999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsUserModeratorAsync Tests

    [Fact]
    public async Task IsUserModeratorAsync_WithModeratorUser_ShouldReturnTrue()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.IsUserModeratorAsync(moderator.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserModeratorAsync_WithRegularUser_ShouldReturnFalse()
    {
        // Arrange
        var regularUser = TestHelper.CreateTestUser(role: UserRoles.User);
        await _context.Users.AddAsync(UserEntity.FromDomainModel(regularUser));
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.IsUserModeratorAsync(regularUser.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task IsUserModeratorAsync_WithInvalidUserId_ShouldReturnFalse(int invalidUserId)
    {
        // Act
        var result = await _moderationService.IsUserModeratorAsync(invalidUserId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUserModeratorAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Act
        var result = await _moderationService.IsUserModeratorAsync(99999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetModerationHistoryForPostAsync Tests

    [Fact]
    public async Task GetModerationHistoryForPostAsync_WithTaggedPost_ShouldReturnHistory()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add multiple moderation actions
        await _context.PostTags.AddAsync(new PostTagEntity
        {
            PostId = post.Id,
            Tag = MisinformationTag,
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await _context.PostTags.AddAsync(new PostTagEntity
        {
            PostId = post.Id,
            Tag = "custom-tag",
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.GetModerationHistoryForPostAsync(post.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var historyList = result.ToList();
        // Should be ordered by most recent first
        historyList[0].Tag.Should().Be("custom-tag");
        historyList[1].Tag.Should().Be(MisinformationTag);

        foreach (var historyItem in historyList)
        {
            historyItem.PostId.Should().Be(post.Id);
            historyItem.CreatedByUserId.Should().Be(moderator.Id);
        }
    }

    [Fact]
    public async Task GetModerationHistoryForPostAsync_WithUntaggedPost_ShouldReturnEmptyHistory()
    {
        // Arrange
        var post = TestHelper.CreateTestPost();
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act
        var result = await _moderationService.GetModerationHistoryForPostAsync(post.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetModerationHistoryForPostAsync_WithInvalidPostId_ShouldReturnEmptyHistory(int invalidPostId)
    {
        // Act
        var result = await _moderationService.GetModerationHistoryForPostAsync(invalidPostId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetModerationHistoryForPostAsync_WithNonExistentPost_ShouldReturnEmptyHistory()
    {
        // Act
        var result = await _moderationService.GetModerationHistoryForPostAsync(99999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task ModerationWorkflow_TagAndUntagPost_ShouldWorkCorrectly()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(id: 120, role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost(id: 400);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act 1 - Tag the post
        var tagResult = await _moderationService.TagPostAsync(post.Id, moderator.Id);

        // Assert 1 - Post should be tagged
        tagResult.Action.Should().Be("tagged");
        var isTagged = await _moderationService.IsPostTaggedAsync(post.Id);
        isTagged.Should().BeTrue();

        // Act 2 - Remove the tag
        var untagResult = await _moderationService.RemoveTagFromPostAsync(post.Id, moderator.Id);

        // Assert 2 - Post should be untagged
        untagResult.Action.Should().Be("untagged");
        var isStillTagged = await _moderationService.IsPostTaggedAsync(post.Id);
        isStillTagged.Should().BeFalse();

        // Act 3 - Check history
        var history = await _moderationService.GetModerationHistoryForPostAsync(post.Id);

        // Assert 3 - History should be empty since the tag was removed (physical delete)
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task ModerationWorkflow_MultipleModerators_ShouldWorkIndependently()
    {
        // Arrange
        var moderator1 = TestHelper.CreateTestUser(id: 130, username: "mod1", email: "mod1@test.com", role: UserRoles.Moderator);
        var moderator2 = TestHelper.CreateTestUser(id: 131, username: "mod2", email: "mod2@test.com", role: UserRoles.Moderator);
        var author = TestHelper.CreateTestUser(id: 132, username: "author", email: "author@test.com", role: UserRoles.User);
        var post1 = TestHelper.CreateTestPost(id: 500, authorId: author.Id, title: "Post 1");
        var post2 = TestHelper.CreateTestPost(id: 501, authorId: author.Id, title: "Post 2");

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator1));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator2));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post1));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post2));
        await _context.SaveChangesAsync();

        // Act - Different moderators tag different posts
        var tagResult1 = await _moderationService.TagPostAsync(post1.Id, moderator1.Id);
        var tagResult2 = await _moderationService.TagPostAsync(post2.Id, moderator2.Id);

        // Assert - Both actions should succeed with correct moderator info
        tagResult1.ModeratorId.Should().Be(moderator1.Id);
        tagResult1.ModeratorUsername.Should().Be(moderator1.Username);

        tagResult2.ModeratorId.Should().Be(moderator2.Id);
        tagResult2.ModeratorUsername.Should().Be(moderator2.Username);

        // Both posts should appear in tagged posts list
        var taggedPosts = await _moderationService.GetTaggedPostsAsync(1, 10);
        taggedPosts.Items.Should().HaveCount(2);
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [Fact]
    public async Task ModerationService_WithDifferentTags_ShouldOnlyRecognizeMisinformationTag()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add a different tag
        await _context.PostTags.AddAsync(new PostTagEntity
        {
            PostId = post.Id,
            Tag = "some-other-tag",
            CreatedByUserId = moderator.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var isTagged = await _moderationService.IsPostTaggedAsync(post.Id);
        var taggedPosts = await _moderationService.GetTaggedPostsAsync(1, 10);

        // Assert - Should not recognize the other tag as misinformation tag
        isTagged.Should().BeFalse();
        taggedPosts.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ModerationService_ConcurrentTagging_ShouldHandleGracefully()
    {
        // Arrange
        var moderator = TestHelper.CreateTestUser(role: UserRoles.Moderator);
        var post = TestHelper.CreateTestPost();

        await _context.Users.AddAsync(UserEntity.FromDomainModel(moderator));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act 1 - Tag the post
        await _moderationService.TagPostAsync(post.Id, moderator.Id);

        // Act 2 - Try to tag again (simulating concurrent request)
        var action = () => _moderationService.TagPostAsync(post.Id, moderator.Id);

        // Assert - Should throw exception for duplicate tagging
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Post {post.Id} is already tagged as '{MisinformationTag}'");
    }

    #endregion
}
