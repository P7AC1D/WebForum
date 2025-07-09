using Bogus;
using Microsoft.Extensions.DependencyInjection;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;

namespace WebForum.IntegrationTests.Infrastructure;

/// <summary>
/// Provides test data generation and seeding utilities using Bogus
/// </summary>
public static class TestDataHelper
{
    private static readonly Faker _faker = new();

    /// <summary>
    /// Seeds the database with test users, posts, comments, and likes
    /// </summary>
    /// <param name="factory">Test factory instance</param>
    /// <param name="userCount">Number of users to create</param>
    /// <param name="postCount">Number of posts to create</param>
    /// <param name="commentCount">Number of comments to create</param>
    /// <param name="likeCount">Number of likes to create</param>
    /// <returns>Seeded test data</returns>
    public static async Task<TestDataSet> SeedDatabaseAsync(
        WebForumTestFactory factory,
        int userCount = 5,
        int postCount = 10,
        int commentCount = 20,
        int likeCount = 15)
    {
        using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ForumDbContext>();

        var testData = new TestDataSet();

        // Create users
        testData.Users = await CreateUsersAsync(context, userCount);
        
        // Create posts
        testData.Posts = await CreatePostsAsync(context, testData.Users, postCount);
        
        // Create comments
        testData.Comments = await CreateCommentsAsync(context, testData.Users, testData.Posts, commentCount);
        
        // Create likes
        testData.Likes = await CreateLikesAsync(context, testData.Users, testData.Posts, likeCount);

        await context.SaveChangesAsync();
        
        return testData;
    }

    /// <summary>
    /// Creates test users with realistic data
    /// </summary>
    public static async Task<List<UserEntity>> CreateUsersAsync(ForumDbContext context, int count)
    {
        var users = new List<UserEntity>();
        var userFaker = new Faker<UserEntity>()
            .RuleFor(u => u.Username, f => f.Internet.UserName())
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.PasswordHash, f => f.Random.Hash())
            .RuleFor(u => u.Role, f => f.PickRandom<UserRoles>())
            .RuleFor(u => u.CreatedAt, f => f.Date.Past(365).ToUniversalTime());

        for (int i = 0; i < count; i++)
        {
            var user = userFaker.Generate();
            users.Add(user);
            context.Users.Add(user);
        }

        await context.SaveChangesAsync();
        return users;
    }

    /// <summary>
    /// Creates test posts with realistic content
    /// </summary>
    public static async Task<List<PostEntity>> CreatePostsAsync(
        ForumDbContext context, 
        List<UserEntity> users, 
        int count)
    {
        var posts = new List<PostEntity>();
        var postFaker = new Faker<PostEntity>()
            .RuleFor(p => p.Title, f => f.Lorem.Sentence(3, 8))
            .RuleFor(p => p.Content, f => f.Lorem.Paragraphs(1, 3))
            .RuleFor(p => p.AuthorId, f => f.PickRandom(users).Id)
            .RuleFor(p => p.CreatedAt, f => f.Date.Past(180).ToUniversalTime());

        for (int i = 0; i < count; i++)
        {
            var post = postFaker.Generate();
            posts.Add(post);
            context.Posts.Add(post);
        }

        await context.SaveChangesAsync();
        return posts;
    }

    /// <summary>
    /// Creates test comments for posts
    /// </summary>
    public static async Task<List<CommentEntity>> CreateCommentsAsync(
        ForumDbContext context,
        List<UserEntity> users,
        List<PostEntity> posts,
        int count)
    {
        var comments = new List<CommentEntity>();
        var commentFaker = new Faker<CommentEntity>()
            .RuleFor(c => c.Content, f => f.Lorem.Sentence(1, 3))
            .RuleFor(c => c.AuthorId, f => f.PickRandom(users).Id)
            .RuleFor(c => c.PostId, f => f.PickRandom(posts).Id)
            .RuleFor(c => c.CreatedAt, f => f.Date.Past(90).ToUniversalTime());

        for (int i = 0; i < count; i++)
        {
            var comment = commentFaker.Generate();
            comments.Add(comment);
            context.Comments.Add(comment);
        }

        await context.SaveChangesAsync();
        return comments;
    }

    /// <summary>
    /// Creates test likes for posts
    /// </summary>
    public static async Task<List<LikeEntity>> CreateLikesAsync(
        ForumDbContext context,
        List<UserEntity> users,
        List<PostEntity> posts,
        int count)
    {
        var likes = new List<LikeEntity>();
        var createdLikes = new HashSet<(int UserId, int PostId)>();

        var likeFaker = new Faker<LikeEntity>()
            .RuleFor(l => l.UserId, f => f.PickRandom(users).Id)
            .RuleFor(l => l.PostId, f => f.PickRandom(posts).Id)
            .RuleFor(l => l.CreatedAt, f => f.Date.Past(60).ToUniversalTime());

        int attempts = 0;
        while (likes.Count < count && attempts < count * 3) // Prevent infinite loop
        {
            var like = likeFaker.Generate();
            var key = (like.UserId, like.PostId);
            
            if (!createdLikes.Contains(key))
            {
                createdLikes.Add(key);
                likes.Add(like);
                context.Likes.Add(like);
            }
            attempts++;
        }

        await context.SaveChangesAsync();
        return likes;
    }

    /// <summary>
    /// Creates a specific test user with known credentials
    /// </summary>
    public static async Task<UserEntity> CreateTestUserAsync(
        ForumDbContext context,
        string username = "testuser",
        string email = "test@example.com",
        UserRoles roles = UserRoles.User)
    {
        var user = new UserEntity
        {
            Username = username,
            Email = email,
            PasswordHash = _faker.Random.Hash(),
            Role = roles,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Creates a specific test post with known data
    /// </summary>
    public static async Task<PostEntity> CreateTestPostAsync(
        ForumDbContext context,
        int authorId,
        string title = "Test Post",
        string content = "This is a test post content.")
    {
        var post = new PostEntity
        {
            Title = title,
            Content = content,
            AuthorId = authorId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Posts.Add(post);
        await context.SaveChangesAsync();
        return post;
    }

    /// <summary>
    /// Creates a specific test comment with known data
    /// </summary>
    public static async Task<CommentEntity> CreateTestCommentAsync(
        ForumDbContext context,
        int postId,
        int authorId,
        string content = "This is a test comment.")
    {
        var comment = new CommentEntity
        {
            Content = content,
            PostId = postId,
            AuthorId = authorId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Comments.Add(comment);
        await context.SaveChangesAsync();
        return comment;
    }
}

/// <summary>
/// Container for all generated test data
/// </summary>
public class TestDataSet
{
    public List<UserEntity> Users { get; set; } = new();
    public List<PostEntity> Posts { get; set; } = new();
    public List<CommentEntity> Comments { get; set; } = new();
    public List<LikeEntity> Likes { get; set; } = new();
}
