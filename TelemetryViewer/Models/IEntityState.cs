using System.ComponentModel;

namespace TelemetryViewer.Models;

/// <summary>
/// Common contract that every state class must implement.
/// Blazor components program against this interface so they
/// don't need to know the concrete type to render a sidebar
/// row, last-seen timestamp, or status badge.
/// </summary>
public interface IEntityState : INotifyPropertyChanged
{
    string   EntityId   { get; }
    string   EntityType { get; }   // e.g. "Vehicle", "Sensor", "Asset"
    string   Status     { get; }   // e.g. "moving", "idle", "offline"
    string   Alert      { get; }   // "" means no alert
    DateTime LastSeen   { get; }
}
