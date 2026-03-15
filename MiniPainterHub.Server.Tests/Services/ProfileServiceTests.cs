using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Tests.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class ProfileServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenValidInput_CreatesAndReturnsProfile()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1", "artist");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var service = new ProfileService(context, new StubImageService());
        var dto = new CreateUserProfileDto
        {
            DisplayName = "  Display Name  ",
            Bio = "  Painter bio  "
        };

        var created = await service.CreateAsync(user.Id, dto);

        created.UserId.Should().Be(user.Id);
        created.DisplayName.Should().Be("Display Name");
        created.Bio.Should().Be("Painter bio");
        created.UserName.Should().Be(user.UserName);

        var stored = await context.Profiles.SingleAsync();
        stored.DisplayName.Should().Be("Display Name");
        stored.Bio.Should().Be("Painter bio");
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenProfileExists_ReturnsProfile()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1", "muralist");
        await context.Users.AddAsync(user);
        await context.Profiles.AddAsync(new MiniPainterHub.Server.Entities.Profile
        {
            UserId = user.Id,
            DisplayName = "Mural Artist",
            Bio = "Bio",
            AvatarUrl = "https://cdn/avatar.jpg"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context, new StubImageService());

        var profile = await service.GetByUserIdAsync(user.Id);

        profile.Should().NotBeNull();
        profile!.UserId.Should().Be(user.Id);
        profile.AvatarUrl.Should().Be("https://cdn/avatar.jpg");
        profile.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task UpdateAsync_WhenValidInput_UpdatesAndReturnsProfile()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1", "updater");
        await context.Users.AddAsync(user);
        await context.Profiles.AddAsync(new MiniPainterHub.Server.Entities.Profile
        {
            UserId = user.Id,
            DisplayName = "Old",
            Bio = "Old bio"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context, new StubImageService());
        var dto = new UpdateUserProfileDto
        {
            DisplayName = "  New Name  ",
            Bio = "  New bio  "
        };

        var updated = await service.UpdateAsync(user.Id, dto);

        updated.DisplayName.Should().Be("New Name");
        updated.Bio.Should().Be("New bio");
        (await context.Profiles.SingleAsync()).DisplayName.Should().Be("New Name");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public async Task CreateAsync_WhenDisplayNameInvalid_ThrowsDomainValidationException(string displayName)
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var service = new ProfileService(context, new StubImageService());

        var act = async () => await service.CreateAsync(user.Id, new CreateUserProfileDto
        {
            DisplayName = displayName,
            Bio = "Valid bio"
        });

        var ex = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Profile data is invalid.");
        ex.Which.Errors.Should().ContainKey("displayName");
    }

    [Fact]
    public async Task UpdateAsync_WhenBioIsTooLong_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1");
        await context.Users.AddAsync(user);
        await context.Profiles.AddAsync(new MiniPainterHub.Server.Entities.Profile
        {
            UserId = user.Id,
            DisplayName = "Name",
            Bio = "Bio"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context, new StubImageService());
        var longBio = new string('b', 501);

        var act = async () => await service.UpdateAsync(user.Id, new UpdateUserProfileDto
        {
            DisplayName = "Name",
            Bio = longBio
        });

        var ex = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Profile data is invalid.");
        ex.Which.Errors.Should().ContainKey("bio");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task UploadAvatarAsync_WhenUserIdMissing_ThrowsArgumentException(string? userId)
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());
        var file = CreateFormFile(new byte[] { 1, 2, 3 }, "avatar.png", "image/png");

        var act = async () => await service.UploadAvatarAsync(userId!, file);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenFileMissing_ThrowsArgumentNullException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());

        var act = async () => await service.UploadAvatarAsync("user-1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenFileIsEmpty_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());
        var file = CreateFormFile(Array.Empty<byte>(), "avatar.png", "image/png");

        var act = async () => await service.UploadAvatarAsync("user-1", file);

        var ex = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Invalid avatar upload.");
        ex.Which.Errors.Should().ContainKey("file");
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenFileExceedsSizeLimit_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());
        var file = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 5_000_001, "file", "avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var act = async () => await service.UploadAvatarAsync("user-1", file);

        var ex = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Invalid avatar upload.");
        ex.Which.Errors.Should().ContainKey("file")
            .WhoseValue.Should().Contain("Max avatar size is 5 MB.");
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenMimeTypeUnsupported_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());
        var file = CreateFormFile(new byte[] { 1, 2, 3 }, "avatar.txt", "text/plain");

        var act = async () => await service.UploadAvatarAsync("user-1", file);

        var ex = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Invalid avatar upload.");
        ex.Which.Errors.Should().ContainKey("file");
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenValidImage_StoresAvatarAndUpdatesProfile()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1", "avatar-user");
        await context.Users.AddAsync(user);
        await context.Profiles.AddAsync(new MiniPainterHub.Server.Entities.Profile
        {
            UserId = user.Id,
            DisplayName = "Display",
            Bio = "Bio"
        });
        await context.SaveChangesAsync();

        var imageService = new StubImageService();
        var service = new ProfileService(context, imageService);
        var file = CreateFormFile(GetTinyPng(), "avatar.png", "image/png");

        var updated = await service.UploadAvatarAsync(user.Id, file);

        imageService.UploadedFileName.Should().Be($"avatar_{user.Id}.jpg");
        updated.AvatarUrl.Should().Be("https://images.test/avatar_user-1.jpg");
        (await context.Profiles.SingleAsync()).AvatarUrl.Should().Be("https://images.test/avatar_user-1.jpg");
    }

    [Fact]
    public async Task SetAvatarUrlAsync_WhenProfileExists_UpdatesAvatarUrl()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1", "avatar-updater");
        await context.Users.AddAsync(user);
        await context.Profiles.AddAsync(new MiniPainterHub.Server.Entities.Profile
        {
            UserId = user.Id,
            DisplayName = "Display",
            Bio = "Bio"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context, new StubImageService());

        var updated = await service.SetAvatarUrlAsync(user.Id, "https://images.test/avatar.jpg");

        updated.AvatarUrl.Should().Be("https://images.test/avatar.jpg");
        (await context.Profiles.SingleAsync()).AvatarUrl.Should().Be("https://images.test/avatar.jpg");
    }

    [Fact]
    public async Task SetAvatarUrlAsync_WhenProfileMissing_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());

        var act = async () => await service.SetAvatarUrlAsync("missing-user", "https://images.test/avatar.jpg");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Profile not found.");
    }

    [Fact]
    public async Task GetUserProfileById_WhenProfileExists_ReturnsPrivateProfile()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1", "lookup-user");
        await context.Users.AddAsync(user);
        await context.Profiles.AddAsync(new MiniPainterHub.Server.Entities.Profile
        {
            UserId = user.Id,
            DisplayName = "Lookup Name",
            Bio = "Lookup Bio"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context, new StubImageService());

        var profile = await service.GetUserProfileById(user.Id);

        profile.UserId.Should().Be(user.Id);
        profile.DisplayName.Should().Be("Lookup Name");
        profile.Bio.Should().Be("Lookup Bio");
        profile.UserName.Should().Be(user.UserName);
        profile.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetPublicProfileById_WhenProfileExists_ReturnsProfileWithoutEmail()
    {
        await using var context = AppDbContextFactory.Create();
        var user = CreateUser("user-1", "lookup-user");
        await context.Users.AddAsync(user);
        await context.Profiles.AddAsync(new MiniPainterHub.Server.Entities.Profile
        {
            UserId = user.Id,
            DisplayName = "Lookup Name",
            Bio = "Lookup Bio"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context, new StubImageService());

        var profile = await service.GetPublicProfileById(user.Id);

        profile.UserId.Should().Be(user.Id);
        profile.DisplayName.Should().Be("Lookup Name");
        profile.Bio.Should().Be("Lookup Bio");
        profile.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task GetUserProfileById_WhenProfileMissing_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());

        var act = async () => await service.GetUserProfileById("missing-user");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Profile not found.");
    }

    [Fact]
    public async Task GetPublicProfileById_WhenProfileMissing_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ProfileService(context, new StubImageService());

        var act = async () => await service.GetPublicProfileById("missing-user");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Profile not found.");
    }

    private static ApplicationUser CreateUser(string id, string? userName = null)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = userName ?? $"user-{id}",
            Email = $"{id}@example.test",
            DateJoined = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static IFormFile CreateFormFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] GetTinyPng()
    {
        using var image = new Image<Rgba32>(4, 4);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class StubImageService : IImageService
    {
        public string? UploadedFileName { get; private set; }

        public Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            UploadedFileName = fileName;
            return Task.FromResult($"https://images.test/{fileName}");
        }

        public Task<Stream> DownloadAsync(string fileName) => Task.FromResult<Stream>(Stream.Null);

        public Task DeleteAsync(string fileName) => Task.CompletedTask;
    }
}
