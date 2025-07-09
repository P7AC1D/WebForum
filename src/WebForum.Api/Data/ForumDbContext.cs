using Microsoft.EntityFrameworkCore;
using WebForum.Api.Models;

namespace WebForum.Api.Data;

public class ForumDbContext(DbContextOptions<ForumDbContext> options) : DbContext(options)
{
  public DbSet<User> Users { get; set; }
  public DbSet<Post> Posts { get; set; }
  public DbSet<Comment> Comments { get; set; }
  public DbSet<Like> Likes { get; set; }
  public DbSet<PostTag> PostTags { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // User configuration
    modelBuilder.Entity<User>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.Username).IsUnique();
      entity.HasIndex(e => e.Email).IsUnique();
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    });

    // Post configuration
    modelBuilder.Entity<Post>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.CreatedAt);
      entity.HasIndex(e => e.AuthorId);
      entity.HasIndex(e => new { e.AuthorId, e.CreatedAt }); // Composite index for author + date queries
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationship
      entity.HasOne<User>()
              .WithMany()
              .HasForeignKey(e => e.AuthorId)
              .OnDelete(DeleteBehavior.Restrict);
    });

    // Comment configuration
    modelBuilder.Entity<Comment>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.PostId);
      entity.HasIndex(e => e.AuthorId);
      entity.HasIndex(e => e.CreatedAt);
      entity.HasIndex(e => new { e.PostId, e.CreatedAt }); // Composite index for post + date queries
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationships
      entity.HasOne<Post>()
              .WithMany()
              .HasForeignKey(e => e.PostId)
              .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne<User>()
              .WithMany()
              .HasForeignKey(e => e.AuthorId)
              .OnDelete(DeleteBehavior.Restrict);
    });

    // Like configuration
    modelBuilder.Entity<Like>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => new { e.PostId, e.UserId }).IsUnique(); // Prevent duplicate likes
      entity.HasIndex(e => e.PostId); // For counting likes per post
      entity.HasIndex(e => e.UserId); // For user's liked posts
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationships
      entity.HasOne<Post>()
              .WithMany()
              .HasForeignKey(e => e.PostId)
              .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne<User>()
              .WithMany()
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    // PostTag configuration
    modelBuilder.Entity<PostTag>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.PostId);
      entity.HasIndex(e => e.Tag); // For filtering posts by tag
      entity.HasIndex(e => e.CreatedByUserId);
      entity.HasIndex(e => new { e.PostId, e.Tag }); // Composite index for efficient tag queries
      entity.HasIndex(e => new { e.Tag, e.CreatedAt }); // For tag-based filtering with date sorting
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationships
      entity.HasOne<Post>()
              .WithMany()
              .HasForeignKey(e => e.PostId)
              .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne<User>()
              .WithMany()
              .HasForeignKey(e => e.CreatedByUserId)
              .OnDelete(DeleteBehavior.Restrict);
    });
  }
}
