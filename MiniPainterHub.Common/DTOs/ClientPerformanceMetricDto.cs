using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class ClientPerformanceMetricDto
{
    [Required]
    [StringLength(80)]
    public string Name { get; set; } = default!;

    public double Value { get; set; }

    [Required]
    [StringLength(16)]
    public string Unit { get; set; } = default!;

    [StringLength(256)]
    public string? Path { get; set; }

    public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
