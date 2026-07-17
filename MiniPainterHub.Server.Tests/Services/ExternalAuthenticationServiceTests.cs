using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public sealed class ExternalAuthenticationServiceTests
{
    [Fact]
    public async Task Register_CreatesUserProfileAndExternalLogin_ThenRejectsReplay()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "subject-1", "artist@example.test", "Artist Name"),
            ExternalAuthPurposes.SignIn,
            null,
            "/feed");
        db.ExternalAuthExchanges.Single().HandleHash.Should().NotBe(handle);
        db.ExternalAuthExchanges.Single().HandleHash.Should().HaveLength(64);

        var exchange = await service.ExchangeAsync(handle);
        exchange.Outcome.Should().Be(ExternalAuthOutcomes.RegistrationRequired);
        exchange.Provider.Should().Be(ExternalAuthProviderNames.Google);
        exchange.Email.Should().Be("artist@example.test");
        exchange.SuggestedUserName.Should().Be("ArtistName");
        exchange.ReturnUrl.Should().Be("/feed");

        var registered = await service.RegisterAsync(handle, new ExternalAuthRegistrationDto { UserName = "googleartist" });
        registered.IsSuccess.Should().BeTrue();
        registered.Token.Should().NotBeNullOrWhiteSpace();
        db.Users.Should().ContainSingle(user => user.UserName == "googleartist" && user.EmailConfirmed);
        db.Profiles.Should().ContainSingle(profile => profile.DisplayName == "googleartist");
        db.UserLogins.Should().ContainSingle(login => login.LoginProvider == "Google" && login.ProviderKey == "subject-1");
        db.UserTokens.Should().BeEmpty();

        var replay = () => service.ExchangeAsync(handle);
        await replay.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Exchange_ReturningUser_IssuesJwtWithSameClaimsAndRoles()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var user = new ApplicationUser { UserName = "returning", Email = "returning@example.test", EmailConfirmed = true };
        (await users.CreateAsync(user, "ValidPass123!")).Succeeded.Should().BeTrue();
        (await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(user, "Admin")).Succeeded.Should().BeTrue();
        (await users.AddLoginAsync(user, new UserLoginInfo("Google", "return-subject", "Google"))).Succeeded.Should().BeTrue();
        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "return-subject", "returning@example.test", "Returning"),
            ExternalAuthPurposes.SignIn,
            null,
            "/support");

        var result = await service.ExchangeAsync(handle);

        result.Outcome.Should().Be(ExternalAuthOutcomes.Authenticated);
        result.ReturnUrl.Should().Be("/support");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        token.Subject.Should().Be(user.Id);
        token.Claims.Should().Contain(claim => claim.Type == "role" && claim.Value == "Admin");
    }

    [Fact]
    public async Task Exchange_SameEmailWithoutLogin_ReturnsConflictWithoutMerging()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var existing = new ApplicationUser { UserName = "local", Email = "same@example.test" };
        (await users.CreateAsync(existing, "ValidPass123!")).Succeeded.Should().BeTrue();
        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "other-subject", "same@example.test", "Same"),
            ExternalAuthPurposes.SignIn,
            null,
            "/");

        var result = await service.ExchangeAsync(handle);

        result.Outcome.Should().Be(ExternalAuthOutcomes.EmailConflict);
        (await users.GetLoginsAsync(existing)).Should().BeEmpty();
        var replay = () => service.ExchangeAsync(handle);
        await replay.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Exchange_LinkIntent_RequiresMatchingEmailAndPreservesExistingAccount()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var existing = new ApplicationUser { UserName = "local", Email = "same@example.test" };
        (await users.CreateAsync(existing, "ValidPass123!")).Succeeded.Should().BeTrue();
        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "link-subject", "same@example.test", "Different Name"),
            ExternalAuthPurposes.Link,
            existing.Id,
            "/account/sign-in-methods");

        var result = await service.ExchangeAsync(handle);

        result.Outcome.Should().Be(ExternalAuthOutcomes.LinkCompleted);
        existing.UserName.Should().Be("local");
        existing.EmailConfirmed.Should().BeTrue();
        (await users.GetLoginsAsync(existing)).Should().ContainSingle(login => login.ProviderKey == "link-subject");
    }

    [Fact]
    public async Task Exchange_LinkIntentWithMismatchedEmail_IsForbiddenAndConsumed()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var existing = new ApplicationUser { UserName = "local", Email = "local@example.test" };
        (await users.CreateAsync(existing, "ValidPass123!")).Succeeded.Should().BeTrue();
        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "wrong-subject", "other@example.test", "Other"),
            ExternalAuthPurposes.Link,
            existing.Id,
            "/");

        var action = () => service.ExchangeAsync(handle);

        await action.Should().ThrowAsync<ForbiddenException>();
        (await users.GetLoginsAsync(existing)).Should().BeEmpty();
        await action.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task PasswordAndDisconnect_PreventRemovingOnlySignInMethod()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var user = new ApplicationUser { UserName = "googleonly", Email = "only@example.test", EmailConfirmed = true };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        (await users.AddLoginAsync(user, new UserLoginInfo("Google", "only-subject", "Google"))).Succeeded.Should().BeTrue();

        var disconnect = () => service.DisconnectAsync(user.Id, ExternalAuthProviderNames.Google);
        await disconnect.Should().ThrowAsync<ForbiddenException>();

        var methods = await service.SetPasswordAsync(user.Id, new SetPasswordDto { NewPassword = "ValidPass123!" });
        methods.HasPassword.Should().BeTrue();
        methods.CanDisconnectGoogle.Should().BeTrue();
        var disconnected = await service.DisconnectAsync(user.Id, ExternalAuthProviderNames.Google);
        disconnected.GoogleConnected.Should().BeFalse();
        disconnected.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task DualExternalProviders_AllowOneProviderToBeDisconnectedWithoutPassword()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var user = new ApplicationUser
        {
            UserName = "dualprovider",
            Email = "dual@example.test",
            EmailConfirmed = true
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        (await users.AddLoginAsync(user, new UserLoginInfo(ExternalAuthProviderNames.Google, "google-subject", ExternalAuthProviderNames.Google))).Succeeded.Should().BeTrue();
        (await users.AddLoginAsync(user, new UserLoginInfo(ExternalAuthProviderNames.Discord, "discord-subject", ExternalAuthProviderNames.Discord))).Succeeded.Should().BeTrue();

        var methods = await service.GetSignInMethodsAsync(user.Id);

        methods.GoogleConnected.Should().BeTrue();
        methods.DiscordConnected.Should().BeTrue();
        methods.CanDisconnectGoogle.Should().BeTrue();
        methods.CanDisconnectDiscord.Should().BeTrue();

        var afterDiscord = await service.DisconnectAsync(user.Id, ExternalAuthProviderNames.Discord);
        afterDiscord.DiscordConnected.Should().BeFalse();
        afterDiscord.GoogleConnected.Should().BeTrue();
        afterDiscord.CanDisconnectGoogle.Should().BeFalse();
        await FluentActions.Invoking(() => service.DisconnectAsync(user.Id, ExternalAuthProviderNames.Google))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateExchange_RejectsUnknownProvider()
    {
        using var factory = new IntegrationTestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();

        await FluentActions.Invoking(() => service.CreateExchangeAsync(
                new ExternalIdentity("Unknown", "subject", "artist@example.test", "Artist"),
                ExternalAuthPurposes.SignIn,
                null,
                "/"))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Exchange_ExpiredOrSuspended_IsRejected()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expiredHandle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "expired", "expired@example.test", "Expired"),
            ExternalAuthPurposes.SignIn,
            null,
            "/");
        db.ExternalAuthExchanges.Single().ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        await FluentActions.Invoking(() => service.ExchangeAsync(expiredHandle)).Should().ThrowAsync<GoneException>();

        var suspended = new ApplicationUser
        {
            UserName = "suspended",
            Email = "suspended@example.test",
            SuspendedUntilUtc = DateTime.UtcNow.AddHours(1)
        };
        (await users.CreateAsync(suspended)).Succeeded.Should().BeTrue();
        (await users.AddLoginAsync(suspended, new UserLoginInfo("Google", "suspended-subject", "Google"))).Succeeded.Should().BeTrue();
        var suspendedHandle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "suspended-subject", "suspended@example.test", "Suspended"),
            ExternalAuthPurposes.SignIn,
            null,
            "/");
        await FluentActions.Invoking(() => service.ExchangeAsync(suspendedHandle)).Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Register_WhenRegistrationsAreDisabled_IsForbiddenAndCreatesNothing()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "blocked-subject", "blocked@example.test", "Blocked"),
            ExternalAuthPurposes.SignIn,
            null,
            "/");
        db.AdminSiteControls.Add(new AdminSiteControl
        {
            Key = AdminSiteControlKeys.NewRegistrations,
            Enabled = false,
            Reason = "maintenance",
            UpdatedByUserId = "admin",
            UpdatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var action = () => service.RegisterAsync(handle, new ExternalAuthRegistrationDto { UserName = "blocked" });

        await action.Should().ThrowAsync<ForbiddenException>();
        db.Users.Should().BeEmpty();
        db.Profiles.Should().BeEmpty();
        db.UserLogins.Should().BeEmpty();
    }

    [Fact]
    public async Task Exchange_LinkSubjectAlreadyOwnedByAnotherUser_ReturnsConflictWithoutMovingLogin()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var owner = new ApplicationUser { UserName = "owner", Email = "owner@example.test" };
        var target = new ApplicationUser { UserName = "target", Email = "target@example.test" };
        (await users.CreateAsync(owner, "ValidPass123!")).Succeeded.Should().BeTrue();
        (await users.CreateAsync(target, "ValidPass123!")).Succeeded.Should().BeTrue();
        (await users.AddLoginAsync(owner, new UserLoginInfo("Google", "owned-subject", "Google"))).Succeeded.Should().BeTrue();
        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "owned-subject", "target@example.test", "Target"),
            ExternalAuthPurposes.Link,
            target.Id,
            "/account/sign-in-methods");

        var action = () => service.ExchangeAsync(handle);

        await action.Should().ThrowAsync<ConflictException>();
        (await users.GetLoginsAsync(owner)).Should().ContainSingle(login => login.ProviderKey == "owned-subject");
        (await users.GetLoginsAsync(target)).Should().BeEmpty();
        await action.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Register_WhenUsernameOrEmailIsClaimedAfterExchange_RejectsRaceWithoutPartialUser()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var usernameHandle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "username-race", "new-email@example.test", "Taken"),
            ExternalAuthPurposes.SignIn,
            null,
            "/");
        (await users.CreateAsync(new ApplicationUser { UserName = "taken", Email = "other@example.test" }, "ValidPass123!")).Succeeded.Should().BeTrue();
        var usernameRace = () => service.RegisterAsync(usernameHandle, new ExternalAuthRegistrationDto { UserName = "taken" });
        await usernameRace.Should().ThrowAsync<DomainValidationException>();

        var emailHandle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "email-race", "claimed@example.test", "Claimed"),
            ExternalAuthPurposes.SignIn,
            null,
            "/");
        (await service.ExchangeAsync(emailHandle)).Outcome.Should().Be(ExternalAuthOutcomes.RegistrationRequired);
        (await users.CreateAsync(new ApplicationUser { UserName = "claimed-locally", Email = "claimed@example.test" }, "ValidPass123!")).Succeeded.Should().BeTrue();
        var emailRace = () => service.RegisterAsync(emailHandle, new ExternalAuthRegistrationDto { UserName = "google-claim" });
        await emailRace.Should().ThrowAsync<ConflictException>();

        db.Users.Count().Should().Be(2);
        db.Profiles.Should().BeEmpty();
        db.UserLogins.Should().BeEmpty();
    }

    [Theory]
    [InlineData("x")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    [InlineData("   ")]
    public async Task Register_WhenUsernameIsOutsideProfileBoundaries_IsRejectedBeforeDatabaseWrites(string userName)
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExternalAuthenticationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var handle = await service.CreateExchangeAsync(
            new ExternalIdentity("Google", "boundary-subject", "boundary@example.test", "Boundary"),
            ExternalAuthPurposes.SignIn,
            null,
            "/");

        var action = () => service.RegisterAsync(handle, new ExternalAuthRegistrationDto { UserName = userName });

        var exception = await action.Should().ThrowAsync<DomainValidationException>();
        exception.Which.Errors.Should().ContainKey(nameof(ExternalAuthRegistrationDto.UserName));
        db.Users.Should().BeEmpty();
        db.Profiles.Should().BeEmpty();
        db.UserLogins.Should().BeEmpty();
    }
}
