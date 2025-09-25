using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;

namespace MiniPainterHub.Server.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }

        public DbSet<Profile> Profiles { get; set; } = default!;
        public DbSet<Post> Posts { get; set; } = default!;
        public DbSet<PostImage> PostImages { get; set; } = default!;
        public DbSet<Comment> Comments { get; set; } = default!;
        public DbSet<Like> Likes { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Profile>(b =>
            {
                // Shared PK = FK to AspNetUsers
                b.HasKey(p => p.UserId);

                b.Property(p => p.UserId).HasMaxLength(450);   // matches default Identity key size
                b.Property(p => p.DisplayName).HasMaxLength(80);
                b.Property(p => p.Bio).HasMaxLength(500);
                b.Property(p => p.AvatarUrl).HasMaxLength(2048);

                b.HasOne(p => p.User)
                 .WithOne(u => u.Profile)                      // <-- add back-nav on ApplicationUser
                 .HasForeignKey<Profile>(p => p.UserId)
                 .IsRequired()                                 // <-- make the relationship required
                 .OnDelete(DeleteBehavior.Cascade);            // or Restrict, if you prefer
            });

            // Post relationship to ApplicationUser: restrict delete
            builder.Entity<Post>()
                   .HasOne(p => p.CreatedBy)
                   .WithMany()
                   .HasForeignKey(p => p.CreatedById)
                   .OnDelete(DeleteBehavior.Restrict);

            // PostImages: cascade on Post deletion
            builder.Entity<PostImage>()
                   .HasOne(pi => pi.Post)
                   .WithMany(p => p.Images)
                   .HasForeignKey(pi => pi.PostId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Comments: cascade on Post deletion, restrict on Author deletion
            builder.Entity<Comment>()
                   .HasOne(c => c.Post)
                   .WithMany(p => p.Comments)
                   .HasForeignKey(c => c.PostId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<Comment>()
                   .HasOne(c => c.Author)
                   .WithMany()
                   .HasForeignKey(c => c.AuthorId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Likes: cascade on Post deletion, restrict on User deletion
            builder.Entity<Like>()
                   .HasOne(l => l.Post)
                   .WithMany(p => p.Likes)
                   .HasForeignKey(l => l.PostId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<Like>()
                   .HasOne(l => l.User)
                   .WithMany()
                   .HasForeignKey(l => l.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }


    }
}
