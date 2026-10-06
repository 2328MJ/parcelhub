using ParcelHub.Api.Services;

namespace ParcelHub.Tests;

public class RequestValidatorTests
{
    private readonly RequestValidator _validator = new();

    [Fact]
    public void ValidOutbound_HasNoErrors()
    {
        Assert.Empty(_validator.Validate(TestData.Outbound()));
    }

    [Fact]
    public void ReturnWithoutConsignee_IsValid()
    {
        Assert.Empty(_validator.Validate(TestData.Return()));
    }

    [Fact]
    public void MissingBody_IsRejected()
    {
        var errors = _validator.Validate(null);

        Assert.Equal("V000", Assert.Single(errors).Code);
    }

    [Fact]
    public void UnknownServiceCode_IsRejected()
    {
        var request = TestData.Outbound();
        request.ServiceCode = "PH-NOPE";

        Assert.Contains(_validator.Validate(request), e => e.Code == "V001");
    }

    [Fact]
    public void ReportsEveryProblem_NotJustTheFirst()
    {
        var request = TestData.Outbound();
        request.Sender!.Address!.PostCode = null;
        request.Parcel!.WeightKg = 0;
        request.LabelFormat = "PDF";

        var codes = _validator.Validate(request).Select(e => e.Code).ToList();

        Assert.Contains("V003", codes);
        Assert.Contains("V004", codes);
        Assert.Contains("V007", codes);
    }

    [Fact]
    public void Return_NeedsSenderPhone()
    {
        var request = TestData.Return();
        request.Sender!.Phone = " ";

        Assert.Contains(_validator.Validate(request), e => e.Code == "V005" && e.Field == "Sender.Phone");
    }

    [Fact]
    public void NorthernIrelandReturn_WithoutItems_IsRejected()
    {
        var request = TestData.NorthernIrelandReturn();
        request.Parcel!.Items.Clear();

        Assert.Contains(_validator.Validate(request), e => e.Code == "V006");
    }

    [Fact]
    public void NorthernIrelandService_FromGreatBritain_IsRejected()
    {
        var request = TestData.Return();
        request.ServiceCode = ServiceCatalogue.NorthernIrelandReturn;

        Assert.Contains(_validator.Validate(request), e => e.Code == "V008");
    }

    [Theory]
    [InlineData(30.0, true)]
    [InlineData(30.1, false)]
    [InlineData(-1.0, false)]
    public void Weight_MustBeWithinLimit(double weightKg, bool valid)
    {
        var request = TestData.Outbound();
        request.Parcel!.WeightKg = (decimal)weightKg;

        Assert.Equal(valid, _validator.Validate(request).Count == 0);
    }
}
