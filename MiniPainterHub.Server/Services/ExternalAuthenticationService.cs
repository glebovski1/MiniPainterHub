using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class ExternalAuthenticationService : IExternalAuthenticationService
{
    private static readonly TimeSpan ExchangeLifetime = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountRestrictionService _restrictions;
    private readonly IJwtTokenIssuer _tokens;
    private readonly ILogger<ExternalAuthenticationService> _logger;

    public ExternalAuthenticationService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IAccountRestrictionService restrictions,
        IJwtTokenIssuer tokens,
        ILogger<ExternalAuthenticationService> logger)
    {
        _db = db;
        _userManager = userManager;
        _restrictions = restrictions;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<string> CreateExchangeAsync(ExternalIdentity identity, string purpose, string? targetUserId, string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!ExternalAuthenticationProviders.IsAllowed(identity.Provider)
            || string.IsNullOrWhiteSpace(identity.ProviderSubject)
            || string.IsNullOrWhiteSpace(identity.VerifiedEmail))
        {
            throw new UnauthorizedAccessException("The external identity could not be verified.");
        }

        var now = DateTime.UtcNow;
        await DeleteExpiredAsync(now);
        var rawHandle = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _db.ExternalAuthExchanges.Add(new ExternalAuthExchange
        {
            Id = Guid.NewGuid(),
            HandleHash = HashHandle(rawHandle),
            Provider = identity.Provider,
            ProviderSubject = identity.ProviderSubject,
            VerifiedEmail = identity.VerifiedEmail.Trim(),
            SuggestedDisplayName = CleanSuggestedName(identity.SuggestedDisplayName, identity.VerifiedEmail),
            TargetUserId = targetUserId,
            Purpose = purpose,
            ReturnUrl = returnUrl,
            CreatedUtc = now,
            ExpiresUtc = now.Add(ExchangeLifetime)
        });
        await _db.SaveChangesAsync();
        _logger.LogInformation("External authentication exchange created. Provider={Provider}; Purpose={Purpose}; Outcome={Outcome}.", identity.Provider, purpose, "created");
        return rawHandle;
    }

    public async Task<ExternalAuthExchangeResponseDto> ExchangeAsync(string rawHandle)
    {
        var exchange = await GetActiveExchangeAsync(rawHandle);
        if (string.Equals(exchange.Purpose, ExternalAuthPurposes.Link, StringComparison.Ordinal))
        {
            return await CompleteLinkAsync(exchange);
        }

        var user = await _userManager.FindByLoginAsync(exchange.Provider, exchange.ProviderSubject);
        if (user is not null)
        {
            await _restrictions.EnsureCanLoginAsync(user);
            await ConsumeAsync(exchange);
            _logger.LogInformation("External authentication completed. Provider={Provider}; Purpose={Purpose}; Outcome={Outcome}.", exchange.Provider, exchange.Purpose, ExternalAuthOutcomes.Authenticated);
            return new ExternalAuthExchangeResponseDto
            {
                Outcome = ExternalAuthOutcomes.Authenticated,
                Provider = exchange.Provider,
                Token = await _tokens.IssueAsync(user),
                ReturnUrl = exchange.ReturnUrl
            };
        }

        var emailOwner = await _userManager.FindByEmailAsync(exchange.VerifiedEmail);
        if (emailOwner is not null)
        {
            await ConsumeAsync(exchange);
            _logger.LogInformation("External authentication completed. Provider={Provider}; Purpose={Purpose}; Outcome={Outcome}.", exchange.Provider, exchange.Purpose, ExternalAuthOutcomes.EmailConflict);
            return new ExternalAuthExchangeResponseDto
            {
                Outcome = ExternalAuthOutcomes.EmailConflict,
                Provider = exchange.Provider,
                Email = exchange.VerifiedEmail,
                ReturnUrl = exchange.ReturnUrl
            };
        }

        _logger.LogInformation("External authentication completed. Provider={Provider}; Purpose={Purpose}; Outcome={Outcome}.", exchange.Provider, exchange.Purpose, ExternalAuthOutcomes.RegistrationRequired);
        return new ExternalAuthExchangeResponseDto
        {
            Outcome = ExternalAuthOutcomes.RegistrationRequired,
            Provider = exchange.Provider,
            Email = exchange.VerifiedEmail,
            SuggestedUserName = exchange.SuggestedDisplayName,
            ReturnUrl = exchange.ReturnUrl
        };
    }

    public async Task<AuthResponseDto> RegisterAsync(string rawHandle, ExternalAuthRegistrationDto request)
    {
        var exchange = await GetActiveExchangeAsync(rawHandle);
        if (!string.Equals(exchange.Purpose, ExternalAuthPurposes.SignIn, StringComparison.Ordinal))
        {
            throw new GoneException("The external authentication attempt is no longer available.");
        }

        await _restrictions.EnsureCanRegisterAsync();
        if (await _userManager.FindByLoginAsync(exchange.Provider, exchange.ProviderSubject) is not null
            || await _userManager.FindByEmailAsync(exchange.VerifiedEmail) is not null)
        {
            throw new ConflictException("An account already exists for this external identity or email address.");
        }

        var userName = request.UserName?.Trim() ?? string.Empty;
        if (userName.Length < ExternalAuthRules.MinUserNameLength || userName.Length > ExternalAuthRules.MaxUserNameLength)
        {
            throw new DomainValidationException("External registration failed.", new Dictionary<string, string[]>
            {
                [nameof(request.UserName)] = [$"Username must be between {ExternalAuthRules.MinUserNameLength} and {ExternalAuthRules.MaxUserNameLength} characters."]
            });
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = exchange.VerifiedEmail,
            EmailConfirmed = true,
            DisplayName = userName
        };

        async Task CreateAccountAsync()
        {
            EnsureIdentitySuccess(await _userManager.CreateAsync(user), "External registration failed.");
            _db.Profiles.Add(new Profile { UserId = user.Id, DisplayName = userName, Bio = null, AvatarUrl = null });
            await _db.SaveChangesAsync();
            EnsureIdentitySuccess(
                await _userManager.AddLoginAsync(user, new UserLoginInfo(exchange.Provider, exchange.ProviderSubject, exchange.Provider)),
                "External registration failed.");
            await ConsumeAsync(exchange);
        }

        if (_db.Database.IsRelational())
        {
            await ExecuteInResilientTransactionAsync(CreateAccountAsync);
        }
        else
        {
            try
            {
                await CreateAccountAsync();
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(user.Id))
                {
                    var created = await _userManager.FindByIdAsync(user.Id);
                    if (created is not null)
                    {
                        await _userManager.DeleteAsync(created);
                    }
                }
                throw;
            }
        }

        _logger.LogInformation("External authentication registration completed. Provider={Provider}; Outcome={Outcome}.", exchange.Provider, "registered");
        return new AuthResponseDto { IsSuccess = true, Token = await _tokens.IssueAsync(user) };
    }

    public async Task<SignInMethodsDto> GetSignInMethodsAsync(string userId)
    {
        var user = await RequireUserAsync(userId);
        return await CreateSignInMethodsAsync(user);
    }

    public async Task<SignInMethodsDto> SetPasswordAsync(string userId, SetPasswordDto request)
    {
        var user = await RequireUserAsync(userId);
        await _restrictions.EnsureCanLoginAsync(user);
        if (await _userManager.HasPasswordAsync(user))
        {
            throw new ConflictException("A password is already configured for this account.");
        }

        EnsureIdentitySuccess(await _userManager.AddPasswordAsync(user, request.NewPassword), "Password setup failed.");
        _logger.LogInformation("Sign-in method changed. Method={Method}; Outcome={Outcome}.", "Password", "added");
        return await CreateSignInMethodsAsync(user);
    }

    public async Task<SignInMethodsDto> DisconnectAsync(string userId, string provider)
    {
        if (!ExternalAuthenticationProviders.TryResolve(provider, out var canonicalProvider, out _))
        {
            throw new NotFoundException("The external sign-in provider is not available.");
        }

        var user = await RequireUserAsync(userId);
        await _restrictions.EnsureCanLoginAsync(user);
        var logins = await _userManager.GetLoginsAsync(user);
        var login = logins.SingleOrDefault(l => string.Equals(l.LoginProvider, canonicalProvider, StringComparison.Ordinal));
        if (login is null)
        {
            throw new NotFoundException($"{canonicalProvider} is not connected to this account.");
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (!hasPassword && logins.Count <= 1)
        {
            throw new ForbiddenException("Set a password before disconnecting the only sign-in method.");
        }

        EnsureIdentitySuccess(
            await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey),
            $"{canonicalProvider} could not be disconnected.");
        _logger.LogInformation("Sign-in method changed. Method={Method}; Outcome={Outcome}.", canonicalProvider, "disconnected");
        return await CreateSignInMethodsAsync(user);
    }

    private async Task<ExternalAuthExchangeResponseDto> CompleteLinkAsync(ExternalAuthExchange exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange.TargetUserId))
        {
            throw new GoneException("The external authentication attempt is no longer available.");
        }

        var user = await RequireUserAsync(exchange.TargetUserId);
        await _restrictions.EnsureCanLoginAsync(user);
        var normalizedExternalEmail = _userManager.NormalizeEmail(exchange.VerifiedEmail);
        if (string.IsNullOrWhiteSpace(user.NormalizedEmail)
            || !string.Equals(user.NormalizedEmail, normalizedExternalEmail, StringComparison.Ordinal))
        {
            await ConsumeAsync(exchange);
            throw new ForbiddenException($"The {exchange.Provider} account email must match your MiniPainterHub email.");
        }

        var owner = await _userManager.FindByLoginAsync(exchange.Provider, exchange.ProviderSubject);
        if (owner is not null && owner.Id != user.Id)
        {
            await ConsumeAsync(exchange);
            throw new ConflictException($"That {exchange.Provider} account is already connected to another user.");
        }

        async Task LinkAccountAsync()
        {
            if (owner is null)
            {
                EnsureIdentitySuccess(
                    await _userManager.AddLoginAsync(user, new UserLoginInfo(exchange.Provider, exchange.ProviderSubject, exchange.Provider)),
                    $"{exchange.Provider} could not be connected.");
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                EnsureIdentitySuccess(await _userManager.UpdateAsync(user), $"{exchange.Provider} could not be connected.");
            }

            await ConsumeAsync(exchange);
        }

        if (_db.Database.IsRelational())
        {
            await ExecuteInResilientTransactionAsync(LinkAccountAsync);
        }
        else
        {
            await LinkAccountAsync();
        }
        _logger.LogInformation("External authentication completed. Provider={Provider}; Purpose={Purpose}; Outcome={Outcome}.", exchange.Provider, exchange.Purpose, ExternalAuthOutcomes.LinkCompleted);
        return new ExternalAuthExchangeResponseDto
        {
            Outcome = ExternalAuthOutcomes.LinkCompleted,
            Provider = exchange.Provider,
            ReturnUrl = exchange.ReturnUrl
        };
    }

    private async Task<ApplicationUser> RequireUserAsync(string userId) =>
        await _userManager.FindByIdAsync(userId) ?? throw new UnauthorizedAccessException("User is not authenticated.");

    private async Task<SignInMethodsDto> CreateSignInMethodsAsync(ApplicationUser user)
    {
        var logins = await _userManager.GetLoginsAsync(user);
        var hasPassword = await _userManager.HasPasswordAsync(user);
        var googleConnected = logins.Any(l => string.Equals(l.LoginProvider, ExternalAuthProviderNames.Google, StringComparison.Ordinal));
        var discordConnected = logins.Any(l => string.Equals(l.LoginProvider, ExternalAuthProviderNames.Discord, StringComparison.Ordinal));
        return new SignInMethodsDto
        {
            HasPassword = hasPassword,
            GoogleConnected = googleConnected,
            CanDisconnectGoogle = googleConnected && (hasPassword || logins.Count > 1),
            DiscordConnected = discordConnected,
            CanDisconnectDiscord = discordConnected && (hasPassword || logins.Count > 1)
        };
    }

    private async Task<ExternalAuthExchange> GetActiveExchangeAsync(string rawHandle)
    {
        if (string.IsNullOrWhiteSpace(rawHandle))
        {
            throw new GoneException("The external authentication attempt is no longer available.");
        }

        var hash = HashHandle(rawHandle);
        var exchange = await _db.ExternalAuthExchanges.SingleOrDefaultAsync(e => e.HandleHash == hash);
        if (exchange is null || exchange.ConsumedUtc.HasValue || exchange.ExpiresUtc <= DateTime.UtcNow)
        {
            if (exchange is { ExpiresUtc: var expiresUtc } && expiresUtc <= DateTime.UtcNow)
            {
                _db.ExternalAuthExchanges.Remove(exchange);
                await _db.SaveChangesAsync();
            }
            _logger.LogInformation("External authentication exchange rejected. Outcome={Outcome}.", exchange?.ConsumedUtc.HasValue == true ? "replay" : "expired_or_missing");
            throw new GoneException("The external authentication attempt is no longer available.");
        }

        return exchange;
    }

    private async Task ConsumeAsync(ExternalAuthExchange exchange)
    {
        var now = DateTime.UtcNow;
        if (_db.Database.IsRelational())
        {
            var affected = await _db.ExternalAuthExchanges
                .Where(e => e.Id == exchange.Id && e.ConsumedUtc == null && e.ExpiresUtc > now)
                .ExecuteUpdateAsync(updates => updates.SetProperty(e => e.ConsumedUtc, now));
            if (affected != 1)
            {
                throw new GoneException("The external authentication attempt is no longer available.");
            }
            exchange.ConsumedUtc = now;
            return;
        }

        if (exchange.ConsumedUtc.HasValue || exchange.ExpiresUtc <= now)
        {
            throw new GoneException("The external authentication attempt is no longer available.");
        }
        exchange.ConsumedUtc = now;
        await _db.SaveChangesAsync();
    }

    private async Task DeleteExpiredAsync(DateTime now)
    {
        if (_db.Database.IsRelational())
        {
            await _db.ExternalAuthExchanges.Where(e => e.ExpiresUtc <= now).ExecuteDeleteAsync();
            return;
        }

        var expired = await _db.ExternalAuthExchanges.Where(e => e.ExpiresUtc <= now).ToListAsync();
        _db.ExternalAuthExchanges.RemoveRange(expired);
        if (expired.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
    }

    private async Task ExecuteInResilientTransactionAsync(Func<Task> operation)
    {
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                await operation();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private static string HashHandle(string rawHandle) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawHandle)));

    private string CleanSuggestedName(string? name, string email)
    {
        var allowed = _userManager.Options.User.AllowedUserNameCharacters;
        var preferred = string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name.Trim();
        var candidate = FilterUserName(preferred, allowed);
        if (candidate.Length < ExternalAuthRules.MinUserNameLength)
        {
            candidate = FilterUserName(email.Split('@')[0], allowed);
        }

        if (candidate.Length < ExternalAuthRules.MinUserNameLength)
        {
            candidate = "painter";
        }

        return candidate.Length <= ExternalAuthRules.MaxUserNameLength
            ? candidate
            : candidate[..ExternalAuthRules.MaxUserNameLength];
    }

    private static string FilterUserName(string value, string? allowedCharacters) =>
        string.IsNullOrEmpty(allowedCharacters)
            ? value
            : new string(value.Where(allowedCharacters.Contains).ToArray());

    private static void EnsureIdentitySuccess(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code) ? "Identity" : error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray(), StringComparer.OrdinalIgnoreCase);
        throw new DomainValidationException(message, errors);
    }
}
