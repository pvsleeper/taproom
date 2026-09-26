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
