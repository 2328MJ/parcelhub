using System.Text.Json.Serialization;
using ParcelHub.Api.Services;
using ParcelHub.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ParcelHubSettings>(builder.Configuration.GetSection(ParcelHubSettings.SectionName));

builder.Services.AddSingleton<RequestValidator>();
builder.Services.AddSingleton<RequestEnhancer>();
builder.Services.AddSingleton<ErrorHarmoniser>();
builder.Services.AddSingleton<LabelStore>();
builder.Services.AddSingleton<ICarrierClient, FakeCarrierClient>();
builder.Services.AddScoped<LabelService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger is on in every environment: this is a sandbox.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

// The pipeline will set these per environment, so you can see which build is running where.
app.MapGet("/version", (IConfiguration config) => new
{
    environment = config["Deployment:Environment"] ?? "Local",
    build = config["Deployment:Build"] ?? "dev",
    commit = config["Deployment:Commit"] ?? "unknown"
});

app.Run();

// Lets the integration tests start the app in memory.
public partial class Program { }
