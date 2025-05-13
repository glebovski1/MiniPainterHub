using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Collections.Generic;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class ProfileService : IProfileService
    {
        private readonly AppDbContext _dbContext;

        public ProfileService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ProfileDto> CreateAsync(string userId, CreateProfileDto dto)
        {
            // Prevent duplicate profiles
            var exists = await _dbContext.Profiles
                .AnyAsync(p => p.UserId == userId);
            if (exists)
                throw new InvalidOperationException("Profile already exists for this user.");

            var profile = new Profile
            {
                UserId = userId,
                DisplayName = dto.DisplayName,
                Bio = dto.Bio
            };

            _dbContext.Profiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            // Manual mapping to DTO
            return new ProfileDto
            {
                UserId = profile.UserId,
                DisplayName = profile.DisplayName,
                Bio = profile.Bio
            };
        }

        public async Task<ProfileDto> GetByUserIdAsync(string userId)
        {
            var profile = await _dbContext.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
                return null;

            return new ProfileDto
            {
                UserId = profile.UserId,
                DisplayName = profile.DisplayName,
                Bio = profile.Bio
            };
        }

        public async Task<ProfileDto> UpdateAsync(string userId, UpdateProfileDto dto)
        {
            var profile = await _dbContext.Profiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
                throw new KeyNotFoundException("Profile not found.");

            profile.DisplayName = dto.DisplayName;
            profile.Bio = dto.Bio;

            await _dbContext.SaveChangesAsync();

            return new ProfileDto
            {
                UserId = profile.UserId,
                DisplayName = profile.DisplayName,
                Bio = profile.Bio
            };
        }
    }
}
