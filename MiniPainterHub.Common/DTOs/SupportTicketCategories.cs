using System;
using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs;

public static class SupportTicketCategories
{
    public const string Bug = "Bug";
    public const string Account = "Account";
    public const string Safety = "Safety";
    public const string Suggestion = "Suggestion";
    public const string Other = "Other";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Bug,
        Account,
        Safety,
        Suggestion,
        Other
    });
}
