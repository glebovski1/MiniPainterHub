using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public static class HobbyProjectKinds
{
    public const string Miniature = "Miniature";
    public const string Unit = "Unit";
    public const string Army = "Army";
    public const string Warband = "Warband";
    public const string Terrain = "Terrain";
    public const string Diorama = "Diorama";
    public const string Other = "Other";

    public static readonly string[] All =
    {
        Miniature, Unit, Army, Warband, Terrain, Diorama, Other
    };
}

public static class HobbyProjectStatuses
{
    public const string Planning = "Planning";
    public const string InProgress = "InProgress";
    public const string OnHold = "OnHold";
    public const string Completed = "Completed";

    public static readonly string[] All = { Planning, InProgress, OnHold, Completed };
}

public static class HobbyProjectSorts
{
    public const string Recent = "recent";
    public const string Oldest = "oldest";
    public const string Title = "title";

    public static readonly string[] All = { Recent, Oldest, Title };
}

public static class HobbyProjectRules
{
    public const int MaxTitleLength = 140;
    public const int MaxDescriptionLength = 4000;
    public const int MaxGameSystemLength = 120;
    public const int MaxFactionThemeLength = 120;
    public const int MaxGoalLength = 240;
    public const int MaxMilestoneLabelLength = 140;
    public const int MaxSearchLength = 120;
    public const int MaxProjectsPerOwner = 50;
    public const int MaxEntriesPerProject = 250;
    public const int MaxShowcaseEntries = 24;
}

public sealed class HobbyProjectReferenceDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = HobbyProjectStatuses.Planning;
    public bool IsPublic { get; set; }
}

public class HobbyProjectSummaryDto
{
    public int Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerUserName { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string? OwnerAvatarUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = HobbyProjectKinds.Other;
    public string? GameSystem { get; set; }
    public string? FactionTheme { get; set; }
    public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public string Status { get; set; } = HobbyProjectStatuses.Planning;
    public int? SelectedCoverPostId { get; set; }
    public int? CoverPostId { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? CoverThumbnailUrl { get; set; }
    public int EntryCount { get; set; }
    public int ShowcaseCount { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    public bool IsArchived { get; set; }
    public bool IsHidden { get; set; }
    public bool IsPublic { get; set; }
    public bool HasCurationWarning { get; set; }
}

public sealed class HobbyProjectDto : HobbyProjectSummaryDto
{
    public DateTime? ModeratedUtc { get; set; }
    public string? ModerationReason { get; set; }
}

public sealed class HobbyProjectEntryDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int PostId { get; set; }
    public DateTime LinkedUtc { get; set; }
    public string? MilestoneLabel { get; set; }
    public int? ShowcaseOrder { get; set; }
    public PostSummaryDto Post { get; set; } = new();
}

public sealed class HobbyProjectQueryDto
{
    public string? Search { get; set; }
    public string? OwnerUserId { get; set; }
    public string? Kind { get; set; }
    public string? Status { get; set; }
    public string Sort { get; set; } = HobbyProjectSorts.Recent;
    public bool IncludeArchived { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public sealed class CreateHobbyProjectDto
{
    [Required, StringLength(HobbyProjectRules.MaxTitleLength)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(HobbyProjectRules.MaxDescriptionLength)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Kind { get; set; } = HobbyProjectKinds.Other;

    [StringLength(HobbyProjectRules.MaxGameSystemLength)]
    public string? GameSystem { get; set; }

    [StringLength(HobbyProjectRules.MaxFactionThemeLength)]
    public string? FactionTheme { get; set; }

    [StringLength(HobbyProjectRules.MaxGoalLength)]
    public string? Goal { get; set; }

    public DateOnly? StartDate { get; set; }
}

public sealed class UpdateHobbyProjectDto
{
    [Required, StringLength(HobbyProjectRules.MaxTitleLength)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(HobbyProjectRules.MaxDescriptionLength)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Kind { get; set; } = HobbyProjectKinds.Other;

    [StringLength(HobbyProjectRules.MaxGameSystemLength)]
    public string? GameSystem { get; set; }

    [StringLength(HobbyProjectRules.MaxFactionThemeLength)]
    public string? FactionTheme { get; set; }

    [StringLength(HobbyProjectRules.MaxGoalLength)]
    public string? Goal { get; set; }

    public DateOnly? StartDate { get; set; }
}

public sealed class UpdateHobbyProjectStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public sealed class LinkHobbyProjectPostDto
{
    [Range(1, int.MaxValue)]
    public int PostId { get; set; }

    public int? SourceProjectId { get; set; }

    [StringLength(HobbyProjectRules.MaxMilestoneLabelLength)]
    public string? MilestoneLabel { get; set; }
}

public sealed class UpdateHobbyProjectEntryDto
{
    [StringLength(HobbyProjectRules.MaxMilestoneLabelLength)]
    public string? MilestoneLabel { get; set; }
}

public sealed class UpdateHobbyProjectShowcaseDto
{
    [MaxLength(HobbyProjectRules.MaxShowcaseEntries)]
    public List<int> PostIds { get; set; } = new();
}

public sealed class UpdateHobbyProjectCoverDto
{
    public int? PostId { get; set; }
}
