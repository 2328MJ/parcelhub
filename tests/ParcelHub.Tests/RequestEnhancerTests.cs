using ParcelHub.Api.Models;
using ParcelHub.Api.Services;

namespace ParcelHub.Tests;

public class RequestEnhancerTests
{
    private readonly RequestEnhancer _enhancer = new(TestData.Options());

    [Fact]
    public void Return_ConsigneeBecomesReturnCentre_WithNoReplyEmail()
    {
        var request = TestData.Return();

        var notes = _enhancer.Enhance(request);

        Assert.Equal("ParcelHub Returns Centre", request.Consignee!.Name);
        Assert.Equal("noreply@parcelhub.example", request.Consignee.Email);
        Assert.Contains(notes, n => n.Contains("noreply@parcelhub.example"));
    }

    [Fact]
    public void Return_ChangingOneRequest_DoesNotChangeTheSharedReturnCentre()
    {
        var first = TestData.Return();
        _enhancer.Enhance(first);
        first.Consignee!.Address!.City = "Changed";

        var second = TestData.Return();
        _enhancer.Enhance(second);

        Assert.Equal("Southampton", second.Consignee!.Address!.City);
    }

    [Fact]
    public void Outbound_ConsigneeIsLeftAlone()
    {
        var request = TestData.Outbound();

        _enhancer.Enhance(request);

        Assert.Equal("Ada Lovelace", request.Consignee!.Name);
        Assert.Equal("ada@example.com", request.Consignee.Email);
    }

    [Theory]
    [InlineData("07911 123456", "GB", "+447911123456")]
    [InlineData("447911123456", "GB", "+447911123456")]
    [InlineData("0044 7911 123456", "GB", "+447911123456")]
    [InlineData("087 123 4567", "IE", "+353871234567")]
    [InlineData("512 345 678", "PL", "+48512345678")]
    [InlineData("0612345678", "FR", "0612345678")]
    [InlineData("", "GB", "")]
    public void ToE164_ConvertsLocalNumbers(string phone, string country, string expected)
    {
        Assert.Equal(expected, RequestEnhancer.ToE164(phone, country));
    }

    [Theory]
    [InlineData("BT1 5GS", "GB", "GB-NIR")]
    [InlineData("bt7 1nn", "GB", "GB-NIR")]
    [InlineData("SO14 3XY", "GB", "GB-GBN")]
    [InlineData("D02 X285", "IE", null)]
    public void DetectRegion_FindsNorthernIreland(string postCode, string country, string? expected)
    {
        Assert.Equal(expected, RequestEnhancer.DetectRegion(new Address { PostCode = postCode, CountryCode = country }));
    }

    [Fact]
    public void PolishPostcode_GetsItsDash()
    {
        var request = TestData.Outbound();
        request.Consignee!.Address = new Address { Line1 = "ul. Długa 5", City = "Gdańsk", PostCode = "80827", CountryCode = "PL" };

        _enhancer.Enhance(request);

        Assert.Equal("80-827", request.Consignee.Address.PostCode);
    }

    [Fact]
    public void MissingCity_FallsBackToAddressLine2()
    {
        var request = TestData.Outbound();
        request.Consignee!.Address!.City = null;
        request.Consignee.Address.Line2 = "Islington";

        _enhancer.Enhance(request);

        Assert.Equal("Islington", request.Consignee.Address.City);
        Assert.Null(request.Consignee.Address.Line2);
    }
}
