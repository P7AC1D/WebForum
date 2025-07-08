using System.ComponentModel.DataAnnotations;

namespace WebForum.Api.Models;

public class PostTag
{
  public int Id { get; set; }

  public int PostId { get; set; }

  [Required]
  [StringLength(50)]
  public string Tag { get; set; } = string.Empty;

  public int CreatedByUserId { get; set; }

  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PostTags
{
  public const string MisleadingInformation = "misleading-information";
  public const string FalseInformation = "false-information";
}
