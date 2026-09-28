using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Taproom.Api.Clients;
using Taproom.Api.Config;
using Taproom.Api.Sources.Omada;
using Taproom.Api.Sources.Opnsense;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<TaproomOptions>(builder.Configuration.GetSection(TaproomOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<ClientSnapshotProvider>();

builder.Services.AddHttpClient<IOmadaClient, OmadaClient>((sp, http) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaproomOptions>>().Value.Omada;
    http.BaseAddress = new Uri(options.BaseUrl);
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaproomOptions>>().Value.Omada;
    var handler = new HttpClientHandler();
    if (options.AllowInvalidCertificate)
    {
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    }
    return handler;
});

builder.Services.AddHttpClient<IOpnsenseClient, OpnsenseClient>((sp, http) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaproomOptions>>().Value.Opnsense;
    http.BaseAddress = new Uri(options.BaseUrl);
    var basicAuth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{options.ApiKey}:{options.ApiSecret}"));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaproomOptions>>().Value.Opnsense;
    var handler = new HttpClientHandler();
    if (options.AllowInvalidCertificate)
    {
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    }
    return handler;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/clients", async (ClientSnapshotProvider provider, CancellationToken ct) =>
    Results.Ok(await provider.GetSnapshotAsync(ct)));

app.MapFallbackToFile("index.html");

app.Run();
