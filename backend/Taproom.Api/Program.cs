using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Taproom.Api.Clients;
using Taproom.Api.Config;
using Taproom.Api.Polling;
using Taproom.Api.Sources.Omada;
using Taproom.Api.Sources.Opnsense;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<TaproomOptions>(builder.Configuration.GetSection(TaproomOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<ClientSnapshotStore>();
builder.Services.AddHostedService<PollerService>();

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

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/debug/opnsense-raw", async (IHttpClientFactory factory) =>
    {
        var http = factory.CreateClient(nameof(IOpnsenseClient));
        var arp = await http.GetStringAsync("/api/diagnostics/interface/get_arp");
        var leases = await http.GetStringAsync("/api/dnsmasq/leases/search");
        return Results.Text($"ARP:\n{arp}\n\nLEASES:\n{leases}", "text/plain");
    });

    app.MapGet("/api/debug/omada-clients-raw", async (IOmadaClient omada, CancellationToken ct) =>
    {
        var clients = await omada.GetClientsAsync(ct);
        return Results.Ok(clients);
    });

    app.MapGet("/api/debug/omada-sites-raw", async (IHttpClientFactory factory, IOmadaClient omada, CancellationToken ct) =>
    {
        // force auth + omadacId to be established
        await omada.GetClientsAsync(ct);
        var http = factory.CreateClient(nameof(IOmadaClient));
        var info = await http.GetStringAsync("/api/info", ct);
        var omadacIdStart = info.IndexOf("\"omadacId\":\"") + "\"omadacId\":\"".Length;
        var omadacId = info[omadacIdStart..info.IndexOf('"', omadacIdStart)];
        var token = typeof(OmadaClient).GetField("_accessToken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(omada) as string;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/openapi/v1/{omadacId}/sites?page=1&pageSize=100");
        request.Headers.TryAddWithoutValidation("Authorization", $"AccessToken={token}");
        var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        return Results.Text(text, "application/json");
    });

    app.MapGet("/api/debug/omada-clients-fulltext", async (IHttpClientFactory factory, IOmadaClient omada, CancellationToken ct) =>
    {
        await omada.GetClientsAsync(ct);
        var http = factory.CreateClient(nameof(IOmadaClient));
        var info = await http.GetStringAsync("/api/info", ct);
        var omadacIdStart = info.IndexOf("\"omadacId\":\"") + "\"omadacId\":\"".Length;
        var omadacId = info[omadacIdStart..info.IndexOf('"', omadacIdStart)];
        var token = typeof(OmadaClient).GetField("_accessToken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(omada) as string;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/openapi/v1/{omadacId}/sites/6aad187d138e6614180bc44e/clients?page=1&pageSize=500");
        request.Headers.TryAddWithoutValidation("Authorization", $"AccessToken={token}");
        var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        return Results.Text(text, "application/json");
    });
}

app.MapGet("/api/health", (ClientSnapshotStore store) =>
{
    var snapshot = store.Current;
    return Results.Ok(new { status = "ok", sources = snapshot?.Sources });
});

app.MapGet("/api/clients", (ClientSnapshotStore store) =>
{
    var snapshot = store.Current;
    return snapshot is null ? Results.StatusCode(503) : Results.Ok(snapshot);
});

app.MapFallbackToFile("index.html");

app.Run();
