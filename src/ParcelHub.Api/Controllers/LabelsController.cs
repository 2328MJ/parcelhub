using Microsoft.AspNetCore.Mvc;
using ParcelHub.Api.Models;
using ParcelHub.Api.Services;

namespace ParcelHub.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class LabelsController : ControllerBase
{
    private readonly LabelService _labelService;
    private readonly LabelStore _labelStore;

    public LabelsController(LabelService labelService, LabelStore labelStore)
    {
        _labelService = labelService;
        _labelStore = labelStore;
    }

    /// <summary>
    /// Create a shipment and label. Like most carrier gateways, this always answers 200 and puts the
    /// outcome in Status, so check Status rather than the HTTP code.
    /// </summary>
    [HttpPost("labels")]
    public async Task<ActionResult<LabelResponse>> CreateLabel([FromBody] LabelRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _labelService.CreateLabelAsync(request, cancellationToken));
    }

    /// <summary>Open a label created since the app started, e.g. in a browser for SVG.</summary>
    [HttpGet("labels/{trackingNumber}")]
    public IActionResult GetLabel(string trackingNumber)
    {
        return _labelStore.TryGet(trackingNumber, out var label)
            ? File(label.Content, label.ContentType)
            : NotFound();
    }

    [HttpGet("labels/formats")]
    public IEnumerable<string> GetSupportedFormats() => LabelRenderer.SupportedFormats;

    [HttpGet("services")]
    public IEnumerable<CarrierService> GetServices() => ServiceCatalogue.All;
}
