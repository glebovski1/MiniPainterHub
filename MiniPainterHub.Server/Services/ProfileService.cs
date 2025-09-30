using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _dbContext;
    public ProfileService(AppDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<UserProfileDto> ProjectProfiles() =>
        _dbContext.Profiles
                  .AsNoTracking()
                  .Select(p => new UserProfileDto
                  {
                      UserId = p.UserId,
                      DisplayName = p.DisplayName,
                      Bio = p.Bio,
                      AvatarUrl = p.AvatarUrl,
                      UserName = p.User.UserName!,
                      Email = p.User.Email!,
                      DateJoined = p.User.DateJoined
                  });

    public async Task<UserProfileDto> CreateAsync(string userId, CreateUserProfileDto dto)
    {
        Validate(dto.DisplayName, dto.Bio);

        if (await _dbContext.Profiles.AnyAsync(p => p.UserId == userId))
            throw new ConflictException("Profile already exists for this user.");

        var profile = new Profile
        {
            UserId = userId,
            DisplayName = dto.DisplayName.Trim(),
            Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim(),
            AvatarUrl = null // always null on create
        };

        _dbContext.Profiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        return await ProjectProfiles()
            .FirstAsync(p => p.UserId == profile.UserId);
    }

    public async Task<UserProfileDto?> GetByUserIdAsync(string userId)
    {
        return await ProjectProfiles()
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<UserProfileDto> UpdateAsync(string userId, UpdateUserProfileDto dto)
    {
        Validate(dto.DisplayName, dto.Bio);

        var profile = await _dbContext.Profiles.FirstOrDefaultAsync(p => p.UserId == userId)
                      ?? throw new NotFoundException("Profile not found.");

        profile.DisplayName = dto.DisplayName.Trim();
        profile.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();

        await _dbContext.SaveChangesAsync();

        return await ProjectProfiles()
            .FirstAsync(p => p.UserId == profile.UserId);
    }

    // Used ONLY by the upload endpoint to persist the (fixed) avatar URL.
    public async Task<UserProfileDto> SetAvatarUrlAsync(string userId, string? avatarUrl)
    {
        var profile = await _dbContext.Profiles.FirstOrDefaultAsync(p => p.UserId == userId)
                      ?? throw new NotFoundException("Profile not found.");

        profile.AvatarUrl = avatarUrl; // controller ensures same URL each time
        await _dbContext.SaveChangesAsync();

        return await ProjectProfiles()
            .FirstAsync(p => p.UserId == profile.UserId);
    }

    public async Task<UserProfileDto> GetUserProfileById(string userId)
    {
        return await ProjectProfiles()
            .FirstOrDefaultAsync(p => p.UserId == userId)
            ?? throw new NotFoundException("Profile not found.");
    }

    private static void Validate(string displayName, string? bio)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var dn = (displayName ?? string.Empty).Trim();
        if (dn.Length < 2 || dn.Length > 80)
        {
            errors[nameof(displayName)] = new[] { "Display name must be between 2 and 80 characters." };
        }

        if (!string.IsNullOrWhiteSpace(bio) && bio.Length > 500)
        {
            errors[nameof(bio)] = new[] { "Bio must be 500 characters or fewer." };
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException("Profile data is invalid.", errors);
        }
    }

  
}