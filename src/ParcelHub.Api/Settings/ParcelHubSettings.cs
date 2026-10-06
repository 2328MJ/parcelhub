using ParcelHub.Api.Models;

namespace ParcelHub.Api.Settings;

/// <summary>
/// Bound from the "ParcelHub" section of appsettings.json.
/// Treat it as read-only at runtime: it is shared by every request.
/// </summary>
public class ParcelHubSettings
{
    public const string SectionName = "ParcelHub";

    /// <summary>Used when a return centre has no email address of its own.</summary>
    public string NoReplyEmail { get; set; } = "noreply@parcelhub.example";

    /// <summary>Where every return is sent back to.</summary>
    public Party ReturnCentre { get; set; } = new();

    public FakeCarrierSettings FakeCarrier { get; set; } = new();

    /// <summary>Source error code (V001, ADDR_UNKNOWN, ...) to standard error.</summary>
    public Dictionary<string, ErrorMapping> ErrorMappings { get; set; } = new();
}

public class FakeCarrierSettings
{
    /// <summary>Chance (0.0 - 1.0) that the carrier is "down" for a request.</summary>
    public double FailureRate { get; set; }

    /// <summary>Simulated network delay per carrier call.</summary>
    public int LatencyMs { get; set; } = 50;

    /// <summary>The carrier's own weight limit, stricter than our validation.</summary>
    public decimal MaxWeightKg { get; set; } = 25;

    /// <summary>How many polls return "not ready yet" before the tracking number appears.</summary>
    public int PollsBeforeTracking { get; set; } = 2;

    public int MaxPollAttempts { get; set; } = 10;
    public int PollDelayMs { get; set; } = 100;
}

public class ErrorMapping
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
}
