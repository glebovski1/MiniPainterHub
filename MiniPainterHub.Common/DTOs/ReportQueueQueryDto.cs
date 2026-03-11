namespace MiniPainterHub.Common.DTOs
{
    public sealed class ReportQueueQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Status { get; set; }
        public string? TargetType { get; set; }
        public string? ReasonCode { get; set; }
    }
}
