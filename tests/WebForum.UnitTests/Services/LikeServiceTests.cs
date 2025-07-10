using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Services.Implementations;
using WebForum.UnitTests.Helpers;

namespace WebForum.UnitTests.Services;

/// <summary>
/// Unit tests for LikeService - User engagement features for post likes
/// Tests like/unlike operations, validation logic, and engagement statistics
/// </summary>
public class LikeServiceTests : IDisposable
{
    private readonly ForumDbContext _context;
    private readonly LikeService _likeService;

    public LikeServiceTests()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ForumDbContext(options);
        _likeService = new LikeService(_context);
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
        var action = () => new LikeService(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithValidContext_ShouldCreateInstance()
    {
        // Arrange, Act & Assert
        var service = new LikeService(_context);
        service.Should().NotBeNull();
    }

    #endregion

    #region ToggleLikeAsync Tests

    [Fact]
    public async Task ToggleLikeAsync_WithValidInputs_ShouldLikePost()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100, username: "author", email: "author@test.com");
        var user = TestHelper.CreateTestUser(id: 101, username: "user", email: "user@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.ToggleLikeAsync(post.Id, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.IsLiked.Should().BeTrue();
        result.LikeCount.Should().Be(1);

        // Verify like was created in database
        var likeInDb = await _context.Likes
            .FirstOrDefaultAsync(l => l.PostId == post.Id && l.UserId == user.Id);
        likeInDb.Should().NotBeNull();
        likeInDb!.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ToggleLikeAsync_WithExistingLike_ShouldUnlikePost()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100, username: "author", email: "author@test.com");
        var user = TestHelper.CreateTestUser(id: 101, username: "user", email: "user@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add existing like
        var existingLike = new LikeEntity
        {
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        await _context.Likes.AddAsync(existingLike);
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.ToggleLikeAsync(post.Id, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.IsLiked.Should().BeFalse();
        result.LikeCount.Should().Be(0);

        // Verify like was removed from database
        var likeInDb = await _context.Likes
            .FirstOrDefaultAsync(l => l.PostId == post.Id && l.UserId == user.Id);
        likeInDb.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task ToggleLikeAsync_WithInvalidPostId_ShouldThrowArgumentException(int invalidPostId)
    {
        // Arrange
        var userId = 1;

        // Act & Assert
        var action = () => _likeService.ToggleLikeAsync(invalidPostId, userId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("postId")
            .WithMessage("Post ID must be greater than zero*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task ToggleLikeAsync_WithInvalidUserId_ShouldThrowArgumentException(int invalidUserId)
    {
        // Arrange
        var postId = 1;

        // Act & Assert
        var action = () => _likeService.ToggleLikeAsync(postId, invalidUserId);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("User ID must be greater than zero*");
    }

    [Fact]
    public async Task ToggleLikeAsync_WithNonExistentPost_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var user = TestHelper.CreateTestUser(id: 101);
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.SaveChangesAsync();

        var nonExistentPostId = 99999;

        // Act & Assert
        var action = () => _likeService.ToggleLikeAsync(nonExistentPostId, user.Id);
        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Post with ID {nonExistentPostId} not found");
    }

    [Fact]
    public async Task ToggleLikeAsync_WithUserLikingOwnPost_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act & Assert
        var action = () => _likeService.ToggleLikeAsync(post.Id, author.Id);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Users cannot like their own posts");
    }

    [Fact]
    public async Task ToggleLikeAsync_WithMultipleLikesOnSamePost_ShouldUpdateCountCorrectly()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user1 = TestHelper.CreateTestUser(id: 101, username: "user1", email: "user1@test.com");
        var user2 = TestHelper.CreateTestUser(id: 102, username: "user2", email: "user2@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user1));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user2));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act - User1 likes the post
        var result1 = await _likeService.ToggleLikeAsync(post.Id, user1.Id);

        // Assert - First like
        result1.IsLiked.Should().BeTrue();
        result1.LikeCount.Should().Be(1);

        // Act - User2 likes the post
        var result2 = await _likeService.ToggleLikeAsync(post.Id, user2.Id);

        // Assert - Second like
        result2.IsLiked.Should().BeTrue();
        result2.LikeCount.Should().Be(2);

        // Act - User1 unlikes the post
        var result3 = await _likeService.ToggleLikeAsync(post.Id, user1.Id);

        // Assert - Unlike decreases count
        result3.IsLiked.Should().BeFalse();
        result3.LikeCount.Should().Be(1);
    }

    #endregion

    #region HasUserLikedPostAsync Tests

    [Fact]
    public async Task HasUserLikedPostAsync_WithExistingLike_ShouldReturnTrue()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user = TestHelper.CreateTestUser(id: 101, username: "user", email: "user@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        var like = new LikeEntity
        {
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.Likes.AddAsync(like);
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.HasUserLikedPostAsync(post.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasUserLikedPostAsync_WithoutLike_ShouldReturnFalse()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user = TestHelper.CreateTestUser(id: 101, username: "user", email: "user@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.HasUserLikedPostAsync(post.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    public async Task HasUserLikedPostAsync_WithInvalidIds_ShouldReturnFalse(int postId, int userId)
    {
        // Act
        var result = await _likeService.HasUserLikedPostAsync(postId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasUserLikedPostAsync_WithNonExistentPost_ShouldReturnFalse()
    {
        // Act
        var result = await _likeService.HasUserLikedPostAsync(99999, 1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetLikeCountForPostAsync Tests

    [Fact]
    public async Task GetLikeCountForPostAsync_WithMultipleLikes_ShouldReturnCorrectCount()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user1 = TestHelper.CreateTestUser(id: 101, username: "user1", email: "user1@test.com");
        var user2 = TestHelper.CreateTestUser(id: 102, username: "user2", email: "user2@test.com");
        var user3 = TestHelper.CreateTestUser(id: 103, username: "user3", email: "user3@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user1));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user2));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user3));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add multiple likes
        await _context.Likes.AddAsync(new LikeEntity { PostId = post.Id, UserId = user1.Id, CreatedAt = DateTimeOffset.UtcNow });
        await _context.Likes.AddAsync(new LikeEntity { PostId = post.Id, UserId = user2.Id, CreatedAt = DateTimeOffset.UtcNow });
        await _context.Likes.AddAsync(new LikeEntity { PostId = post.Id, UserId = user3.Id, CreatedAt = DateTimeOffset.UtcNow });
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.GetLikeCountForPostAsync(post.Id);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task GetLikeCountForPostAsync_WithNoLikes_ShouldReturnZero()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.GetLikeCountForPostAsync(post.Id);

        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetLikeCountForPostAsync_WithInvalidPostId_ShouldReturnZero(int invalidPostId)
    {
        // Act
        var result = await _likeService.GetLikeCountForPostAsync(invalidPostId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetLikeCountForPostAsync_WithNonExistentPost_ShouldReturnZero()
    {
        // Act
        var result = await _likeService.GetLikeCountForPostAsync(99999);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetLikesForPostAsync Tests

    [Fact]
    public async Task GetLikesForPostAsync_WithMultipleLikes_ShouldReturnOrderedLikes()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user1 = TestHelper.CreateTestUser(id: 101, username: "user1", email: "user1@test.com");
        var user2 = TestHelper.CreateTestUser(id: 102, username: "user2", email: "user2@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user1));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user2));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));

        // Add likes with different timestamps
        var like1 = new LikeEntity 
        { 
            PostId = post.Id, 
            UserId = user1.Id, 
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) 
        };
        var like2 = new LikeEntity 
        { 
            PostId = post.Id, 
            UserId = user2.Id, 
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) 
        };
        await _context.Likes.AddAsync(like1);
        await _context.Likes.AddAsync(like2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.GetLikesForPostAsync(post.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var likesList = result.ToList();
        // Should be ordered by most recent first
        likesList[0].UserId.Should().Be(user2.Id);
        likesList[0].PostId.Should().Be(post.Id);
        likesList[1].UserId.Should().Be(user1.Id);
        likesList[1].PostId.Should().Be(post.Id);

        // Verify all likes have proper data
        foreach (var like in likesList)
        {
            like.PostId.Should().Be(post.Id);
            like.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));
        }
    }

    [Fact]
    public async Task GetLikesForPostAsync_WithNoLikes_ShouldReturnEmptyCollection()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act
        var result = await _likeService.GetLikesForPostAsync(post.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetLikesForPostAsync_WithInvalidPostId_ShouldReturnEmptyCollection(int invalidPostId)
    {
        // Act
        var result = await _likeService.GetLikesForPostAsync(invalidPostId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLikesForPostAsync_WithNonExistentPost_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _likeService.GetLikesForPostAsync(99999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task LikeWorkflow_CompleteToggleCycle_ShouldWorkCorrectly()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user = TestHelper.CreateTestUser(id: 101, username: "user", email: "user@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act & Assert - Initial state: no likes
        var hasLiked = await _likeService.HasUserLikedPostAsync(post.Id, user.Id);
        hasLiked.Should().BeFalse();

        var likeCount = await _likeService.GetLikeCountForPostAsync(post.Id);
        likeCount.Should().Be(0);

        // Act & Assert - Like the post
        var likeResult = await _likeService.ToggleLikeAsync(post.Id, user.Id);
        likeResult.IsLiked.Should().BeTrue();
        likeResult.LikeCount.Should().Be(1);

        hasLiked = await _likeService.HasUserLikedPostAsync(post.Id, user.Id);
        hasLiked.Should().BeTrue();

        // Act & Assert - Unlike the post
        var unlikeResult = await _likeService.ToggleLikeAsync(post.Id, user.Id);
        unlikeResult.IsLiked.Should().BeFalse();
        unlikeResult.LikeCount.Should().Be(0);

        hasLiked = await _likeService.HasUserLikedPostAsync(post.Id, user.Id);
        hasLiked.Should().BeFalse();
    }

    [Fact]
    public async Task LikeWorkflow_MultipleUsersLikingSamePost_ShouldWorkCorrectly()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user1 = TestHelper.CreateTestUser(id: 101, username: "user1", email: "user1@test.com");
        var user2 = TestHelper.CreateTestUser(id: 102, username: "user2", email: "user2@test.com");
        var user3 = TestHelper.CreateTestUser(id: 103, username: "user3", email: "user3@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user1));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user2));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user3));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act - Multiple users like the post
        await _likeService.ToggleLikeAsync(post.Id, user1.Id);
        await _likeService.ToggleLikeAsync(post.Id, user2.Id);
        await _likeService.ToggleLikeAsync(post.Id, user3.Id);

        // Assert - All users have liked, count is correct
        var hasUser1Liked = await _likeService.HasUserLikedPostAsync(post.Id, user1.Id);
        var hasUser2Liked = await _likeService.HasUserLikedPostAsync(post.Id, user2.Id);
        var hasUser3Liked = await _likeService.HasUserLikedPostAsync(post.Id, user3.Id);

        hasUser1Liked.Should().BeTrue();
        hasUser2Liked.Should().BeTrue();
        hasUser3Liked.Should().BeTrue();

        var totalLikes = await _likeService.GetLikeCountForPostAsync(post.Id);
        totalLikes.Should().Be(3);

        var likes = await _likeService.GetLikesForPostAsync(post.Id);
        likes.Should().HaveCount(3);

        // Act - One user unlikes
        await _likeService.ToggleLikeAsync(post.Id, user2.Id);

        // Assert - Updated state
        hasUser2Liked = await _likeService.HasUserLikedPostAsync(post.Id, user2.Id);
        hasUser2Liked.Should().BeFalse();

        totalLikes = await _likeService.GetLikeCountForPostAsync(post.Id);
        totalLikes.Should().Be(2);
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [Fact]
    public async Task LikeService_ConcurrentLikeOperations_ShouldHandleGracefully()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user = TestHelper.CreateTestUser(id: 101, username: "user", email: "user@test.com");
        var post = TestHelper.CreateTestPost(id: 200, authorId: author.Id);

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post));
        await _context.SaveChangesAsync();

        // Act - Like the post
        await _likeService.ToggleLikeAsync(post.Id, user.Id);

        // Verify post is liked
        var hasLiked = await _likeService.HasUserLikedPostAsync(post.Id, user.Id);
        hasLiked.Should().BeTrue();

        // Act - Try to like again (should unlike)
        var result = await _likeService.ToggleLikeAsync(post.Id, user.Id);

        // Assert - Should be unliked now
        result.IsLiked.Should().BeFalse();
        result.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task LikeService_LikingDifferentPosts_ShouldIsolateCorrectly()
    {
        // Arrange
        var author = TestHelper.CreateTestUser(id: 100);
        var user = TestHelper.CreateTestUser(id: 101, username: "user", email: "user@test.com");
        var post1 = TestHelper.CreateTestPost(id: 200, authorId: author.Id, title: "Post 1");
        var post2 = TestHelper.CreateTestPost(id: 201, authorId: author.Id, title: "Post 2");

        await _context.Users.AddAsync(UserEntity.FromDomainModel(author));
        await _context.Users.AddAsync(UserEntity.FromDomainModel(user));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post1));
        await _context.Posts.AddAsync(PostEntity.FromDomainModel(post2));
        await _context.SaveChangesAsync();

        // Act - Like only post1
        await _likeService.ToggleLikeAsync(post1.Id, user.Id);

        // Assert - User has liked post1 but not post2
        var hasLikedPost1 = await _likeService.HasUserLikedPostAsync(post1.Id, user.Id);
        var hasLikedPost2 = await _likeService.HasUserLikedPostAsync(post2.Id, user.Id);

        hasLikedPost1.Should().BeTrue();
        hasLikedPost2.Should().BeFalse();

        var post1LikeCount = await _likeService.GetLikeCountForPostAsync(post1.Id);
        var post2LikeCount = await _likeService.GetLikeCountForPostAsync(post2.Id);

        post1LikeCount.Should().Be(1);
        post2LikeCount.Should().Be(0);
    }

    #endregion
}
