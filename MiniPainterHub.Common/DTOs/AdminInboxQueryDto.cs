namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminInboxQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int WindowHours { get; set; } = 24;
        public string? ItemType { get; set; } = AdminInboxItemTypes.All;
        public string? State { get; set; } = AdminInboxStates.All;
        public string? Search { get; set; }
    }
}
