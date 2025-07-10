using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Models.Response;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Service implementation for moderation operations including post tagging
/// </summary>
public class ModerationService : IModerationService
{
  private readonly ForumDbContext _context;
  private const string MisinformationTag = "misleading or false information";

  public ModerationService(ForumDbContext context)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
  }

  /// <summary>
  /// Tag a post as "misleading or false information" for regulatory compliance
  /// </summary>
  /// <param name="postId">Post ID to tag</param>
  /// <param name="moderatorId">Moderator user ID performing the action</param>
  /// <returns>Moderation response with tag details</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="InvalidOperationException">Thrown when post is already tagged</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  /// <exception cref="UnauthorizedAccessException">Thrown when user is not a moderator</exception>
  public async Task<ModerationResponse> TagPostAsync(int postId, int moderatorId)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    if (moderatorId <= 0)
      throw new ArgumentException("Moderator ID must be greater than zero", nameof(moderatorId));

    // Check if user is a moderator
    if (!await IsUserModeratorAsync(moderatorId))
      throw new UnauthorizedAccessException("User is not authorized to perform moderation actions");

    // Check if post exists
    var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
    if (!postExists)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    // Check if post is already tagged
    var existingTag = await _context.PostTags
        .FirstOrDefaultAsync(pt => pt.PostId == postId && pt.Tag == MisinformationTag);

    if (existingTag != null)
      throw new InvalidOperationException($"Post {postId} is already tagged as '{MisinformationTag}'");

    // Get moderator information
    var moderator = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == moderatorId);

    if (moderator == null)
      throw new KeyNotFoundException($"Moderator with ID {moderatorId} not found");

    // Create the tag
    var postTag = new PostTag
    {
      PostId = postId,
      Tag = MisinformationTag,
      CreatedByUserId = moderatorId,
      CreatedAt = DateTimeOffset.UtcNow
    };

    var postTagEntity = PostTagEntity.FromDomainModel(postTag);
    _context.PostTags.Add(postTagEntity);
    await _context.SaveChangesAsync();

    return new ModerationResponse
    {
      PostId = postId,
      Action = "tagged",
      Tag = MisinformationTag,
      ModeratorId = moderatorId,
      ModeratorUsername = moderator.Username,
      ActionTimestamp = postTag.CreatedAt
    };
  }

  /// <summary>
  /// Remove "misleading or false information" tag from a post
  /// </summary>
  /// <param name="postId">Post ID to remove tag from</param>
  /// <param name="moderatorId">Moderator user ID performing the action</param>
  /// <returns>Moderation response with removal details</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post or tag is not found</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  /// <exception cref="UnauthorizedAccessException">Thrown when user is not a moderator</exception>
  public async Task<ModerationResponse> RemoveTagFromPostAsync(int postId, int moderatorId)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    if (moderatorId <= 0)
      throw new ArgumentException("Moderator ID must be greater than zero", nameof(moderatorId));

    // Check if user is a moderator
    if (!await IsUserModeratorAsync(moderatorId))
      throw new UnauthorizedAccessException("User is not authorized to perform moderation actions");

    // Check if post exists
    var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
    if (!postExists)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    // Find the existing tag
    var existingTag = await _context.PostTags
        .FirstOrDefaultAsync(pt => pt.PostId == postId && pt.Tag == MisinformationTag);

    if (existingTag == null)
      throw new KeyNotFoundException($"No '{MisinformationTag}' tag found for post {postId}");

    // Get moderator information
    var moderator = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == moderatorId);

    if (moderator == null)
      throw new KeyNotFoundException($"Moderator with ID {moderatorId} not found");

    // Remove the tag
    _context.PostTags.Remove(existingTag);
    await _context.SaveChangesAsync();

    return new ModerationResponse
    {
      PostId = postId,
      Action = "untagged",
      Tag = MisinformationTag,
      ModeratorId = moderatorId,
      ModeratorUsername = moderator.Username,
      ActionTimestamp = DateTimeOffset.UtcNow
    };
  }

  /// <summary>
  /// Get all posts that have been tagged as "misleading or false information"
  /// </summary>
  /// <param name="page">Page number for pagination</param>
  /// <param name="pageSize">Number of posts per page</param>
  /// <returns>Paginated list of tagged posts with moderation details</returns>
  /// <exception cref="ArgumentException">Thrown when pagination parameters are invalid</exception>
  public async Task<PagedResult<TaggedPost>> GetTaggedPostsAsync(int page, int pageSize)
  {
    if (page < 1)
      throw new ArgumentException("Page must be greater than zero", nameof(page));

    if (pageSize < 1 || pageSize > 100)
      throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

    var query = from pt in _context.PostTags
                join p in _context.Posts on pt.PostId equals p.Id
                join author in _context.Users on p.AuthorId equals author.Id
                join moderator in _context.Users on pt.CreatedByUserId equals moderator.Id
                where pt.Tag == MisinformationTag
                orderby pt.CreatedAt descending
                select new TaggedPost
                {
                  Id = p.Id,
                  Title = p.Title,
                  Content = p.Content,
                  AuthorId = p.AuthorId,
                  CreatedAt = p.CreatedAt,
                  Tag = pt.Tag,
                  TaggedByUserId = pt.CreatedByUserId,
                  TaggedByUsername = moderator.Username,
                  TaggedAt = pt.CreatedAt
                };

    var totalItems = await query.CountAsync();
    var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

    var taggedPosts = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PagedResult<TaggedPost>
    {
      Items = taggedPosts,
      Page = page,
      PageSize = pageSize,
      TotalCount = totalItems,
      TotalPages = totalPages,
      HasPrevious = page > 1,
      HasNext = page < totalPages
    };
  }

  /// <summary>
  /// Check if a post is tagged as "misleading or false information"
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <returns>True if post is tagged, false otherwise</returns>
  public async Task<bool> IsPostTaggedAsync(int postId)
  {
    if (postId <= 0)
      return false;

    return await _context.PostTags
        .AnyAsync(pt => pt.PostId == postId && pt.Tag == MisinformationTag);
  }

  /// <summary>
  /// Check if a user is a moderator
  /// </summary>
  /// <param name="userId">User ID to check</param>
  /// <returns>True if user is a moderator, false otherwise</returns>
  public async Task<bool> IsUserModeratorAsync(int userId)
  {
    if (userId <= 0)
      return false;

    var userEntity = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity == null) return false;

    var user = userEntity.ToDomainModel();
    return user.Role == UserRoles.Moderator;
  }

  /// <summary>
  /// Get moderation history for a specific post
  /// </summary>
  /// <param name="postId">Post ID to get history for</param>
  /// <returns>List of moderation actions performed on the post</returns>
  public async Task<IEnumerable<PostTag>> GetModerationHistoryForPostAsync(int postId)
  {
    if (postId <= 0)
      return Enumerable.Empty<PostTag>();

    var postTagEntities = await _context.PostTags
        .Where(pt => pt.PostId == postId)
        .OrderByDescending(pt => pt.CreatedAt)
        .ToListAsync();

    return postTagEntities.Select(pte => pte.ToDomainModel());
  }
}
