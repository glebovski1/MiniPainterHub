using System;
using System.Security.Cryptography;
using System.Text;

namespace MiniPainterHub.Server.Features.Conversations;

internal static class DirectConversationKey
{
    public const int Length = 64;

    public static string Create(string userId, string otherUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(otherUserId);

        var first = string.CompareOrdinal(userId, otherUserId) <= 0 ? userId : otherUserId;
        var second = ReferenceEquals(first, userId) ? otherUserId : userId;
        var canonical = $"{first}\u001f{second}";
        return Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(canonical))).ToLowerInvariant();
    }
}
