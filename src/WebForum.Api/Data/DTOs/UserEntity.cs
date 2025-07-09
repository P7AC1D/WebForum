using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Data.DTOs;

/// <summary>
/// Database entity for user accounts
/// </summary>
public class UserEntity
{
  public int Id { get; set; }

  [Required]
  [StringLength(50, MinimumLength = 3)]
  public string Username { get; set; } = string.Empty;

  [Required]
  [EmailAddress]
  [StringLength(100)]
  public string Email { get; set; } = string.Empty;

  [Required]
  public string PasswordHash { get; set; } = string.Empty;

  [Required]
  public string Role { get; set; } = "User"; // Store as string in DB

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

  /// <summary>
  /// Convert UserEntity to domain User model
  /// </summary>
  /// <returns>Domain User model</returns>
  public Models.User ToDomainModel()
  {
    return new Models.User
    {
      Id = Id,
      Username = Username,
      Email = Email,
      PasswordHash = PasswordHash,
      Role = Enum.TryParse<Models.UserRoles>(Role, true, out var role) ? role : Models.UserRoles.User,
      CreatedAt = CreatedAt
    };
  }

  /// <summary>
  /// Create UserEntity from domain User model
  /// </summary>
  /// <param name="user">Domain User model</param>
  /// <returns>UserEntity for database storage</returns>
  public static UserEntity FromDomainModel(Models.User user)
  {
    return new UserEntity
    {
      Id = user.Id,
      Username = user.Username,
      Email = user.Email,
      PasswordHash = user.PasswordHash,
      Role = user.Role.ToString(),
      CreatedAt = user.CreatedAt
    };
  }
}
