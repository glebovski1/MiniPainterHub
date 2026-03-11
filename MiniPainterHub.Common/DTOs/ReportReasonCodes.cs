namespace MiniPainterHub.Common.DTOs
{
    public static class ReportReasonCodes
    {
        public const string Spam = "Spam";
        public const string Harassment = "Harassment";
        public const string Inappropriate = "Inappropriate";
        public const string Copyright = "Copyright";
        public const string Other = "Other";

        public static readonly string[] All = { Spam, Harassment, Inappropriate, Copyright, Other };
    }
}
