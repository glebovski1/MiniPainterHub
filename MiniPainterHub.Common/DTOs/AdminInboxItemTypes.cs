using System;

namespace MiniPainterHub.Common.DTOs
{
    public static class AdminInboxItemTypes
    {
        public const string All = "";
        public const string Post = ReportTargetTypes.Post;
        public const string Comment = ReportTargetTypes.Comment;

        public static readonly string[] ContentTypes = { Post, Comment };

        public static bool IsContentType(string? value) =>
            string.Equals(value, Post, StringComparison.Ordinal)
            || string.Equals(value, Comment, StringComparison.Ordinal);
    }
}
