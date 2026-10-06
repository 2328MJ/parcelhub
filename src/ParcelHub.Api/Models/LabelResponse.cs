namespace ParcelHub.Api.Models;

public enum ResponseStatus
{
    OK,
    VALIDATION_ERROR,
    CARRIER_ERROR,
    UNEXPECTED_ERROR
}

public class LabelResponse
{
    public ResponseStatus Status { get; set; }
    public string? Message { get; set; }
    public string? TrackingNumber { get; set; }
    public Label? Label { get; set; }

    /// <summary>Standardised errors. Each one keeps the original reason in Detail.</summary>
    public List<HarmonisedError> Errors { get; set; } = new();

    /// <summary>Every change the enhancer made to the request, so nothing happens silently.</summary>
    public List<string> Enhancements { get; set; } = new();

    public List<DiagnosticEntry> Diagnostics { get; set; } = new();
}

public record Label(string Format, string Encoding, string Contents);

public record HarmonisedError(string Code, string Message, string? Detail);

public record DiagnosticEntry(string Step, long ElapsedMs);
