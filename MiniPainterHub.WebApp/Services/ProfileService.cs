using Microsoft.AspNetCore.Components.Forms;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace MiniPainterHub.WebApp.Services
{
    public sealed class ProfileService : IProfileService
    {
        private readonly HttpClient _http;

        public bool UseCache { get; set; } = true;
        public UserProfileDto? Mine { get; private set; }

        public event Action<UserProfileDto?>? MineChanged;
        public ProfileService(HttpClient http) => _http = http;

        public async Task<UserProfileDto?> GetMineAsync()
        {
            if (UseCache && Mine is not null) return Mine;

            var resp = await _http.GetAsync("api/profiles/me");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Mine = null;
                MineChanged?.Invoke(Mine);
                return null;
            }
            resp.EnsureSuccessStatusCode();


            var userProfile = await resp.Content.ReadFromJsonAsync<UserProfileDto>();

            return SetAndReturnMine(userProfile!);
        }

        public async Task<UserProfileDto?> CreateMineAsync(CreateUserProfileDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/profiles/me", dto);

            resp.EnsureSuccessStatusCode();

            var userProfile = await resp.Content.ReadFromJsonAsync<UserProfileDto>();

            return SetAndReturnMine(userProfile!);

        }

        public async Task<UserProfileDto?> UpdateMineAsync(UpdateUserProfileDto dto)
        {
            var resp = await _http.PutAsJsonAsync("api/profiles/me", dto);

            resp.EnsureSuccessStatusCode();

            var userProfile = await resp.Content.ReadFromJsonAsync<UserProfileDto>();

            return SetAndReturnMine(userProfile!);
        }

        public async Task<UserProfileDto?> UploadAvatarAsync(IBrowserFile file, long maxSizeBytes = 5_000_000)
        {
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(maxAllowedSize: maxSizeBytes);
            var part = new StreamContent(stream);
            part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(part, "file", file.Name); // must be "file" to match controller

            var resp = await _http.PostAsync("api/profiles/me/avatar", content);

            var userProfile = await resp.Content.ReadFromJsonAsync<UserProfileDto>();

            return SetAndReturnMine(userProfile!);
        }

        public async Task<UserProfileDto> RemoveAvatarAsync()
        {
            var resp = await _http.DeleteAsync("api/profiles/me/avatar");
            resp.EnsureSuccessStatusCode();
            var userProfile = await resp.Content.ReadFromJsonAsync<UserProfileDto>()!;

            return SetAndReturnMine(userProfile!)!;
        }

        public async Task<UserProfileDto> GetUserProfileById(string id)
        {
            var resp = _http.GetAsync($"api/profiles/{id}");
            resp.Result.EnsureSuccessStatusCode();

            var userProfile = await resp.Result.Content.ReadFromJsonAsync<UserProfileDto>()!;

            return SetAndReturnMine(userProfile);

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
            return Mine!;
        }

        
    }
}
