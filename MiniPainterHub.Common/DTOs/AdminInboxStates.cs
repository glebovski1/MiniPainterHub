using System;

namespace MiniPainterHub.Common.DTOs
{
    public static class AdminInboxStates
    {
        public const string All = "";
        public const string Active = "Active";
        public const string Hidden = "Hidden";
        public const string Reported = "Reported";
        public const string Reviewed = "Reviewed";

        public static readonly string[] AllStates = { Active, Hidden, Reported, Reviewed };

        public static bool IsKnownState(string? value) =>
            string.Equals(value, All, StringComparison.Ordinal)
            || string.Equals(value, Active, StringComparison.Ordinal)
            || string.Equals(value, Hidden, StringComparison.Ordinal)
            || string.Equals(value, Reported, StringComparison.Ordinal)
            || string.Equals(value, Reviewed, StringComparison.Ordinal);
    }
}
