using Microsoft.Extensions.Options;
using ParcelHub.Api.Models;
using ParcelHub.Api.Settings;

namespace ParcelHub.Api.Services;

/// <summary>
/// Step 2: tidy up a valid request so the carrier will accept it.
/// Every change is recorded and returned to the caller.
/// </summary>
public class RequestEnhancer
{
    private static readonly Dictionary<string, string> DiallingCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GB"] = "44",
        ["IE"] = "353",
        ["PL"] = "48"
    };

    private readonly ParcelHubSettings _settings;

    public RequestEnhancer(IOptions<ParcelHubSettings> settings)
    {
        _settings = settings.Value;
    }

    public List<string> Enhance(LabelRequest request)
    {
        var notes = new List<string>();

        if (ServiceCatalogue.IsReturn(request.ServiceCode))
        {
            request.Consignee = CreateReturnCentre();
            notes.Add($"Consignee set to the return centre ({request.Consignee.Name})");

            // The client doesn't know the return centre's email, so fall back to a no-reply address.
            if (string.IsNullOrWhiteSpace(request.Consignee.Email))
            {
                request.Consignee.Email = _settings.NoReplyEmail;
                notes.Add($"Consignee email missing, defaulted to {_settings.NoReplyEmail}");
            }
        }

        EnhanceParty(request.Sender, "Sender", notes);
        EnhanceParty(request.Consignee, "Consignee", notes);

        return notes;
    }

    /// <summary>
    /// Builds a fresh copy for each request. Handing out the settings object itself would let one
    /// request change the return centre for every other request.
    /// </summary>
    private Party CreateReturnCentre()
    {
        var centre = _settings.ReturnCentre;
        return new Party
        {
            Name = centre.Name,
            Email = centre.Email,
            Phone = centre.Phone,
            Address = new Address
            {
                Line1 = centre.Address?.Line1,
                Line2 = centre.Address?.Line2,
                City = centre.Address?.City,
                PostCode = centre.Address?.PostCode,
                CountryCode = centre.Address?.CountryCode
            }
        };
    }

    private static void EnhanceParty(Party? party, string role, List<string> notes)
    {
        if (party?.Address == null)
        {
            return;
        }
        var address = party.Address;

        if (string.IsNullOrWhiteSpace(address.City) && !string.IsNullOrWhiteSpace(address.Line2))
        {
            address.City = address.Line2;
            address.Line2 = null;
            notes.Add($"{role} city missing, used address line 2 ({address.City})");
        }

        // Polish postcodes are NN-NNN; clients often send them without the dash.
        if (string.Equals(address.CountryCode, "PL", StringComparison.OrdinalIgnoreCase)
            && address.PostCode is { Length: 5 } postCode
            && postCode.All(char.IsDigit))
        {
            address.PostCode = $"{postCode[..2]}-{postCode[2..]}";
            notes.Add($"{role} Polish postcode formatted as {address.PostCode}");
        }

        address.Region = DetectRegion(address);
        if (address.Region == "GB-NIR")
        {
            notes.Add($"{role} is in Northern Ireland (GB-NIR)");
        }

        var phone = ToE164(party.Phone, address.CountryCode);
        if (phone != party.Phone)
        {
            notes.Add($"{role} phone converted to international format {phone}");
            party.Phone = phone;
        }
    }

    /// <summary>GB-NIR for Northern Ireland (BT postcodes), GB-GBN for the rest of Great Britain.</summary>
    public static string? DetectRegion(Address? address)
    {
        if (address == null || !string.Equals(address.CountryCode, "GB", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var postCode = (address.PostCode ?? "").Replace(" ", "");
        return postCode.StartsWith("BT", StringComparison.OrdinalIgnoreCase) ? "GB-NIR" : "GB-GBN";
    }

    /// <summary>
    /// Converts a local phone number to E.164, e.g. "07911 123456" in GB becomes "+447911123456".
    /// Numbers for countries it doesn't know are returned unchanged.
    /// </summary>
    public static string? ToE164(string? phone, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return phone;
        }
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return phone;
        }
        if (phone.TrimStart().StartsWith('+'))
        {
            return "+" + digits;
        }
        if (digits.StartsWith("00"))
        {
            return "+" + digits[2..];
        }
        if (countryCode == null || !DiallingCodes.TryGetValue(countryCode, out var diallingCode))
        {
            return phone;
        }
        if (digits.StartsWith('0'))
        {
            return "+" + diallingCode + digits[1..];
        }
        return digits.StartsWith(diallingCode) ? "+" + digits : "+" + diallingCode + digits;
    }
}
