using System.Collections.Concurrent;

namespace ParcelHub.Api.Services;

/// <summary>
/// Keeps the most recent labels in memory so you can open them at /api/v1/labels/{trackingNumber}.
/// Everything is lost on restart - fine for a sandbox, not for production.
/// </summary>
public class LabelStore
{
    private const int MaxLabels = 500;

    private readonly ConcurrentDictionary<string, RenderedLabel> _labels = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _order = new();

    public void Save(string trackingNumber, RenderedLabel label)
    {
        _labels[trackingNumber] = label;
        _order.Enqueue(trackingNumber);

        while (_labels.Count > MaxLabels && _order.TryDequeue(out var oldest))
        {
            _labels.TryRemove(oldest, out _);
        }
    }

    public bool TryGet(string trackingNumber, out RenderedLabel label) =>
        _labels.TryGetValue(trackingNumber, out label!);
}
