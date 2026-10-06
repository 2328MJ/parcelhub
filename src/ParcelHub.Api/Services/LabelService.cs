using System.Diagnostics;
using Microsoft.Extensions.Options;
using ParcelHub.Api.Models;
using ParcelHub.Api.Settings;

namespace ParcelHub.Api.Services;

/// <summary>
/// The whole journey of one label request:
///   1. validate  →  2. enhance  →  3. carrier (create + poll tracking)  →  4. render label
/// Any failure becomes a standard error response instead of an exception.
/// </summary>
public class LabelService
{
    private readonly RequestValidator _validator;
    private readonly RequestEnhancer _enhancer;
    private readonly ICarrierClient _carrier;
    private readonly ErrorHarmoniser _harmoniser;
    private readonly LabelStore _store;
    private readonly FakeCarrierSettings _carrierSettings;
    private readonly ILogger<LabelService> _logger;

    public LabelService(
        RequestValidator validator,
        RequestEnhancer enhancer,
        ICarrierClient carrier,
        ErrorHarmoniser harmoniser,
        LabelStore store,
        IOptions<ParcelHubSettings> settings,
        ILogger<LabelService> logger)
    {
        _validator = validator;
        _enhancer = enhancer;
        _carrier = carrier;
        _harmoniser = harmoniser;
        _store = store;
        _carrierSettings = settings.Value.FakeCarrier;
        _logger = logger;
    }

    public async Task<LabelResponse> CreateLabelAsync(LabelRequest? request, CancellationToken cancellationToken = default)
    {
        var response = new LabelResponse();
        var total = Stopwatch.StartNew();

        try
        {
            // 1. Validate
            var errors = Timed(response, "Validate", () => _validator.Validate(request));
            if (errors.Count > 0)
            {
                response.Status = ResponseStatus.VALIDATION_ERROR;
                response.Message = errors[0].Message;
                response.Errors = errors.Select(e => _harmoniser.Harmonise(e.Code, $"{e.Field}: {e.Message}")).ToList();
                return response;
            }

            // 2. Enhance
            response.Enhancements = Timed(response, "Enhance", () => _enhancer.Enhance(request!));
            var service = ServiceCatalogue.Find(request!.ServiceCode)!;

            // 3. Carrier
            var step = Stopwatch.StartNew();
            var shipmentId = await _carrier.CreateShipmentAsync(request, cancellationToken);
            response.Diagnostics.Add(new DiagnosticEntry("Carrier: create shipment", step.ElapsedMilliseconds));

            step.Restart();
            var trackingNumber = await PollForTrackingNumberAsync(shipmentId, cancellationToken);
            response.TrackingNumber = trackingNumber;
            response.Diagnostics.Add(new DiagnosticEntry("Carrier: get tracking number", step.ElapsedMilliseconds));

            // 4. Render
            var label = Timed(response, "Render label", () => LabelRenderer.Render(request.LabelFormat, request, service, trackingNumber));
            _store.Save(trackingNumber, label);
            response.Label = new Label(label.Format, "BASE64", Convert.ToBase64String(label.Content));

            response.Status = ResponseStatus.OK;
            response.Message = $"{service.Name} label created";
        }
        catch (CarrierException ex)
        {
            _logger.LogWarning("Carrier rejected {TransactionKey}: [{Code}] {Message}", request?.TransactionKey, ex.Code, ex.Message);
            response.Status = ResponseStatus.CARRIER_ERROR;
            response.Message = ex.Message;
            response.Errors = new List<HarmonisedError> { _harmoniser.Harmonise(ex.Code, ex.Message) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error for {TransactionKey}", request?.TransactionKey);
            response.Status = ResponseStatus.UNEXPECTED_ERROR;
            response.Message = ex.Message;
            response.Errors = new List<HarmonisedError> { _harmoniser.Harmonise("UNEXPECTED", ex.Message) };
        }
        finally
        {
            response.Diagnostics.Add(new DiagnosticEntry("Total", total.ElapsedMilliseconds));
            _logger.LogInformation("Label request {TransactionKey} finished: {Status} {Message} in {ElapsedMs} ms",
                request?.TransactionKey, response.Status, response.Message, total.ElapsedMilliseconds);
        }

        return response;
    }

    /// <summary>
    /// The carrier assigns tracking numbers asynchronously, so ask until one appears.
    /// Carrier errors are deliberately not caught here: a real error (bad credentials, unknown shipment)
    /// should surface straight away, not be retried until it looks like a timeout.
    /// </summary>
    private async Task<string> PollForTrackingNumberAsync(string shipmentId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _carrierSettings.MaxPollAttempts; attempt++)
        {
            var trackingNumber = await _carrier.GetTrackingNumberAsync(shipmentId, cancellationToken);
            if (!string.IsNullOrEmpty(trackingNumber))
            {
                _logger.LogInformation("Tracking number {TrackingNumber} received after {Attempts} attempt(s)", trackingNumber, attempt);
                return trackingNumber;
            }
            await Task.Delay(_carrierSettings.PollDelayMs, cancellationToken);
        }

        throw new CarrierException("TRACKING_TIMEOUT", $"No tracking number after {_carrierSettings.MaxPollAttempts} attempts");
    }

    private static T Timed<T>(LabelResponse response, string step, Func<T> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = action();
        response.Diagnostics.Add(new DiagnosticEntry(step, stopwatch.ElapsedMilliseconds));
        return result;
    }
}
