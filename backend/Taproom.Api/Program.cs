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
builder.Services.AddSingleton<GeoIpService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DomainResolver>();
builder.Services.AddSingleton<ConnectionsService>();
builder.Services.AddSingleton<DnsPanelService>();

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

void ConfigureOpnsenseClient(IHttpClientBuilder clientBuilder)
{
    clientBuilder.ConfigureHttpClient((sp, http) =>
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
}

ConfigureOpnsenseClient(builder.Services.AddHttpClient<IOpnsenseClient, OpnsenseClient>());
ConfigureOpnsenseClient(builder.Services.AddHttpClient<IStatesClient, StatesClient>());
ConfigureOpnsenseClient(builder.Services.AddHttpClient<IUnboundLogClient, UnboundLogClient>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/clients", async (ClientSnapshotProvider provider, CancellationToken ct) =>
    Results.Ok(await provider.GetSnapshotAsync(ct)));

app.MapGet("/api/clients/{mac}", async (string mac, ClientSnapshotProvider provider, CancellationToken ct) =>
{
    var snapshot = await provider.GetSnapshotAsync(ct);
    var client = snapshot.Clients.FirstOrDefault(c => c.Mac == mac);
    return client is null ? Results.NotFound() : Results.Ok(client);
});

app.MapGet("/api/clients/{mac}/connections", async (string mac, ConnectionsService service, CancellationToken ct) =>
{
    var result = await service.GetConnectionsAsync(mac, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/api/clients/{mac}/dns", async (string mac, int? minutes, int? tail, DnsPanelService service, CancellationToken ct) =>
{
    var result = await service.GetDnsAsync(mac, minutes ?? 60, tail ?? 50, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapFallbackToFile("index.html");

app.Run();
