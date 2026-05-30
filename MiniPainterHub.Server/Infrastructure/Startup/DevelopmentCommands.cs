using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub;

public partial class Program
{
    private static DevelopmentCommand? TryParseDevelopmentCommand(string[] args)
    {
        var seedDevContent = args.Contains("--seed-dev-content", StringComparer.OrdinalIgnoreCase);
        var generateDevAvatars = args.Contains("--generate-dev-avatars", StringComparer.OrdinalIgnoreCase);

        if (!seedDevContent && !generateDevAvatars)
        {
            return null;
        }

        if (seedDevContent && generateDevAvatars)
        {
            throw new InvalidOperationException("Specify either --seed-dev-content or --generate-dev-avatars, not both.");
        }

        string? avatarsDirectory = null;
        string? postImagesDirectory = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--avatars-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("The --avatars-dir option requires a value.");
                }

                avatarsDirectory = args[i + 1];
                i++;
                continue;
            }

            if (string.Equals(args[i], "--post-images-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("The --post-images-dir option requires a value.");
                }

                postImagesDirectory = args[i + 1];
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(avatarsDirectory))
        {
            var commandName = seedDevContent ? "--seed-dev-content" : "--generate-dev-avatars";
            throw new InvalidOperationException($"The {commandName} command requires --avatars-dir <path>.");
        }

        if (generateDevAvatars && !string.IsNullOrWhiteSpace(postImagesDirectory))
        {
            throw new InvalidOperationException("The --generate-dev-avatars command does not support --post-images-dir.");
        }

        var commandKind = seedDevContent
            ? DevelopmentCommandKind.SeedContent
            : DevelopmentCommandKind.GenerateAvatarsOnly;

        return new DevelopmentCommand(commandKind, avatarsDirectory, postImagesDirectory);
    }

    private static async Task RunDevelopmentCommandAsync(WebApplication app, DevelopmentCommand command)
    {
        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException($"The {GetCommandName(command.Kind)} command can only run in Development.");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentContentSeeder>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        switch (command.Kind)
        {
            case DevelopmentCommandKind.SeedContent:
                {
                    var result = await seeder.ResetAndSeedAsync(command.AvatarsDirectory, command.PostImagesDirectory);

                    logger.LogInformation(
                        "Development seed complete. Users: {Users}, posts: {Posts}, comments: {Comments}, avatars: {Avatars}, post images: {PostImages}.",
                        result.UsersCreated,
                        result.PostsCreated,
                        result.CommentsCreated,
                        result.AvatarsImported,
                        result.PostImagesImported);

                    foreach (var credential in result.Credentials)
                    {
                        logger.LogInformation(
                            "Seeded user {UserName} ({Email}) roles [{Roles}] password {Password}",
                            credential.UserName,
                            credential.Email,
                            string.Join(", ", credential.Roles),
                            credential.Password);
                    }

                    break;
                }
            case DevelopmentCommandKind.GenerateAvatarsOnly:
                {
                    var result = await seeder.GenerateAvatarsOnlyAsync(command.AvatarsDirectory);

                    logger.LogInformation(
                        "Development avatar generation complete. Avatars: {Avatars}, existing users updated: {UsersUpdated}.",
                        result.AvatarsImported,
                        result.ExistingUsersUpdated);

                    foreach (var avatar in result.Avatars)
                    {
                        logger.LogInformation(
                            "Prepared avatar for {UserName} from {SourceFile} at {AvatarUrl}",
                            avatar.UserName,
                            avatar.SourceFileName,
                            avatar.AvatarUrl);
                    }

                    break;
                }
            default:
                throw new InvalidOperationException($"Unsupported development command '{command.Kind}'.");
        }
    }

    private static string GetCommandName(DevelopmentCommandKind kind) => kind switch
    {
        DevelopmentCommandKind.SeedContent => "--seed-dev-content",
        DevelopmentCommandKind.GenerateAvatarsOnly => "--generate-dev-avatars",
        _ => throw new InvalidOperationException($"Unsupported development command '{kind}'.")
    };

    private enum DevelopmentCommandKind
    {
        SeedContent,
        GenerateAvatarsOnly
    }

    private sealed record DevelopmentCommand(DevelopmentCommandKind Kind, string AvatarsDirectory, string? PostImagesDirectory);
}
