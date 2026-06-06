using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public sealed class AdminSiteControlService : IAdminSiteControlService
    {
        private static readonly string[] ActorRolePriority = { "Admin", "Moderator" };
        private static readonly IReadOnlyDictionary<string, ControlDefinition> Definitions =
            new Dictionary<string, ControlDefinition>(StringComparer.Ordinal)
            {
                [AdminSiteControlKeys.PublicSite] = new("Public site", "Pause public access while keeping admin bypass available."),
                [AdminSiteControlKeys.NewPosts] = new("New posts and uploads", "Block new post creation and image upload posts."),
                [AdminSiteControlKeys.NewComments] = new("New comments", "Block new comment creation."),
                [AdminSiteControlKeys.NewRegistrations] = new("New registrations", "Block new account registration.")
            };

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminSiteControlService(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IReadOnlyList<AdminSiteControlDto>> GetControlsAsync()
        {
            var rows = await _db.AdminSiteControls
                .AsNoTracking()
                .Where(c => AdminSiteControlKeys.All.Contains(c.Key))
                .ToDictionaryAsync(c => c.Key, StringComparer.Ordinal);
            var now = DateTime.UtcNow;

            return AdminSiteControlKeys.All
                .Select(key => Map(key, rows.TryGetValue(key, out var row) ? row : null, now))
                .ToList();
        }

        public async Task<AdminSiteControlDto> GetControlAsync(string key)
        {
            EnsureKnownKey(key);
            var row = await _db.AdminSiteControls.AsNoTracking().FirstOrDefaultAsync(c => c.Key == key);
            return Map(key, row, DateTime.UtcNow);
        }

        public async Task<AdminSiteControlDto> UpdateControlAsync(string key, UpdateAdminSiteControlRequestDto request, string actorUserId)
        {
            EnsureKnownKey(key);
            ArgumentNullException.ThrowIfNull(request);

            var reason = Normalize(request.Reason);
            var message = Normalize(request.Message);
            if (!request.Enabled && string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainValidationException("Site control update is invalid.", new Dictionary<string, string[]>
                {
                    ["reason"] = new[] { "Reason is required when disabling a control." }
                });
            }

            var now = DateTime.UtcNow;
            var row = await _db.AdminSiteControls.FirstOrDefaultAsync(c => c.Key == key);
            if (row is null)
            {
                row = new AdminSiteControl { Key = key };
                _db.AdminSiteControls.Add(row);
            }

            row.Enabled = request.Enabled;
            row.DisabledUntilUtc = request.Enabled ? null : request.DisabledUntilUtc?.ToUniversalTime();
            row.Message = message;
            row.Reason = reason;
            row.UpdatedUtc = now;
            row.UpdatedByUserId = actorUserId;

            await AddAuditAsync(actorUserId, key, row);
            await _db.SaveChangesAsync();

            return Map(key, row, now);
        }

        public async Task<bool> IsEnabledAsync(string key)
        {
            EnsureKnownKey(key);
            var row = await _db.AdminSiteControls.AsNoTracking().FirstOrDefaultAsync(c => c.Key == key);
            return Map(key, row, DateTime.UtcNow).EffectiveEnabled;
        }

        private async Task AddAuditAsync(string actorUserId, string key, AdminSiteControl row)
        {
            var actor = await _userManager.FindByIdAsync(actorUserId) ?? throw new UnauthorizedAccessException("Actor not found.");
            var roles = await _userManager.GetRolesAsync(actor);
            _db.ModerationAuditLogs.Add(new ModerationAuditLog
            {
                CreatedUtc = DateTime.UtcNow,
                ActorUserId = actorUserId,
                ActorRole = ResolveAuditActorRole(roles),
                ActionType = "SiteControlUpdate",
                TargetType = "SiteControl",
                TargetId = key,
                Reason = row.Reason,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    row.Enabled,
                    row.DisabledUntilUtc,
                    row.Message
                })
            });
        }

        private static AdminSiteControlDto Map(string key, AdminSiteControl? row, DateTime now)
        {
            var definition = Definitions[key];
            var enabled = row?.Enabled ?? true;
            var expired = !enabled && row?.DisabledUntilUtc.HasValue == true && row.DisabledUntilUtc.Value <= now;
            return new AdminSiteControlDto
            {
                Key = key,
                Label = definition.Label,
                Description = definition.Description,
                Enabled = enabled,
                EffectiveEnabled = enabled || expired,
                IsExpired = expired,
                DisabledUntilUtc = row?.DisabledUntilUtc,
                Message = row?.Message,
                Reason = row?.Reason,
                UpdatedUtc = row?.UpdatedUtc,
                UpdatedByUserId = row?.UpdatedByUserId
            };
        }

        private static void EnsureKnownKey(string key)
        {
            if (!AdminSiteControlKeys.IsKnown(key))
            {
                throw new DomainValidationException("Unsupported site control.", new Dictionary<string, string[]>
                {
                    ["key"] = new[] { "Unsupported site control key." }
                });
            }
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string ResolveAuditActorRole(IList<string> roles)
        {
            foreach (var prioritizedRole in ActorRolePriority)
            {
                var match = roles.FirstOrDefault(role =>
                    string.Equals(role, prioritizedRole, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? "Unknown";
        }

        private sealed record ControlDefinition(string Label, string Description);
    }
}
