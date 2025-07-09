using Microsoft.EntityFrameworkCore;
using WebForum.Api.Data.DTOs;

namespace WebForum.Api.Data;

public class ForumDbContext(DbContextOptions<ForumDbContext> options) : DbContext(options)
{
  public DbSet<UserEntity> Users { get; set; }
  public DbSet<PostEntity> Posts { get; set; }
  public DbSet<CommentEntity> Comments { get; set; }
  public DbSet<LikeEntity> Likes { get; set; }
  public DbSet<PostTagEntity> PostTags { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // UserEntity configuration
    modelBuilder.Entity<UserEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.Username).IsUnique();
      entity.HasIndex(e => e.Email).IsUnique();
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    });

    // PostEntity configuration
    modelBuilder.Entity<PostEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.CreatedAt);
      entity.HasIndex(e => e.AuthorId);
      entity.HasIndex(e => new { e.AuthorId, e.CreatedAt }); // Composite index for author + date queries
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationship
      entity.HasOne<UserEntity>()
              .WithMany()
              .HasForeignKey(e => e.AuthorId)
              .OnDelete(DeleteBehavior.Restrict);
    });

    // CommentEntity configuration
    modelBuilder.Entity<CommentEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.PostId);
      entity.HasIndex(e => e.AuthorId);
      entity.HasIndex(e => e.CreatedAt);
      entity.HasIndex(e => new { e.PostId, e.CreatedAt }); // Composite index for Post + date queries
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationships
      entity.HasOne<PostEntity>()
              .WithMany()
              .HasForeignKey(e => e.PostId)
              .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne<UserEntity>()
              .WithMany()
              .HasForeignKey(e => e.AuthorId)
              .OnDelete(DeleteBehavior.Restrict);
    });

    // LikeEntity configuration
    modelBuilder.Entity<LikeEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => new { e.PostId, e.UserId }).IsUnique(); // Prevent duplicate Likes
      entity.HasIndex(e => e.PostId); // For counting Likes per Post
      entity.HasIndex(e => e.UserId); // For User's's Likes Posts
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationships
      entity.HasOne<PostEntity>()
              .WithMany()
              .HasForeignKey(e => e.PostId)
              .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne<UserEntity>()
              .WithMany()
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    // PostEntityTag configuration
    modelBuilder.Entity<PostTagEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.PostId);
      entity.HasIndex(e => e.Tag); // For filtering Posts by tag
      entity.HasIndex(e => e.CreatedByUserId);
      entity.HasIndex(e => new { e.PostId, e.Tag }); // Composite index for efficient tag queries
      entity.HasIndex(e => new { e.Tag, e.CreatedAt }); // For tag-based filtering with date sorting
      entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

      // Foreign key relationships
      entity.HasOne<PostEntity>()
              .WithMany()
              .HasForeignKey(e => e.PostId)
              .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne<UserEntity>()
              .WithMany()
              .HasForeignKey(e => e.CreatedByUserId)
              .OnDelete(DeleteBehavior.Restrict);
    });
  }
}
