using System;
using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs
{
    public class UserRestrictionDto
    {
        public string UserId { get; set; } = default!;
        public bool IsSuspended { get; set; }
        public bool CanPost { get; set; }
        public bool CanComment { get; set; }
        public bool CanPostImages { get; set; }
        public string? Reason { get; set; }
        public DateTime? Until { get; set; }
    }

    public class SetUserRestrictionDto
    {
        public bool CanPost { get; set; } = true;
        public bool CanComment { get; set; } = true;
        public bool CanPostImages { get; set; } = true;
        public string? Reason { get; set; }
        public DateTime? Until { get; set; }
    }

    public class SetSuspensionDto
    {
        public string? Reason { get; set; }
        public DateTime? Until { get; set; }
    }

    public class AppSettingDto
    {
        public string Key { get; set; } = default!;
        public string Value { get; set; } = default!;
    }

    public class SetAppSettingDto
    {
        public string Key { get; set; } = default!;
        public string Value { get; set; } = default!;
    }

    public class FeedPolicyDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public double WRecency { get; set; }
        public double WLikes { get; set; }
        public double WComments { get; set; }
        public double WReportsPenalty { get; set; }
        public double HalfLifeHours { get; set; }
        public bool DiversityByAuthor { get; set; }
        public int MaxPerAuthorPerPage { get; set; }
        public bool ExcludeNSFW { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpsertFeedPolicyDto
    {
        public string Name { get; set; } = default!;
        public double WRecency { get; set; }
        public double WLikes { get; set; }
        public double WComments { get; set; }
        public double WReportsPenalty { get; set; }
        public double HalfLifeHours { get; set; }
        public bool DiversityByAuthor { get; set; }
        public int MaxPerAuthorPerPage { get; set; }
        public bool ExcludeNSFW { get; set; }
    }

    public class NewsItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string BodyMarkdown { get; set; } = default!;
        public DateTime PublishAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsPinned { get; set; }
        public int PinPriority { get; set; }
        public string Status { get; set; } = default!;
    }

    public class UpsertNewsItemDto
    {
        public string Title { get; set; } = default!;
        public string BodyMarkdown { get; set; } = default!;
        public DateTime PublishAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsPinned { get; set; }
        public int PinPriority { get; set; }
    }

    public class ModerationActionDto
    {
        public long Id { get; set; }
        public string ActorUserId { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string TargetType { get; set; } = default!;
        public string TargetId { get; set; } = default!;
        public string? Reason { get; set; }
        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class FeedItemDto
    {
        public string Type { get; set; } = default!;
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string? Content { get; set; }
        public int PinPriority { get; set; }
        public double Score { get; set; }
    }
}
