using WebForum.Api.Models;

namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for user profile management and user-related data operations
/// </summary>
public interface IUserService
{
  /// <summary>
  /// Get user profile information by ID including statistics
  /// </summary>
  /// <param name="userId">User ID to retrieve</param>
  /// <returns>User profile information with statistics</returns>
  /// <exception cref="KeyNotFoundException">Thrown when user is not found</exception>
  /// <exception cref="ArgumentException">Thrown when user ID is invalid</exception>
  Task<UserInfo> GetUserProfileAsync(int userId);

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
  Task<PagedResult<Post>> GetUserPostsAsync(int userId, int page, int pageSize, string sortOrder);

  /// <summary>
  /// Check if a user exists by ID
  /// </summary>
  /// <param name="userId">User ID to check</param>
  /// <returns>True if user exists, false otherwise</returns>
  Task<bool> UserExistsAsync(int userId);

  /// <summary>
  /// Get user by ID (internal use)
  /// </summary>
  /// <param name="userId">User ID to retrieve</param>
  /// <returns>User entity or null if not found</returns>
  Task<User?> GetUserByIdAsync(int userId);

  /// <summary>
  /// Get user by email (for authentication)
  /// </summary>
  /// <param name="email">Email address to search for</param>
  /// <returns>User entity or null if not found</returns>
  Task<User?> GetUserByEmailAsync(string email);

  /// <summary>
  /// Get user by username
  /// </summary>
  /// <param name="username">Username to search for</param>
  /// <returns>User entity or null if not found</returns>
  Task<User?> GetUserByUsernameAsync(string username);
}
