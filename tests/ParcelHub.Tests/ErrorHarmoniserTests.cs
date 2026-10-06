using ParcelHub.Api.Services;

namespace ParcelHub.Tests;

public class ErrorHarmoniserTests
{
    private readonly ErrorHarmoniser _harmoniser = new(TestData.Options());

    [Fact]
    public void MappedCode_UsesTheMapping_AndKeepsTheDetail()
    {
        var error = _harmoniser.Harmonise("ADDR_UNKNOWN", "Postcode ZZ99 9ZZ is not recognised");

        Assert.Equal("30001", error.Code);
        Assert.Equal("Postcode ZZ99 9ZZ is not recognised", error.Detail);
    }

    [Fact]
    public void UnmappedCode_IsUnknownCarrierError_ButTheRealReasonSurvives()
    {
        var error = _harmoniser.Harmonise("V005", "Consignee email address cannot be null");

        Assert.Equal(ErrorHarmoniser.UnknownCode, error.Code);
        Assert.Equal(ErrorHarmoniser.UnknownMessage, error.Message);
        Assert.Equal("[V005] Consignee email address cannot be null", error.Detail);
    }

    [Fact]
    public void Lookup_IgnoresCase()
    {
        Assert.Equal("20001", _harmoniser.Harmonise("v001", "x").Code);
    }
}
