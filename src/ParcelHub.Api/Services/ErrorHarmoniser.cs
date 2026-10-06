using Microsoft.Extensions.Options;
using ParcelHub.Api.Models;
using ParcelHub.Api.Settings;

namespace ParcelHub.Api.Services;

/// <summary>
/// Turns our validation codes and the carrier's error codes into one standard set of errors,
/// using the ErrorMappings section of appsettings.json.
/// An unmapped code still becomes "Unknown Carrier Error", but the original code and reason are
/// always kept in Detail, so the real cause is never lost.
/// </summary>
public class ErrorHarmoniser
{
    public const string UnknownCode = "10000";
    public const string UnknownMessage = "Unknown Carrier Error";

    private readonly Dictionary<string, ErrorMapping> _mappings;

    public ErrorHarmoniser(IOptions<ParcelHubSettings> settings)
    {
        _mappings = new Dictionary<string, ErrorMapping>(settings.Value.ErrorMappings, StringComparer.OrdinalIgnoreCase);
    }

    public HarmonisedError Harmonise(string sourceCode, string detail)
    {
        return _mappings.TryGetValue(sourceCode, out var mapping)
            ? new HarmonisedError(mapping.Code, mapping.Message, detail)
            : new HarmonisedError(UnknownCode, UnknownMessage, $"[{sourceCode}] {detail}");
    }
}
