using System.Threading.Tasks;
using MiniPainterHub.Server.Tests.Infrastructure;

namespace MiniPainterHub.Server.Tests.Infrastructure;

internal static class IntegrationSeedExtensions
{
    public static Task SeedUserAsync(
        this IntegrationTestApplicationFactory factory,
        string userId,
        string? userName = null)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Users.AddAsync(TestData.CreateUser(userId, userName));
            await db.SaveChangesAsync();
        });
    }

    public static Task SeedUserAndPostAsync(
        this IntegrationTestApplicationFactory factory,
        string userId,
        int postId,
        int imageCount = 0)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            var user = TestData.CreateUser(userId, userId);
            await db.Users.AddAsync(user);
            await db.Posts.AddAsync(TestData.CreatePost(postId, user.Id, imageCount));
            await db.SaveChangesAsync();
        });
    }

    public static Task SeedCommentAsync(
        this IntegrationTestApplicationFactory factory,
        int commentId,
        int postId,
        string authorId,
        string? text = null)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            var comment = TestData.CreateComment(commentId, postId, authorId);
            if (!string.IsNullOrWhiteSpace(text))
            {
                comment.Text = text;
            }

            await db.Comments.AddAsync(comment);
            await db.SaveChangesAsync();
        });
    }

    public static Task SeedProfileAsync(
        this IntegrationTestApplicationFactory factory,
        string userId,
        string? displayName = null,
        string? bio = null)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Profiles.AddAsync(TestData.CreateProfile(userId, displayName, bio));
            await db.SaveChangesAsync();
        });
    }
}
