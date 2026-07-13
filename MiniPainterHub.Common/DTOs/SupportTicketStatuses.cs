using System;
using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs;

public static class SupportTicketStatuses
{
    public const string New = "New";
    public const string WaitingForAdmin = "WaitingForAdmin";
    public const string WaitingForUser = "WaitingForUser";
    public const string Resolved = "Resolved";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        New,
        WaitingForAdmin,
        WaitingForUser,
        Resolved
    });
}
