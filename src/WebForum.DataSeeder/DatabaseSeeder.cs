using Bogus;
using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;

namespace WebForum.DataSeeder;

/// <summary>
/// Database seeder for creating realistic test data for the Web Forum
/// This uses the same data generation logic as the integration tests to ensure consistency
/// </summary>
public static class DatabaseSeeder
{
  /// <summary>
  /// Seeds the database with realistic test data
  /// </summary>
  /// <param name="context">Database context</param>
  /// <param name="userCount">Number of users to create</param>
  /// <param name="postCount">Number of posts to create</param>
  /// <param name="commentCount">Number of comments to create</param>
  /// <param name="likeCount">Number of likes to create</param>
  public static async Task SeedAsync(
      ForumDbContext context, 
      int userCount = 10, 
      int postCount = 25, 
      int commentCount = 50, 
      int likeCount = 75)
  {
    Console.WriteLine("🧹 Cleaning existing data...");
    await CleanDatabaseAsync(context);

    Console.WriteLine("👥 Creating users...");
    var users = await CreateUsersAsync(context, userCount);

    Console.WriteLine("📝 Creating posts...");
    var posts = await CreatePostsAsync(context, users, postCount);

    Console.WriteLine("💬 Creating comments...");
    await CreateCommentsAsync(context, users, posts, commentCount);

    Console.WriteLine("👍 Creating likes...");
    await CreateLikesAsync(context, users, posts, likeCount);

    Console.WriteLine("🏷️ Creating moderation tags...");
    await CreateModerationTagsAsync(context, users, posts);

    await context.SaveChangesAsync();
    Console.WriteLine("✅ Seeding completed successfully!");
  }

  /// <summary>
  /// Cleans all data from the database while preserving schema
  /// </summary>
  private static async Task CleanDatabaseAsync(ForumDbContext context)
  {
    // Clean in dependency order
    await context.Database.ExecuteSqlRawAsync("DELETE FROM \"PostTags\"");
    await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Likes\"");
    await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Comments\"");
    await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Posts\"");
    await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\"");

    // Reset sequences
    await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Users_Id_seq\" RESTART WITH 1");
    await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Posts_Id_seq\" RESTART WITH 1");
    await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Comments_Id_seq\" RESTART WITH 1");
    await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Likes_Id_seq\" RESTART WITH 1");
    await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"PostTags_Id_seq\" RESTART WITH 1");
  }

  /// <summary>
  /// Creates realistic test users with proper password hashing
  /// </summary>
  private static async Task<List<UserEntity>> CreateUsersAsync(ForumDbContext context, int count)
  {
    var faker = new Faker<UserEntity>()
        .RuleFor(u => u.Username, f => f.Internet.UserName())
        .RuleFor(u => u.Email, f => f.Internet.Email())
        .RuleFor(u => u.PasswordHash, f => BCrypt.Net.BCrypt.HashPassword("password123"))
        .RuleFor(u => u.Role, f => f.PickRandom<UserRoles>())
        .RuleFor(u => u.CreatedAt, f => f.Date.PastOffset(365).ToUniversalTime());

    var users = new List<UserEntity>();

    // Create a guaranteed moderator
    var moderator = new UserEntity
    {
      Username = "moderator",
      Email = "moderator@webforum.com",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
      Role = UserRoles.Moderator,
      CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
    };
    users.Add(moderator);

    // Create a guaranteed regular user
    var regularUser = new UserEntity
    {
      Username = "testuser",
      Email = "user@webforum.com",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
      Role = UserRoles.User,
      CreatedAt = DateTimeOffset.UtcNow.AddDays(-25)
    };
    users.Add(regularUser);

    // Generate the rest with faker
    for (int i = 2; i < count; i++)
    {
      var user = faker.Generate();
      
      // Ensure unique usernames and emails
      var attempts = 0;
      while ((users.Any(u => u.Username == user.Username) || 
              users.Any(u => u.Email == user.Email)) && attempts < 10)
      {
        user = faker.Generate();
        attempts++;
      }
      
      if (attempts < 10)
      {
        users.Add(user);
      }
    }

    context.Users.AddRange(users);
    await context.SaveChangesAsync();
    
    Console.WriteLine($"   ✓ Created {users.Count} users ({users.Count(u => u.Role == UserRoles.Moderator)} moderators)");
    return users;
  }

  /// <summary>
  /// Creates realistic forum posts with varied content
  /// </summary>
  private static async Task<List<PostEntity>> CreatePostsAsync(ForumDbContext context, List<UserEntity> users, int count)
  {
    var faker = new Faker<PostEntity>()
        .RuleFor(p => p.Title, f => f.Lorem.Sentence(4, 8).TrimEnd('.'))
        .RuleFor(p => p.Content, f => f.Lorem.Paragraphs(2, 5))
        .RuleFor(p => p.AuthorId, f => f.PickRandom(users).Id)
        .RuleFor(p => p.CreatedAt, f => f.Date.PastOffset(180).ToUniversalTime());

    var posts = faker.Generate(count);
    
    // Ensure posts are created in chronological order
    posts = posts.OrderBy(p => p.CreatedAt).ToList();
    
    context.Posts.AddRange(posts);
    await context.SaveChangesAsync();
    
    Console.WriteLine($"   ✓ Created {posts.Count} posts");
    return posts;
  }

  /// <summary>
  /// Creates realistic comments on posts
  /// </summary>
  private static async Task CreateCommentsAsync(ForumDbContext context, List<UserEntity> users, List<PostEntity> posts, int count)
  {
    var faker = new Faker<CommentEntity>()
        .RuleFor(c => c.Content, f => f.Lorem.Sentences(1, 3))
        .RuleFor(c => c.PostId, f => f.PickRandom(posts).Id)
        .RuleFor(c => c.AuthorId, f => f.PickRandom(users).Id)
        .RuleFor(c => c.CreatedAt, f => f.Date.PastOffset(90).ToUniversalTime());

    var comments = faker.Generate(count);
    
    // Ensure comments are created after their associated posts
    foreach (var comment in comments)
    {
      var post = posts.First(p => p.Id == comment.PostId);
      if (comment.CreatedAt < post.CreatedAt)
      {
        comment.CreatedAt = post.CreatedAt.AddMinutes(faker.Random.Int(1, 60 * 24 * 7)); // 1 minute to 1 week after post
      }
    }
    
    context.Comments.AddRange(comments);
    await context.SaveChangesAsync();
    
    Console.WriteLine($"   ✓ Created {comments.Count} comments");
  }

  /// <summary>
  /// Creates realistic likes on posts with business rule enforcement
  /// </summary>
  private static async Task CreateLikesAsync(ForumDbContext context, List<UserEntity> users, List<PostEntity> posts, int count)
  {
    var likes = new List<LikeEntity>();
    var faker = new Faker();
    var usedCombinations = new HashSet<(int UserId, int PostId)>();

    for (int i = 0; i < count && likes.Count < count; i++)
    {
      var user = faker.PickRandom(users);
      var post = faker.PickRandom(posts);
      
      // Business rule: Users cannot like their own posts
      if (user.Id == post.AuthorId)
        continue;
      
      // Business rule: Each user can only like a post once
      if (usedCombinations.Contains((user.Id, post.Id)))
        continue;
      
      var like = new LikeEntity
      {
        UserId = user.Id,
        PostId = post.Id,
        CreatedAt = faker.Date.BetweenOffset(post.CreatedAt, DateTimeOffset.UtcNow).ToUniversalTime()
      };
      
      likes.Add(like);
      usedCombinations.Add((user.Id, post.Id));
    }
    
    context.Likes.AddRange(likes);
    await context.SaveChangesAsync();
    
    Console.WriteLine($"   ✓ Created {likes.Count} likes");
  }

  /// <summary>
  /// Creates some moderation tags for demonstration purposes
  /// </summary>
  private static async Task CreateModerationTagsAsync(ForumDbContext context, List<UserEntity> users, List<PostEntity> posts)
  {
    var moderators = users.Where(u => u.Role == UserRoles.Moderator).ToList();
    if (!moderators.Any()) return;

    var faker = new Faker();
    var taggedPostsCount = Math.Min(3, posts.Count / 4); // Tag about 25% of posts, max 3
    var postsToTag = faker.PickRandom(posts, taggedPostsCount);

    var tags = new List<PostTagEntity>();
    
    foreach (var post in postsToTag)
    {
      var moderator = faker.PickRandom(moderators);
      var tag = new PostTagEntity
      {
        PostId = post.Id,
        Tag = "misleading or false information",
        CreatedByUserId = moderator.Id,
        CreatedAt = faker.Date.BetweenOffset(post.CreatedAt.AddHours(1), DateTimeOffset.UtcNow).ToUniversalTime()
      };
      tags.Add(tag);
    }
    
    context.PostTags.AddRange(tags);
    await context.SaveChangesAsync();
    
    Console.WriteLine($"   ✓ Created {tags.Count} moderation tags");
  }
}
