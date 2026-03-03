using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class FeatureFlagsService : IFeatureFlagsService
    {
        private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "SiteOnline",
            "RegistrationEnabled",
            "LoginEnabled",
            "PostingEnabled",
            "ImageUploadEnabled",
            "RetentionDays"
        };

        private readonly AppDbContext _db;

        public FeatureFlagsService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<bool> GetFlagAsync(string key, bool defaultValue)
        {
            var setting = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
            return setting is null ? defaultValue : bool.TryParse(setting.Value, out var v) && v;
        }

        public async Task<Dictionary<string, string>> GetFlagsAsync()
        {
            return await _db.AppSettings.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.Value);
        }

        public async Task SetFlagAsync(string key, string value, string actorUserId)
        {
            if (!AllowedKeys.Contains(key))
            {
                throw new DomainValidationException("Unknown feature flag key.");
            }

            var setting = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
            if (setting is null)
            {
                _db.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow, UpdatedByUserId = actorUserId });
            }
            else
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedByUserId = actorUserId;
            }

            await _db.SaveChangesAsync();
        }
    }
}
