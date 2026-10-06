using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ParcelHub.Api.Models;
using ParcelHub.Api.Services;
using ParcelHub.Api.Settings;

namespace ParcelHub.Tests;

public class LabelServiceTests
{
    private static LabelService CreateService(Action<FakeCarrierSettings>? configureCarrier = null, LabelStore? store = null)
    {
        var options = TestData.Options(configureCarrier);
        return new LabelService(
            new RequestValidator(),
            new RequestEnhancer(options),
            new FakeCarrierClient(options),
            new ErrorHarmoniser(options),
            store ?? new LabelStore(),
            options,
            NullLogger<LabelService>.Instance);
    }

    [Fact]
    public async Task Outbound_ReturnsTrackingNumberAndSvgLabel()
    {
        var store = new LabelStore();

        var response = await CreateService(store: store).CreateLabelAsync(TestData.Outbound());

        Assert.Equal(ResponseStatus.OK, response.Status);
        Assert.Matches(@"^PH\d{9}GB$", response.TrackingNumber);
        Assert.Equal("SVG", response.Label!.Format);
        var svg = Encoding.UTF8.GetString(Convert.FromBase64String(response.Label.Contents));
        Assert.Contains("Ada Lovelace", svg);
        Assert.True(store.TryGet(response.TrackingNumber!, out _));
    }

    [Fact]
    public async Task Return_LabelIsAddressedToTheReturnCentre()
    {
        var response = await CreateService().CreateLabelAsync(TestData.Return());

        Assert.Equal(ResponseStatus.OK, response.Status);
        var svg = Encoding.UTF8.GetString(Convert.FromBase64String(response.Label!.Contents));
        Assert.Contains("ParcelHub Returns Centre", svg);
        Assert.Contains(response.Enhancements, n => n.Contains("noreply"));
    }

    [Fact]
    public async Task NorthernIrelandReturn_AsZpl()
    {
        var request = TestData.NorthernIrelandReturn();
        request.LabelFormat = "zpl";

        var response = await CreateService().CreateLabelAsync(request);

        Assert.Equal(ResponseStatus.OK, response.Status);
        var zpl = Encoding.UTF8.GetString(Convert.FromBase64String(response.Label!.Contents));
        Assert.StartsWith("^XA", zpl);
        Assert.Contains(response.TrackingNumber!, zpl);
    }

    [Fact]
    public async Task ValidationFailure_NeverCallsTheCarrier()
    {
        var request = TestData.Outbound();
        request.ServiceCode = "PH-NOPE";

        var response = await CreateService().CreateLabelAsync(request);

        Assert.Equal(ResponseStatus.VALIDATION_ERROR, response.Status);
        Assert.Equal("20001", Assert.Single(response.Errors).Code);
        Assert.DoesNotContain(response.Diagnostics, d => d.Step.StartsWith("Carrier"));
    }

    [Fact]
    public async Task UnknownPostcode_IsACarrierError()
    {
        var request = TestData.Outbound();
        request.Consignee!.Address!.PostCode = "ZZ99 9ZZ";

        var response = await CreateService().CreateLabelAsync(request);

        Assert.Equal(ResponseStatus.CARRIER_ERROR, response.Status);
        Assert.Equal("30001", Assert.Single(response.Errors).Code);
    }

    [Fact]
    public async Task OverCarrierWeightLimit_PassesValidation_ButCarrierRejects()
    {
        var request = TestData.Outbound();
        request.Parcel!.WeightKg = 26;

        var response = await CreateService().CreateLabelAsync(request);

        Assert.Equal(ResponseStatus.CARRIER_ERROR, response.Status);
        Assert.Contains("[OVERWEIGHT]", Assert.Single(response.Errors).Detail);
    }

    [Fact]
    public async Task TrackingNumberNeverArrives_TimesOut()
    {
        var service = CreateService(carrier => carrier.PollsBeforeTracking = 100);

        var response = await service.CreateLabelAsync(TestData.Outbound());

        Assert.Equal(ResponseStatus.CARRIER_ERROR, response.Status);
        Assert.Contains("[TRACKING_TIMEOUT]", Assert.Single(response.Errors).Detail);
    }

    [Fact]
    public async Task CarrierDown_IsReportedAsServiceUnavailable()
    {
        var service = CreateService(carrier => carrier.FailureRate = 1.0);

        var response = await service.CreateLabelAsync(TestData.Outbound());

        Assert.Equal(ResponseStatus.CARRIER_ERROR, response.Status);
        Assert.Contains("[SERVICE_UNAVAILABLE]", Assert.Single(response.Errors).Detail);
    }
}
