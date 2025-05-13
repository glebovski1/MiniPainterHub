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
        public DbSet<Comment> Comments { get; set; } = default!;
        public DbSet<Like> Likes { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Profile one-to-one with ApplicationUser, PK is UserId
            builder.Entity<Profile>()
                   .HasKey(p => p.UserId);
            builder.Entity<Profile>()
                   .HasOne(p => p.User)
                   .WithOne()
                   .HasForeignKey<Profile>(p => p.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Post relationship to ApplicationUser: restrict delete
            builder.Entity<Post>()
                   .HasOne(p => p.CreatedBy)
                   .WithMany()
                   .HasForeignKey(p => p.CreatedById)
                   .OnDelete(DeleteBehavior.Restrict);

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
