using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Service implementation for like/unlike operations on posts
/// </summary>
public class LikeService : ILikeService
{
  private readonly ForumDbContext _context;

  public LikeService(ForumDbContext context)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
  }

  /// <summary>
  /// Toggle like status on a post (like if not liked, unlike if already liked)
  /// </summary>
  /// <param name="postId">Post ID to like/unlike</param>
  /// <param name="userId">User ID performing the action</param>
  /// <returns>Like response with updated status and count</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post is not found</exception>
  /// <exception cref="InvalidOperationException">Thrown when user tries to like their own post</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  public async Task<LikeResponse> ToggleLikeAsync(int postId, int userId)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    if (userId <= 0)
      throw new ArgumentException("User ID must be greater than zero", nameof(userId));

    // Check if post exists and get author ID
    var post = await _context.Posts
        .Select(p => new { p.Id, p.AuthorId })
        .FirstOrDefaultAsync(p => p.Id == postId);

    if (post == null)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    // Prevent users from liking their own posts
    if (post.AuthorId == userId)
      throw new InvalidOperationException("Users cannot like their own posts");

    // Check if user has already liked this post
    var existingLike = await _context.Likes
        .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

    bool isLiked;
    if (existingLike != null)
    {
      // Remove the like
      _context.Likes.Remove(existingLike);
      isLiked = false;
    }
    else
    {
      // Add a new like
      var like = new Like
      {
        PostId = postId,
        UserId = userId,
        CreatedAt = DateTimeOffset.UtcNow
      };
      var likeEntity = LikeEntity.FromDomainModel(like);
      _context.Likes.Add(likeEntity);
      isLiked = true;
    }

    await _context.SaveChangesAsync();

    // Get updated like count
    var likeCount = await _context.Likes.CountAsync(l => l.PostId == postId);

    return new LikeResponse
    {
      PostId = postId,
      IsLiked = isLiked,
      LikeCount = likeCount
    };
  }

  /// <summary>
  /// Remove like from a post (explicit unlike operation)
  /// </summary>
  /// <param name="postId">Post ID to unlike</param>
  /// <param name="userId">User ID performing the action</param>
  /// <returns>Like response with updated status and count</returns>
  /// <exception cref="KeyNotFoundException">Thrown when post or like is not found</exception>
  /// <exception cref="ArgumentException">Thrown when IDs are invalid</exception>
  public async Task<LikeResponse> UnlikePostAsync(int postId, int userId)
  {
    if (postId <= 0)
      throw new ArgumentException("Post ID must be greater than zero", nameof(postId));

    if (userId <= 0)
      throw new ArgumentException("User ID must be greater than zero", nameof(userId));

    // Check if post exists
    var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
    if (!postExists)
      throw new KeyNotFoundException($"Post with ID {postId} not found");

    // Find the existing like
    var existingLike = await _context.Likes
        .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

    if (existingLike == null)
      throw new KeyNotFoundException($"Like not found for post {postId} by user {userId}");

    // Remove the like
    _context.Likes.Remove(existingLike);
    await _context.SaveChangesAsync();

    // Get updated like count
    var likeCount = await _context.Likes.CountAsync(l => l.PostId == postId);

    return new LikeResponse
    {
      PostId = postId,
      IsLiked = false,
      LikeCount = likeCount
    };
  }

  /// <summary>
  /// Check if a user has liked a specific post
  /// </summary>
  /// <param name="postId">Post ID to check</param>
  /// <param name="userId">User ID to check</param>
  /// <returns>True if user has liked the post, false otherwise</returns>
  public async Task<bool> HasUserLikedPostAsync(int postId, int userId)
  {
    if (postId <= 0 || userId <= 0)
      return false;

    return await _context.Likes
        .AnyAsync(l => l.PostId == postId && l.UserId == userId);
  }

  /// <summary>
  /// Get the total number of likes for a post
  /// </summary>
  /// <param name="postId">Post ID to count likes for</param>
  /// <returns>Number of likes for the post</returns>
  public async Task<int> GetLikeCountForPostAsync(int postId)
  {
    if (postId <= 0)
      return 0;

    return await _context.Likes.CountAsync(l => l.PostId == postId);
  }

  /// <summary>
  /// Get all likes for a specific post with user information
  /// </summary>
  /// <param name="postId">Post ID to get likes for</param>
  /// <returns>List of likes with user information</returns>
  public async Task<IEnumerable<Like>> GetLikesForPostAsync(int postId)
  {
    if (postId <= 0)
      return Enumerable.Empty<Like>();

    var likeEntities = await _context.Likes
        .Where(l => l.PostId == postId)
        .OrderByDescending(l => l.CreatedAt)
        .ToListAsync();

    return likeEntities.Select(le => le.ToDomainModel());
  }
}
