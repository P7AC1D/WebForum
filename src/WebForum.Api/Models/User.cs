using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

public class User
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
  public UserRoles Role { get; set; } = UserRoles.User;

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
