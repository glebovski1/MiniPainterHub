namespace MiniPainterHub.Common.DTOs
{
    public static class ReportStatuses
    {
        public const string Open = "Open";
        public const string Reviewed = "Reviewed";
        public const string Actioned = "Actioned";
        public const string Dismissed = "Dismissed";

        public static readonly string[] Resolvable = { Reviewed, Actioned, Dismissed };
    }
}
