using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ParcelHub.Api.Models;
using ParcelHub.Api.Settings;

namespace ParcelHub.Api.Services;

/// <summary>An error reported by the carrier, with the carrier's own error code.</summary>
public class CarrierException : Exception
{
    public string Code { get; }

    public CarrierException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public interface ICarrierClient
{
    /// <summary>Creates the shipment and returns the carrier's shipment id.</summary>
    Task<string> CreateShipmentAsync(LabelRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the tracking number, or null if the carrier hasn't assigned one yet.</summary>
    Task<string?> GetTrackingNumberAsync(string shipmentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pretends to be a carrier API. It behaves like a real one in the ways that matter:
/// - tracking numbers arrive asynchronously, so you have to poll for them
/// - it has its own rules (weight limit, unknown postcodes) that our validation doesn't know about
/// - it can be "down" (set FakeCarrier:FailureRate in appsettings)
/// Try consignee postcode "ZZ99 9ZZ" or a 26 kg parcel to see carrier errors.
/// </summary>
public class FakeCarrierClient : ICarrierClient
{
    public const string UnknownPostCode = "ZZ999ZZ";

    private readonly ConcurrentDictionary<string, int> _pollCounts = new();
    private readonly FakeCarrierSettings _settings;

    public FakeCarrierClient(IOptions<ParcelHubSettings> settings)
    {
        _settings = settings.Value.FakeCarrier;
    }

    public async Task<string> CreateShipmentAsync(LabelRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_settings.LatencyMs, cancellationToken);

        if (Random.Shared.NextDouble() < _settings.FailureRate)
        {
            throw new CarrierException("SERVICE_UNAVAILABLE", "Carrier API is temporarily unavailable");
        }

        var postCode = request.Consignee?.Address?.PostCode?.Replace(" ", "").ToUpperInvariant();
        if (postCode == UnknownPostCode)
        {
            throw new CarrierException("ADDR_UNKNOWN", $"Postcode {request.Consignee!.Address!.PostCode} is not recognised by the carrier");
        }

        if (request.Parcel!.WeightKg > _settings.MaxWeightKg)
        {
            throw new CarrierException("OVERWEIGHT", $"Carrier accepts parcels up to {_settings.MaxWeightKg} kg, this one is {request.Parcel.WeightKg} kg");
        }

        var shipmentId = Guid.NewGuid().ToString("N");
        _pollCounts[shipmentId] = 0;
        return shipmentId;
    }

    public async Task<string?> GetTrackingNumberAsync(string shipmentId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_settings.LatencyMs, cancellationToken);

        if (!_pollCounts.ContainsKey(shipmentId))
        {
            throw new CarrierException("SHIPMENT_NOT_FOUND", $"Shipment {shipmentId} does not exist");
        }

        var polls = _pollCounts.AddOrUpdate(shipmentId, 1, (_, count) => count + 1);
        if (polls <= _settings.PollsBeforeTracking)
        {
            return null;
        }

        _pollCounts.TryRemove(shipmentId, out _);
        return $"PH{Random.Shared.Next(100_000_000, 999_999_999)}GB";
    }
}
