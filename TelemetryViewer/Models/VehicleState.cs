using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tracking;

namespace TelemetryViewer.Models;

public sealed class VehicleState : IEntityState
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── IEntityState ────────────────────────────────────────
    public string EntityId   => VehicleId;
    public string EntityType => "Vehicle";
    public string Status     => _state;
    public string Alert      => _alert;

    // ── Identity ─────────────────────────────────────────────
    public string VehicleId { get; init; } = string.Empty;

    // ── Timestamps ───────────────────────────────────────────
    private DateTime _lastSeen;
    public  DateTime LastSeen { get => _lastSeen; private set => Set(ref _lastSeen, value); }

    // ── Location ─────────────────────────────────────────────
    private double _latitude;
    private double _longitude;
    private float  _altitude;
    public double Latitude  { get => _latitude;  private set => Set(ref _latitude,  value); }
    public double Longitude { get => _longitude; private set => Set(ref _longitude, value); }
    public float  Altitude  { get => _altitude;  private set => Set(ref _altitude,  value); }

    // ── Motion ───────────────────────────────────────────────
    private float _speedMs;
    private float _headingDeg;
    private float _climbMs;
    public float SpeedMs    { get => _speedMs;    private set => Set(ref _speedMs,    value); }
    public float HeadingDeg { get => _headingDeg; private set => Set(ref _headingDeg, value); }
    public float ClimbMs    { get => _climbMs;    private value => Set(ref _climbMs,    value); }

    // ── Physical ─────────────────────────────────────────────
    private float _fuelPercent;
    private float _engineTempC;
    private float _payloadKg;
    public float FuelPercent { get => _fuelPercent; private set => Set(ref _fuelPercent, value); }
    public float EngineTempC { get => _engineTempC; private set => Set(ref _engineTempC, value); }
    public float PayloadKg   { get => _payloadKg;   private set => Set(ref _payloadKg,   value); }

    // ── Status ───────────────────────────────────────────────
    private string _state = string.Empty;
    private string _alert = string.Empty;
    public string State { get => _state; private set => Set(ref _state, value); }
    // Alert exposed via IEntityState.Alert — backed by _alert field above
    private string AlertBacking { set => Set(ref _alert, value, nameof(Alert)); }

    // ── History ──────────────────────────────────────────────
    private readonly Queue<TelemetrySnapshot> _history = new();
    private const int MaxHistory = 50;
    public IReadOnlyList<TelemetrySnapshot> History => _history.ToList();

    public int UpdateCount { get; private set; }

    // ── Apply ────────────────────────────────────────────────
    internal void ApplyUpdate(TelemetryUpdate update)
    {
        LastSeen = update.Timestamp.ToDateTime();

        if (update.Location is { } loc)
        {
            Latitude  = loc.Latitude;
            Longitude = loc.Longitude;
            Altitude  = loc.Altitude;
        }
        if (update.Motion is { } mot)
        {
            SpeedMs    = mot.SpeedMs;
            HeadingDeg = mot.HeadingDeg;
            ClimbMs    = mot.ClimbMs;
        }
        if (update.Physical is { } phys)
        {
            FuelPercent = phys.FuelPercent;
            EngineTempC = phys.EngineTempC;
            PayloadKg   = phys.PayloadKg;
        }
        if (update.Status is { } status)
        {
            State       = status.State;
            AlertBacking = status.Alert;
        }

        UpdateCount++;
        _history.Enqueue(TelemetrySnapshot.From(this));
        if (_history.Count > MaxHistory)
            _history.Dequeue();
    }
}

public sealed record TelemetrySnapshot(
    DateTime Timestamp,
    double Latitude, double Longitude, float Altitude,
    float SpeedMs, float HeadingDeg,
    string State, string Alert)
{
    internal static TelemetrySnapshot From(VehicleState s) => new(
        s.LastSeen,
        s.Latitude, s.Longitude, s.Altitude,
        s.SpeedMs,  s.HeadingDeg,
        s.State,    s.Alert);
}
