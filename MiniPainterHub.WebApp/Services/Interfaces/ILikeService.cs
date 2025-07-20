using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
   
        public interface ILikeService
        {
            /// <summary>
            /// Fetches the total like count & whether the current user has liked.
            /// </summary>
            Task<LikeDto> GetLikesAsync(int postId);

            /// <summary>
            /// Toggles the like state (POST) and returns the updated LikeDto.
            /// </summary>
            Task<LikeDto> ToggleLikeAsync(int postId);
        }
    
}
