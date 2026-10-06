using ParcelHub.Api.Models;

namespace ParcelHub.Api.Services;

public record ValidationError(string Code, string Field, string Message);

/// <summary>
/// Step 1: reject requests that can never succeed.
/// Unlike a "first error wins" validator, this collects every problem so the caller can fix them all at once.
/// </summary>
public class RequestValidator
{
    public const decimal MaxWeightKg = 30;

    public List<ValidationError> Validate(LabelRequest? request)
    {
        var errors = new List<ValidationError>();
        if (request == null)
        {
            errors.Add(new("V000", "Request", "Request body is missing"));
            return errors;
        }

        var service = ServiceCatalogue.Find(request.ServiceCode);
        if (service == null)
        {
            errors.Add(new("V001", "ServiceCode", $"Unknown service code '{request.ServiceCode}'"));
        }

        if (!LabelRenderer.IsSupported(request.LabelFormat))
        {
            errors.Add(new("V007", "LabelFormat",
                $"Label format '{request.LabelFormat}' is not supported. Use one of: {string.Join(", ", LabelRenderer.SupportedFormats)}"));
        }

        ValidateParty(request.Sender, "Sender", errors);

        if (service?.Flow == FlowType.Return)
        {
            // The consignee on a return is always the return centre, so the client doesn't have to send one.
            // The sender's phone is needed because the carrier texts them a drop-off code.
            if (string.IsNullOrWhiteSpace(request.Sender?.Phone))
            {
                errors.Add(new("V005", "Sender.Phone", "Returns need the sender's phone number for the drop-off code"));
            }

            var fromNorthernIreland = RequestEnhancer.DetectRegion(request.Sender?.Address) == "GB-NIR";
            if (fromNorthernIreland && request.Parcel?.Items.Count == 0)
            {
                errors.Add(new("V006", "Parcel.Items", "Returns from Northern Ireland need at least one item for customs"));
            }
            if (service.Code == ServiceCatalogue.NorthernIrelandReturn && !fromNorthernIreland)
            {
                errors.Add(new("V008", "ServiceCode", $"{service.Code} is only for parcels sent from Northern Ireland (BT postcodes)"));
            }
        }
        else
        {
            ValidateParty(request.Consignee, "Consignee", errors);
            if (request.Consignee != null
                && string.IsNullOrWhiteSpace(request.Consignee.Email)
                && string.IsNullOrWhiteSpace(request.Consignee.Phone))
            {
                errors.Add(new("V005", "Consignee", "Consignee needs an email or phone number for delivery notifications"));
            }
        }

        if (request.Parcel == null)
        {
            errors.Add(new("V004", "Parcel", "Parcel is missing"));
        }
        else if (request.Parcel.WeightKg <= 0 || request.Parcel.WeightKg > MaxWeightKg)
        {
            errors.Add(new("V004", "Parcel.WeightKg", $"Weight must be more than 0 and at most {MaxWeightKg} kg"));
        }

        return errors;
    }

    private static void ValidateParty(Party? party, string field, List<ValidationError> errors)
    {
        if (party == null)
        {
            errors.Add(new("V002", field, $"{field} is missing"));
            return;
        }
        if (string.IsNullOrWhiteSpace(party.Name))
        {
            errors.Add(new("V002", $"{field}.Name", $"{field} name is missing"));
        }
        if (party.Address == null)
        {
            errors.Add(new("V003", $"{field}.Address", $"{field} address is missing"));
            return;
        }
        if (string.IsNullOrWhiteSpace(party.Address.Line1))
        {
            errors.Add(new("V003", $"{field}.Address.Line1", $"{field} address line 1 is missing"));
        }
        if (string.IsNullOrWhiteSpace(party.Address.PostCode))
        {
            errors.Add(new("V003", $"{field}.Address.PostCode", $"{field} postcode is missing"));
        }
        if (string.IsNullOrWhiteSpace(party.Address.CountryCode))
        {
            errors.Add(new("V003", $"{field}.Address.CountryCode", $"{field} country code is missing"));
        }
    }
}
