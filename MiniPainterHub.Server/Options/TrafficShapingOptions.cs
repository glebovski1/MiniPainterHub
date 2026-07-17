using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Server.Options;

public sealed class TrafficShapingOptions
{
    public RateLimitPolicyOptions Auth { get; set; } = new()
    {
        PermitLimit = 10,
        WindowSeconds = 60
    };

    public RateLimitPolicyOptions EmailConfirmation { get; set; } = new()
    {
        PermitLimit = 5,
        WindowSeconds = 3_600
    };

    public RateLimitPolicyOptions Search { get; set; } = new()
    {
        PermitLimit = 60,
        WindowSeconds = 60
    };

    public RateLimitPolicyOptions Write { get; set; } = new()
    {
        PermitLimit = 30,
        WindowSeconds = 60
    };

    public RateLimitPolicyOptions Upload { get; set; } = new()
    {
        PermitLimit = 6,
        WindowSeconds = 60
    };

    public RateLimitPolicyOptions Realtime { get; set; } = new()
    {
        PermitLimit = 30,
        WindowSeconds = 60
    };

    [Range(1, 100)]
    public int GlobalUploadConcurrency { get; set; } = 8;

    [Range(1, 20)]
    public int PerClientUploadConcurrency { get; set; } = 2;
}

public sealed class RateLimitPolicyOptions
{
    [Range(1, 100_000)]
    public int PermitLimit { get; set; }

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; }
}
