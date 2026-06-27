using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Features.Conversations;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class SocialUniquenessTests
{
    [Fact]
    public void AppDbContext_Model_EnforcesSocialUniqueness()
    {
        using var context = AppDbContextFactory.Create();

        var likeType = context.Model.FindEntityType(typeof(Like))!;
        likeType.GetIndexes()
            .Should()
            .ContainSingle(index =>
                index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(Like.PostId), nameof(Like.UserId) }));

        var conversationType = context.Model.FindEntityType(typeof(Conversation))!;
        conversationType.GetIndexes()
            .Should()
            .ContainSingle(index =>
                index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(Conversation.DirectConversationKey) }));
    }

    [Fact]
    public async Task ToggleAsync_WhenDuplicateLikeInsertRaceOccurs_ReturnsLikedState()
    {
        var options = CreateOptions();
        await using (var seed = new AppDbContext(options))
        {
            var user = TestData.CreateUser("user-1");
            await seed.Users.AddAsync(user);
            await seed.Posts.AddAsync(TestData.CreatePost(1, user.Id));
            await seed.SaveChangesAsync();
        }

        await using var context = new DuplicateLikeRaceDbContext(options);
        var service = new LikeService(context);

        var result = await service.ToggleAsync(1, "user-1");

        result.Should().BeTrue();
        await using var verify = new AppDbContext(options);
        (await verify.Likes.CountAsync(l => l.PostId == 1 && l.UserId == "user-1")).Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateDirectConversationAsync_WhenDuplicateInsertRaceOccurs_ReturnsWinningConversation()
    {
        var options = CreateOptions();
        await using (var seed = new AppDbContext(options))
        {
            await seed.Users.AddRangeAsync(TestData.CreateUser("user-a", "alpha"), TestData.CreateUser("user-b", "bravo"));
            await seed.SaveChangesAsync();
        }

        await using var context = new DuplicateConversationRaceDbContext(options);
        var service = new ConversationService(context, new NoopChatNotifier(), NullLogger<ConversationService>.Instance);

        var result = await service.GetOrCreateDirectConversationAsync("user-a", "user-b");

        result.OtherUser.UserId.Should().Be("user-b");
        await using var verify = new AppDbContext(options);
        var conversations = await verify.Conversations.Include(c => c.Participants).ToListAsync();
        conversations.Should().ContainSingle();
        conversations[0].DirectConversationKey.Should().Be(DirectConversationKey.Create("user-a", "user-b"));
    }

    [Fact]
    public async Task GetOrCreateDirectConversationAsync_WhenCalledInReverseOrder_ReturnsSameConversation()
    {
        var options = CreateOptions();
        await using (var seed = new AppDbContext(options))
        {
            await seed.Users.AddRangeAsync(TestData.CreateUser("user-a", "alpha"), TestData.CreateUser("user-b", "bravo"));
            await seed.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var service = new ConversationService(context, new NoopChatNotifier(), NullLogger<ConversationService>.Instance);

        var first = await service.GetOrCreateDirectConversationAsync("user-a", "user-b");
        var second = await service.GetOrCreateDirectConversationAsync("user-b", "user-a");

        second.Id.Should().Be(first.Id);
        second.OtherUser.UserId.Should().Be("user-a");
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private sealed class DuplicateLikeRaceDbContext : AppDbContext
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public DuplicateLikeRaceDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var pendingLike = ChangeTracker.Entries<Like>()
                .FirstOrDefault(entry => entry.State == EntityState.Added)
                ?.Entity;

            if (pendingLike is not null)
            {
                foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                await using var winningContext = new AppDbContext(_options);
                winningContext.Likes.Add(new Like
                {
                    PostId = pendingLike.PostId,
                    UserId = pendingLike.UserId,
                    CreatedAt = DateTime.UtcNow
                });
                await winningContext.SaveChangesAsync(cancellationToken);

                throw new DbUpdateException("Simulated duplicate like insert.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class DuplicateConversationRaceDbContext : AppDbContext
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public DuplicateConversationRaceDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var pendingConversation = ChangeTracker.Entries<Conversation>()
                .FirstOrDefault(entry => entry.State == EntityState.Added && entry.Entity.DirectConversationKey is not null)
                ?.Entity;

            if (pendingConversation is not null)
            {
                var participants = pendingConversation.Participants
                    .Select(participant => participant.UserId)
                    .ToList();

                foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                var now = DateTime.UtcNow;
                await using var winningContext = new AppDbContext(_options);
                winningContext.Conversations.Add(new Conversation
                {
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    DirectConversationKey = pendingConversation.DirectConversationKey,
                    Participants = participants
                        .Select(userId => new ConversationParticipant
                        {
                            UserId = userId,
                            JoinedUtc = now
                        })
                        .ToList()
                });
                await winningContext.SaveChangesAsync(cancellationToken);

                throw new DbUpdateException("Simulated duplicate direct conversation insert.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class NoopChatNotifier : IChatNotifier
    {
        public Task NotifyMessageSentAsync(int conversationId, DirectMessageDto message, IEnumerable<string> participantUserIds) =>
            Task.CompletedTask;

        public Task NotifyConversationReadAsync(int conversationId, string readerUserId, DateTime lastReadUtc, IEnumerable<string> participantUserIds) =>
            Task.CompletedTask;
    }
}
