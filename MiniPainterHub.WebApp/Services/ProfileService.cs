using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public sealed class ProfileService : IProfileService
    {
        private readonly ApiClient _api;

        public bool UseCache { get; set; } = true;
        public UserProfileDto? Mine { get; private set; }

        public event Action<UserProfileDto?>? MineChanged;

        public ProfileService(ApiClient api) => _api = api;

        public async Task<UserProfileDto?> GetMineAsync()
        {
            if (UseCache && Mine is not null)
            {
                return Mine;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/profiles/me");
            var result = await _api.SendForResultAsync<UserProfileDto>(request, new ApiRequestOptions
            {
                SuppressedStatusCodes = new HashSet<HttpStatusCode> { HttpStatusCode.NotFound }
            });

            if (!result.Success)
            {
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    Mine = null;
                    MineChanged?.Invoke(Mine);
                    return null;
                }

                return Mine;
            }

            if (result.Value is null)
            {
                throw new InvalidOperationException("API returned no data for the current user profile.");
            }

            return SetAndReturnMine(result.Value);
        }

        public async Task<UserProfileDto?> CreateMineAsync(CreateUserProfileDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/profiles/me")
            {
                Content = JsonContent.Create(dto)
            };

            var result = await _api.SendAsync<UserProfileDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when creating the profile.");
            }

            return SetAndReturnMine(result);
        }

        public async Task<UserProfileDto?> UpdateMineAsync(UpdateUserProfileDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, "api/profiles/me")
            {
                Content = JsonContent.Create(dto)
            };

            var result = await _api.SendAsync<UserProfileDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when updating the profile.");
            }

            return SetAndReturnMine(result);
        }

        public async Task<UserProfileDto?> UploadAvatarAsync(IBrowserFile file, long maxSizeBytes = 5_000_000)
        {
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(maxAllowedSize: maxSizeBytes);
            var part = new StreamContent(stream);
            part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(part, "file", file.Name);

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/profiles/me/avatar")
            {
                Content = content
            };

            var result = await _api.SendAsync<UserProfileDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when uploading the avatar.");
            }

            return SetAndReturnMine(result);
        }

        public async Task<UserProfileDto> RemoveAvatarAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, "api/profiles/me/avatar");
            var result = await _api.SendAsync<UserProfileDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when removing the avatar.");
            }

            return SetAndReturnMine(result)!;
        }

        public async Task<UserProfileDto> GetUserProfileById(string id)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/profiles/{id}");
            var result = await _api.SendAsync<UserProfileDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException($"API returned no data when fetching the profile for user '{id}'.");
            }

            return result;
        }

        public void ClearCache()
        {
            Mine = null;
            MineChanged?.Invoke(Mine);
        }

        private UserProfileDto? SetAndReturnMine(UserProfileDto userProfileDto)
        {
            Mine = userProfileDto;
            MineChanged?.Invoke(Mine);
            return Mine;
        }
    }
}
