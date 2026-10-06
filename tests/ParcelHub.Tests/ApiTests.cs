using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using ParcelHub.Api.Models;

namespace ParcelHub.Tests;

/// <summary>Starts the real app in memory and calls it over HTTP, the same way a client would.</summary>
public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_IsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Version_ReportsTheEnvironment()
    {
        var body = await _client.GetStringAsync("/version");

        Assert.Contains("environment", body);
    }

    [Fact]
    public async Task CreateLabel_ThenOpenIt()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/labels", TestData.Outbound(), Json);
        var label = await created.Content.ReadFromJsonAsync<LabelResponse>(Json);

        Assert.Equal(ResponseStatus.OK, label!.Status);

        var preview = await _client.GetAsync($"/api/v1/labels/{label.TrackingNumber}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/svg+xml", preview.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UnknownLabel_IsNotFound()
    {
        var response = await _client.GetAsync("/api/v1/labels/PH000000000GB");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
