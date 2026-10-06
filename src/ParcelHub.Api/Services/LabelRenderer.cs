using System.Security;
using System.Text;
using ParcelHub.Api.Models;

namespace ParcelHub.Api.Services;

public record RenderedLabel(string Format, string ContentType, byte[] Content);

/// <summary>
/// Step 4: draws the label. SVG opens in any browser; ZPL is what thermal label printers use
/// (paste it into https://labelary.com/viewer.html to see it).
/// </summary>
public static class LabelRenderer
{
    public static readonly string[] SupportedFormats = { "SVG", "ZPL" };

    public static bool IsSupported(string? format) =>
        format != null && SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);

    public static RenderedLabel Render(string format, LabelRequest request, CarrierService service, string trackingNumber)
    {
        return format.ToUpperInvariant() switch
        {
            "SVG" => new RenderedLabel("SVG", "image/svg+xml", Encoding.UTF8.GetBytes(RenderSvg(request, service, trackingNumber))),
            "ZPL" => new RenderedLabel("ZPL", "text/plain", Encoding.UTF8.GetBytes(RenderZpl(request, service, trackingNumber))),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported label format")
        };
    }

    private static string RenderSvg(LabelRequest request, CarrierService service, string trackingNumber)
    {
        var sender = request.Sender!;
        var consignee = request.Consignee!;
        var isReturn = service.Flow == FlowType.Return;
        var svg = new StringBuilder();

        svg.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="400" height="400" viewBox="0 0 400 400" font-family="Arial, Helvetica, sans-serif">""");
        svg.AppendLine("""<rect x="1" y="1" width="398" height="398" fill="white" stroke="black" stroke-width="2"/>""");
        svg.AppendLine("""<rect x="1" y="1" width="398" height="50" fill="black"/>""");
        svg.AppendLine("""<text x="16" y="34" font-size="22" font-weight="bold" fill="white">ParcelHub</text>""");
        svg.AppendLine($"""<text x="384" y="32" font-size="15" fill="white" text-anchor="end">{Xml(service.Name)}</text>""");

        svg.AppendLine("""<text x="16" y="74" font-size="11" fill="#555">FROM</text>""");
        svg.AppendLine($"""<text x="16" y="92" font-size="14">{Xml(sender.Name)}</text>""");
        svg.AppendLine($"""<text x="16" y="110" font-size="12">{Xml(FormatAddress(sender.Address))}</text>""");

        svg.AppendLine("""<line x1="16" y1="126" x2="384" y2="126" stroke="black"/>""");
        svg.AppendLine("""<text x="16" y="148" font-size="11" fill="#555">TO</text>""");
        svg.AppendLine($"""<text x="16" y="172" font-size="20" font-weight="bold">{Xml(consignee.Name)}</text>""");
        svg.AppendLine($"""<text x="16" y="196" font-size="14">{Xml(consignee.Address?.Line1)}</text>""");
        svg.AppendLine($"""<text x="16" y="216" font-size="14">{Xml(consignee.Address?.Line2)}</text>""");
        svg.AppendLine($"""<text x="16" y="242" font-size="18" font-weight="bold">{Xml(consignee.Address?.City)} {Xml(consignee.Address?.PostCode)}</text>""");

        if (isReturn)
        {
            svg.AppendLine("""<rect x="330" y="140" width="54" height="54" fill="black"/>""");
            svg.AppendLine("""<text x="357" y="180" font-size="36" font-weight="bold" fill="white" text-anchor="middle">R</text>""");
        }
        if (sender.Address?.Region == "GB-NIR")
        {
            svg.AppendLine("""<text x="384" y="216" font-size="11" text-anchor="end">NI · CUSTOMS</text>""");
        }

        svg.Append(RenderBarcode(trackingNumber, y: 262, height: 80));
        svg.AppendLine($"""<text x="200" y="366" font-size="16" font-family="Courier New, monospace" text-anchor="middle" letter-spacing="2">{Xml(trackingNumber)}</text>""");
        svg.AppendLine($"""<text x="200" y="388" font-size="10" fill="#555" text-anchor="middle">{request.Parcel!.WeightKg} kg · {Xml(request.TransactionKey)}</text>""");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    /// <summary>A barcode-looking pattern derived from the tracking number. Not a scannable symbology.</summary>
    private static string RenderBarcode(string value, int y, int height)
    {
        var bars = new List<(bool Filled, int Width)>();
        foreach (var ch in value)
        {
            for (var i = 0; i < 4; i++)
            {
                bars.Add((i % 2 == 0, 1 + ((ch >> i) & 3)));
            }
        }

        var x = (400 - bars.Sum(b => b.Width + 1)) / 2;
        var svg = new StringBuilder();
        foreach (var (filled, width) in bars)
        {
            if (filled)
            {
                svg.AppendLine($"""<rect x="{x}" y="{y}" width="{width}" height="{height}"/>""");
            }
            x += width + 1;
        }
        return svg.ToString();
    }

    private static string RenderZpl(LabelRequest request, CarrierService service, string trackingNumber)
    {
        var sender = request.Sender!;
        var consignee = request.Consignee!;
        return string.Join("\n",
            "^XA",
            "^CI28",
            "^FO30,30^A0N,45,45^FDParcelHub^FS",
            $"^FO30,85^A0N,25,25^FD{Zpl(service.Name)}^FS",
            $"^FO30,140^A0N,22,22^FDFROM: {Zpl(sender.Name)}^FS",
            $"^FO30,168^A0N,22,22^FD{Zpl(FormatAddress(sender.Address))}^FS",
            $"^FO30,230^A0N,32,32^FDTO: {Zpl(consignee.Name)}^FS",
            $"^FO30,272^A0N,26,26^FD{Zpl(consignee.Address?.Line1)}^FS",
            $"^FO30,304^A0N,26,26^FD{Zpl(consignee.Address?.City)} {Zpl(consignee.Address?.PostCode)}^FS",
            $"^FO30,380^BY3^BCN,140,Y,N,N^FD{Zpl(trackingNumber)}^FS",
            "^XZ");
    }

    private static string FormatAddress(Address? address) =>
        address == null
            ? ""
            : string.Join(", ", new[] { address.Line1, address.City, address.PostCode, address.CountryCode }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Xml(string? value) => SecurityElement.Escape(value ?? "");

    // ^ and ~ are ZPL command characters, so they can't appear in field data.
    private static string Zpl(string? value) => (value ?? "").Replace("^", "").Replace("~", "");
}
