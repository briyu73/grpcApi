using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Components;
using TelemetryViewer.Models;

namespace TelemetryViewer.Services;

/// <summary>
/// Blazor-aware wrapper around TrackingClient.
///
/// Responsibilities:
///   - Start / stop the gRPC stream
///   - Own the ObservableCollection the sidebar binds to
///   - Marshal VehicleAdded / entity-added events onto the
///     Blazor synchronisation context so StateHasChanged is
///     always called on the right thread
/// </summary>
public sealed class TelemetryService : IAsyncDisposable
{
    private readonly TrackingClient _client;
    private readonly ILogger<TelemetryService> _logger;

    // All known entities in arrival order — sidebar binds to this
    public ObservableCollection<IEntityState> Entities { get; } = new();

    // Convenience typed views (built from the same objects)
    public IEnumerable<VehicleState> Vehicles => Entities.OfType<VehicleState>();
    public IEnumerable<SensorState>  Sensors  => Entities.OfType<SensorState>();

    // Components subscribe to this to trigger their own StateHasChanged
    public event Action<IEntityState>? EntityUpdated;

    // Blazor's dispatcher — set on first component render so we always
    // have a valid one; falls back to a no-op before any UI is attached
    private IComponentRenderMode? _dispatcher;
    private readonly SynchronizationContext? _syncContext;

    public TelemetryService(TrackingClient client, ILogger<TelemetryService> logger)
    {
        _client     = client;
        _logger     = logger;
        _syncContext = SynchronizationContext.Current;

        // Wire up store events from the library
        _client.Store.VehicleAdded += OnVehicleAdded;
    }

    public async Task StartAsync(IEnumerable<string>? initialIds = null)
    {
        _logger.LogInformation("Starting telemetry stream");
        _client.Start(initialIds);
        await Task.CompletedTask;
    }

    public Task AddSubscriptionAsync(IEnumerable<string> ids)
        => _client.AddVehiclesAsync(ids);

    public Task RemoveSubscriptionAsync(IEnumerable<string> ids)
        => _client.RemoveVehiclesAsync(ids);

    // ── Store event handlers ──────────────────────────────────

    private void OnVehicleAdded(VehicleState state)
    {
        // Subscribe to the entity's own property changes so we can
        // forward them to interested components
        state.PropertyChanged += (_, _) => OnEntityUpdated(state);

        Marshal(() => Entities.Add(state));
    }

    private void OnEntityUpdated(IEntityState state)
    {
        Marshal(() => EntityUpdated?.Invoke(state));
    }

    // ── Thread marshalling ────────────────────────────────────

    /// <summary>
    /// Posts <paramref name="action"/> onto the captured sync context
    /// (which is Blazor's circuit context on Server, or just runs
    /// inline on WASM where everything is single-threaded).
    /// </summary>
    private void Marshal(Action action)
    {
        if (_syncContext is null || _syncContext == SynchronizationContext.Current)
            action();
        else
            _syncContext.Post(_ => action(), null);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Store.VehicleAdded -= OnVehicleAdded;
        await _client.DisposeAsync();
    }
}
