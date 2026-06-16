using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TelemetryViewer.Models;

/// <summary>
/// Example of a second entity type. Demonstrates that the UI
/// components work against IEntityState regardless of the
/// concrete type behind it.
/// </summary>
public sealed class SensorState : IEntityState
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── IEntityState ────────────────────────────────────────
    public string EntityId   => SensorId;
    public string EntityType => "Sensor";
    public string Status     => _online ? "online" : "offline";
    public string Alert      => _alert;

    // ── Identity ─────────────────────────────────────────────
    public string SensorId   { get; init; } = string.Empty;
    public string SensorKind { get; init; } = string.Empty;  // "env", "rf", etc.

    // ── Timestamps ───────────────────────────────────────────
    private DateTime _lastSeen;
    public  DateTime LastSeen { get => _lastSeen; private set => Set(ref _lastSeen, value); }

    // ── Readings ─────────────────────────────────────────────
    private float  _temperature;
    private float  _humidity;
    private float  _signalDbm;
    private bool   _online;
    private string _alert = string.Empty;

    public float  Temperature { get => _temperature; private set => Set(ref _temperature, value); }
    public float  Humidity    { get => _humidity;    private set => Set(ref _humidity,    value); }
    public float  SignalDbm   { get => _signalDbm;   private set => Set(ref _signalDbm,   value); }
    public bool   Online      { get => _online;      private set => Set(ref _online,      value); }

    private string AlertBacking { set => Set(ref _alert, value, nameof(Alert)); }

    public int UpdateCount { get; private set; }

    // ── Apply (called by whatever store manages SensorState) ──
    public void ApplyUpdate(
        float? temperature = null,
        float? humidity    = null,
        float? signalDbm   = null,
        bool?  online      = null,
        string? alert      = null)
    {
        LastSeen = DateTime.UtcNow;
        if (temperature.HasValue) Temperature = temperature.Value;
        if (humidity.HasValue)    Humidity    = humidity.Value;
        if (signalDbm.HasValue)   SignalDbm   = signalDbm.Value;
        if (online.HasValue)      Online      = online.Value;
        if (alert is not null)    AlertBacking = alert;
        UpdateCount++;
    }
}
