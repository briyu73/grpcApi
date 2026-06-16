using TelemetryViewer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register the gRPC address from config
var grpcAddress = builder.Configuration["Telemetry:GrpcAddress"] ?? "https://localhost:5001";

// TrackingClient is a long-lived object — singleton lifetime
builder.Services.AddSingleton<TrackingClient>(_ => new TrackingClient(grpcAddress));

// TelemetryService is the Blazor-aware wrapper — also singleton so all
// components share the same live state
builder.Services.AddSingleton<TelemetryService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<TelemetryViewer.Components.App>()
    .AddInteractiveServerRenderMode();

// Start receiving telemetry as soon as the app starts
var telemetry = app.Services.GetRequiredService<TelemetryService>();
await telemetry.StartAsync();

app.Run();
