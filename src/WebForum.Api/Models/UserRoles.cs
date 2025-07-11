namespace WebForum.Api.Models;

/// <summary>
/// Enumeration of user roles available in the forum system
/// </summary>
public enum UserRoles
{
  /// <summary>
  /// Regular user with basic forum permissions (create posts, comments, likes)
  /// </summary>
  User,

  /// <summary>
  /// Moderator with enhanced permissions (all user permissions plus moderation capabilities)
  /// </summary>
  Moderator
}
