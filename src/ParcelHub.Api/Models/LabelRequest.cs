namespace ParcelHub.Api.Models;

/// <summary>
/// What a client (think "GRP") sends to ask for a shipping label.
/// </summary>
public class LabelRequest
{
    public string? TransactionKey { get; set; }

    /// <summary>One of the codes in <see cref="Services.ServiceCatalogue"/>, e.g. PH-STD or PH-RTN.</summary>
    public string? ServiceCode { get; set; }

    /// <summary>Who the parcel comes from. On a return this is the customer sending it back.</summary>
    public Party? Sender { get; set; }

    /// <summary>Who the parcel goes to. On a return this is replaced with the return centre.</summary>
    public Party? Consignee { get; set; }

    public Parcel? Parcel { get; set; }

    /// <summary>SVG (viewable in a browser) or ZPL (thermal printers).</summary>
    public string LabelFormat { get; set; } = "SVG";
}

public class Party
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Address? Address { get; set; }
}

public class Address
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? PostCode { get; set; }
    public string? CountryCode { get; set; }

    /// <summary>Filled in by the enhancer, e.g. GB-NIR for Northern Ireland.</summary>
    public string? Region { get; set; }
}

public class Parcel
{
    public string? Barcode { get; set; }
    public decimal WeightKg { get; set; }

    /// <summary>Contents of the parcel. Needed for customs on Northern Ireland returns.</summary>
    public List<ParcelItem> Items { get; set; } = new();
}

public class ParcelItem
{
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public decimal Value { get; set; }
    public string? HsCode { get; set; }
}
