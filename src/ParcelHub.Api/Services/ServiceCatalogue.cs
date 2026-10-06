namespace ParcelHub.Api.Services;

public enum FlowType
{
    Outbound,
    Return
}

public record CarrierService(string Code, string Name, FlowType Flow);

/// <summary>
/// The single place that knows which service codes exist and which are returns.
/// Everything else asks this class instead of keeping its own copy of the codes.
/// </summary>
public static class ServiceCatalogue
{
    public const string Standard = "PH-STD";
    public const string Return = "PH-RTN";
    public const string NorthernIrelandReturn = "PH-RTN-NI";

    private static readonly Dictionary<string, CarrierService> Services = new(StringComparer.OrdinalIgnoreCase)
    {
        [Standard] = new(Standard, "Standard Delivery", FlowType.Outbound),
        [Return] = new(Return, "Standard Return", FlowType.Return),
        [NorthernIrelandReturn] = new(NorthernIrelandReturn, "Northern Ireland Return", FlowType.Return),
    };

    public static IEnumerable<CarrierService> All => Services.Values;

    public static CarrierService? Find(string? code) =>
        code != null && Services.TryGetValue(code, out var service) ? service : null;

    public static bool IsReturn(string? code) => Find(code)?.Flow == FlowType.Return;
}
