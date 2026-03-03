using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/feed")]
    public class FeedController : ControllerBase
    {
        private readonly IFeedService _feed;

        public FeedController(IFeedService feed)
        {
            _feed = feed;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<FeedItemDto>>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
            => Ok(await _feed.GetFeedAsync(page, pageSize));
    }
}
