using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
        public DbSet<Tag> Tags { get; set; } = default!;
        public DbSet<PostTag> PostTags { get; set; } = default!;
        public DbSet<Comment> Comments { get; set; } = default!;
        public DbSet<Like> Likes { get; set; } = default!;
        public DbSet<ModerationAuditLog> ModerationAuditLogs { get; set; } = default!;
        public DbSet<ContentReport> ContentReports { get; set; } = default!;
        public DbSet<Follow> Follows { get; set; } = default!;
        public DbSet<Conversation> Conversations { get; set; } = default!;
        public DbSet<ConversationParticipant> ConversationParticipants { get; set; } = default!;
        public DbSet<DirectMessage> DirectMessages { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Profile>(b =>
            {
                b.HasKey(p => p.UserId);

                b.Property(p => p.UserId).HasMaxLength(450);
                b.Property(p => p.DisplayName).HasMaxLength(80);
                b.Property(p => p.Bio).HasMaxLength(500);
                b.Property(p => p.AvatarUrl).HasMaxLength(2048);

                b.HasOne(p => p.User)
                    .WithOne(u => u.Profile)
                    .HasForeignKey<Profile>(p => p.UserId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Post>()
                .HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PostImage>()
                .HasOne(pi => pi.Post)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PostTag>(b =>
            {
                b.HasKey(pt => new { pt.PostId, pt.TagId });

                b.HasOne(pt => pt.Post)
                    .WithMany(p => p.PostTags)
                    .HasForeignKey(pt => pt.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(pt => pt.Tag)
                    .WithMany(t => t.PostTags)
                    .HasForeignKey(pt => pt.TagId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Tag>(b =>
            {
                b.Property(t => t.DisplayName).HasMaxLength(MiniPainterHub.Common.DTOs.TagRules.MaxTagLength).IsRequired();
                b.Property(t => t.NormalizedName).HasMaxLength(MiniPainterHub.Common.DTOs.TagRules.MaxTagLength).IsRequired();
                b.Property(t => t.Slug).HasMaxLength(64).IsRequired();

                b.HasIndex(t => t.NormalizedName).IsUnique();
                b.HasIndex(t => t.Slug).IsUnique();
            });

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
            builder.Entity<Comment>()
                .HasOne(c => c.ModeratedByUser)
                .WithMany()
                .HasForeignKey(c => c.ModeratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Post>()
                .HasOne(p => p.ModeratedByUser)
                .WithMany()
                .HasForeignKey(p => p.ModeratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>(b =>
            {
                b.Property(u => u.SuspensionReason).HasMaxLength(500);
            });

            builder.Entity<ModerationAuditLog>(b =>
            {
                b.Property(m => m.ActorUserId).HasMaxLength(450).IsRequired();
                b.Property(m => m.ActorRole).HasMaxLength(64).IsRequired();
                b.Property(m => m.ActionType).HasMaxLength(64).IsRequired();
                b.Property(m => m.TargetType).HasMaxLength(64).IsRequired();
                b.Property(m => m.TargetId).HasMaxLength(128).IsRequired();
                b.Property(m => m.Reason).HasMaxLength(500);

                b.HasIndex(m => m.CreatedUtc);
                b.HasIndex(m => new { m.TargetType, m.TargetId });
                b.HasIndex(m => m.ActorUserId);
            });

            builder.Entity<ContentReport>(b =>
            {
                b.Property(r => r.ReporterUserId).HasMaxLength(450).IsRequired();
                b.Property(r => r.TargetType).HasMaxLength(64).IsRequired();
                b.Property(r => r.TargetId).HasMaxLength(128).IsRequired();
                b.Property(r => r.ReasonCode).HasMaxLength(32).IsRequired();
                b.Property(r => r.Details).HasMaxLength(1000);
                b.Property(r => r.Status).HasMaxLength(32).IsRequired();
                b.Property(r => r.ReviewedByUserId).HasMaxLength(450);
                b.Property(r => r.ResolutionNote).HasMaxLength(500);

                b.HasIndex(r => new { r.TargetType, r.TargetId, r.Status });
                b.HasIndex(r => new { r.ReporterUserId, r.TargetType, r.TargetId, r.Status });
                b.HasIndex(r => new { r.Status, r.CreatedUtc });
            });

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

            builder.Entity<Follow>(b =>
            {
                b.HasKey(f => new { f.FollowerUserId, f.FollowedUserId });

                b.Property(f => f.FollowerUserId).HasMaxLength(450);
                b.Property(f => f.FollowedUserId).HasMaxLength(450);

                b.HasOne(f => f.FollowerUser)
                    .WithMany(u => u.Following)
                    .HasForeignKey(f => f.FollowerUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(f => f.FollowedUser)
                    .WithMany(u => u.Followers)
                    .HasForeignKey(f => f.FollowedUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Conversation>(b =>
            {
                b.Property(c => c.CreatedUtc).IsRequired();
                b.Property(c => c.UpdatedUtc).IsRequired();
            });

            builder.Entity<ConversationParticipant>(b =>
            {
                b.HasKey(cp => new { cp.ConversationId, cp.UserId });
                b.Property(cp => cp.UserId).HasMaxLength(450);

                b.HasOne(cp => cp.Conversation)
                    .WithMany(c => c.Participants)
                    .HasForeignKey(cp => cp.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(cp => cp.User)
                    .WithMany(u => u.ConversationParticipants)
                    .HasForeignKey(cp => cp.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<DirectMessage>(b =>
            {
                b.Property(m => m.Body).HasMaxLength(2000);

                b.HasOne(m => m.Conversation)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(m => m.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(m => m.SenderUser)
                    .WithMany(u => u.SentMessages)
                    .HasForeignKey(m => m.SenderUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
