using Microsoft.Extensions.Options;
using ParcelHub.Api.Models;
using ParcelHub.Api.Services;
using ParcelHub.Api.Settings;

namespace ParcelHub.Tests;

/// <summary>Ready-made requests and settings so each test only states what's different about it.</summary>
public static class TestData
{
    public static ParcelHubSettings Settings(Action<FakeCarrierSettings>? configureCarrier = null)
    {
        var settings = new ParcelHubSettings
        {
            NoReplyEmail = "noreply@parcelhub.example",
            ReturnCentre = new Party
            {
                Name = "ParcelHub Returns Centre",
                Address = new Address { Line1 = "Unit 7, Harbour Park", City = "Southampton", PostCode = "SO14 3XY", CountryCode = "GB" }
            },
            FakeCarrier = new FakeCarrierSettings
            {
                FailureRate = 0,
                LatencyMs = 0,
                PollDelayMs = 0,
                PollsBeforeTracking = 2,
                MaxPollAttempts = 5,
                MaxWeightKg = 25
            },
            ErrorMappings = new Dictionary<string, ErrorMapping>
            {
                ["V001"] = new() { Code = "20001", Message = "Unknown service" },
                ["ADDR_UNKNOWN"] = new() { Code = "30001", Message = "Address not recognised by carrier" }
            }
        };
        configureCarrier?.Invoke(settings.FakeCarrier);
        return settings;
    }

    public static IOptions<ParcelHubSettings> Options(Action<FakeCarrierSettings>? configureCarrier = null) =>
        Microsoft.Extensions.Options.Options.Create(Settings(configureCarrier));

    public static LabelRequest Outbound() => new()
    {
        TransactionKey = "TEST-OUT-1",
        ServiceCode = ServiceCatalogue.Standard,
        Sender = new Party
        {
            Name = "Kettle & Co",
            Email = "dispatch@kettle.example",
            Address = new Address { Line1 = "1 Mill Lane", City = "Leeds", PostCode = "LS1 4AB", CountryCode = "GB" }
        },
        Consignee = new Party
        {
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Phone = "07911 123456",
            Address = new Address { Line1 = "12 Analytical Row", City = "London", PostCode = "N1 9GU", CountryCode = "GB" }
        },
        Parcel = new Parcel { Barcode = "PARCEL-1", WeightKg = 2.5m },
        LabelFormat = "SVG"
    };

    /// <summary>A return with no consignee at all - the return centre is filled in for us.</summary>
    public static LabelRequest Return() => new()
    {
        TransactionKey = "TEST-RTN-1",
        ServiceCode = ServiceCatalogue.Return,
        Sender = new Party
        {
            Name = "Joe Bloggs",
            Phone = "07700 900123",
            Address = new Address { Line1 = "4 Elm Street", City = "Bristol", PostCode = "BS1 5TR", CountryCode = "GB" }
        },
        Parcel = new Parcel { Barcode = "RTN-1", WeightKg = 1.2m },
        LabelFormat = "SVG"
    };

    public static LabelRequest NorthernIrelandReturn()
    {
        var request = Return();
        request.ServiceCode = ServiceCatalogue.NorthernIrelandReturn;
        request.Sender!.Address = new Address { Line1 = "9 Lagan Walk", City = "Belfast", PostCode = "BT1 5GS", CountryCode = "GB" };
        request.Parcel!.Items.Add(new ParcelItem { Description = "Cotton shirt", Quantity = 1, Value = 25m, HsCode = "6205" });
        return request;
    }
}
