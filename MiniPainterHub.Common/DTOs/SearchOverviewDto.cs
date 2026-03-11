using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class SearchOverviewDto
    {
        public List<PostSummaryDto> Posts { get; set; } = new();
        public List<UserListItemDto> Users { get; set; } = new();
        public List<SearchTagResultDto> Tags { get; set; } = new();
    }
}
