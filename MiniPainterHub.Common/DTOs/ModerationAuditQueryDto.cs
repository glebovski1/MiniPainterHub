namespace MiniPainterHub.Common.DTOs
{
    public class ModerationAuditQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? TargetType { get; set; }
        public string? ActorUserId { get; set; }
        public string? ActionType { get; set; }
    }
}
