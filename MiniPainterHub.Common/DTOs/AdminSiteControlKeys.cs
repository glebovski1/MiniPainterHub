using System;

namespace MiniPainterHub.Common.DTOs
{
    public static class AdminSiteControlKeys
    {
        public const string PublicSite = "public-site";
        public const string NewPosts = "new-posts";
        public const string NewComments = "new-comments";
        public const string NewRegistrations = "new-registrations";

        public static readonly string[] All = { PublicSite, NewPosts, NewComments, NewRegistrations };

        public static bool IsKnown(string? key) =>
            string.Equals(key, PublicSite, StringComparison.Ordinal)
            || string.Equals(key, NewPosts, StringComparison.Ordinal)
            || string.Equals(key, NewComments, StringComparison.Ordinal)
            || string.Equals(key, NewRegistrations, StringComparison.Ordinal);
    }
}
