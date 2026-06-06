namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminDashboardMetricDto
    {
        public string Key { get; set; } = default!;
        public string Label { get; set; } = default!;
        public string Value { get; set; } = default!;
        public string? Unit { get; set; }
        public string Status { get; set; } = "Normal";
    }
}
