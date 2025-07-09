using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Service implementation for user profile management and user-related data operations
/// </summary>
public class UserService : IUserService
{
  private readonly ForumDbContext _context;

  public UserService(ForumDbContext context)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
  }

  /// <summary>
  /// Get user profile information by ID including statistics
  /// </summary>
  /// <param name="userId">User ID to retrieve</param>
  /// <returns>User profile information with statistics</returns>
  /// <exception cref="KeyNotFoundException">Thrown when user is not found</exception>
  /// <exception cref="ArgumentException">Thrown when user ID is invalid</exception>
  public async Task<UserInfo> GetUserProfileAsync(int userId)
  {
    if (userId <= 0)
      throw new ArgumentException("User ID must be greater than zero", nameof(userId));

    var userEntity = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity == null)
      throw new KeyNotFoundException($"User with ID {userId} not found");

    // Convert to domain model
    var user = userEntity.ToDomainModel();

    // Get user statistics
    var postCount = await _context.Posts
        .CountAsync(p => p.AuthorId == userId);

    var commentCount = await _context.Comments
        .CountAsync(c => c.AuthorId == userId);

    var likesReceived = await _context.Likes
        .Where(l => _context.Posts.Any(p => p.Id == l.PostId && p.AuthorId == userId))
        .CountAsync();

    return UserInfo.ForPublicProfile(user, postCount, commentCount, likesReceived);
  }

  /// <summary>
  /// Get posts created by a specific user with pagination and sorting
  /// </summary>
  /// <param name="userId">User ID whose posts to retrieve</param>
  /// <param name="page">Page number for pagination</param>
  /// <param name="pageSize">Number of posts per page</param>
  /// <param name="sortOrder">Sort order for posts by creation date</param>
  /// <returns>Paginated list of user's posts</returns>
  /// <exception cref="KeyNotFoundException">Thrown when user is not found</exception>
  /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
  public async Task<PagedResult<Post>> GetUserPostsAsync(int userId, int page, int pageSize, string sortOrder)
  {
    if (userId <= 0)
      throw new ArgumentException("User ID must be greater than zero", nameof(userId));

    if (page < 1)
      throw new ArgumentException("Page must be greater than zero", nameof(page));

    if (pageSize < 1 || pageSize > 100)
      throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

    // Check if user exists
    var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
    if (!userExists)
      throw new KeyNotFoundException($"User with ID {userId} not found");

    var query = _context.Posts
        .Where(p => p.AuthorId == userId);

    // Apply sorting
    query = sortOrder?.ToLower() switch
    {
      "desc" or "newest" => query.OrderByDescending(p => p.CreatedAt),
      "asc" or "oldest" => query.OrderBy(p => p.CreatedAt),
      _ => query.OrderByDescending(p => p.CreatedAt) // Default to newest first
    };

    var totalItems = await query.CountAsync();
    var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

    var postEntities = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    // Convert DTO entities to domain models (no count enrichment needed for user posts)
    var posts = postEntities.Select(pe => pe.ToDomainModel()).ToList();

    return new PagedResult<Post>
    {
      Items = posts,
      Page = page,
      PageSize = pageSize,
      TotalCount = totalItems,
      TotalPages = totalPages,
      HasPrevious = page > 1,
      HasNext = page < totalPages
    };
  }

  /// <summary>
  /// Check if a user exists by ID
  /// </summary>
  /// <param name="userId">User ID to check</param>
  /// <returns>True if user exists, false otherwise</returns>
  public async Task<bool> UserExistsAsync(int userId)
  {
    if (userId <= 0)
      return false;

    return await _context.Users.AnyAsync(u => u.Id == userId);
  }

  /// <summary>
  /// Get user by ID (internal use)
  /// </summary>
  /// <param name="userId">User ID to retrieve</param>
  /// <returns>User entity or null if not found</returns>
  public async Task<User?> GetUserByIdAsync(int userId)
  {
    if (userId <= 0)
      return null;

    var userEntity = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == userId);

    return userEntity?.ToDomainModel();
  }

  /// <summary>
  /// Get user by email (for authentication)
  /// </summary>
  /// <param name="email">Email address to search for</param>
  /// <returns>User entity or null if not found</returns>
  public async Task<User?> GetUserByEmailAsync(string email)
  {
    if (string.IsNullOrWhiteSpace(email))
      return null;

    var userEntity = await _context.Users
        .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

    return userEntity?.ToDomainModel();
  }

  /// <summary>
  /// Get user by username
  /// </summary>
  /// <param name="username">Username to search for</param>
  /// <returns>User entity or null if not found</returns>
  public async Task<User?> GetUserByUsernameAsync(string username)
  {
    if (string.IsNullOrWhiteSpace(username))
      return null;

    var userEntity = await _context.Users
        .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

    return userEntity?.ToDomainModel();
  }

  /// <summary>
  /// Get usernames for multiple user IDs efficiently
  /// </summary>
  /// <param name="userIds">Collection of user IDs to retrieve usernames for</param>
  /// <returns>Dictionary mapping user IDs to usernames</returns>
  public async Task<Dictionary<int, string>> GetUsernamesByIdsAsync(IEnumerable<int> userIds)
  {
    if (userIds == null || !userIds.Any())
      return new Dictionary<int, string>();

    var distinctIds = userIds.Where(id => id > 0).Distinct().ToList();
    
    if (!distinctIds.Any())
      return new Dictionary<int, string>();

    var userEntities = await _context.Users
        .Where(u => distinctIds.Contains(u.Id))
        .Select(u => new { u.Id, u.Username })
        .ToListAsync();

    return userEntities.ToDictionary(u => u.Id, u => u.Username);
  }
}
