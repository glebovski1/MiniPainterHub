namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminDashboardHealthDto
    {
        public string Key { get; set; } = default!;
        public string Label { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string Detail { get; set; } = default!;
    }
}
