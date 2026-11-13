using System.Text.Json;
using System.Text.Json.Serialization;

namespace K7.Clients.Shared.UI.Helpers;

public sealed class SettingsFormTracker<T> where T : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private string? _savedJson;

    public void Capture(T state) => _savedJson = Serialize(state);

    /// <summary>
    /// Serialized form of <paramref name="state"/>, comparable with ordinal equality. Take it
    /// before an async save and hand it to <see cref="CaptureSnapshot"/> afterwards, so edits made
    /// while the save was in flight still count as dirty.
    /// </summary>
    public string Snapshot(T state) => Serialize(state);

    /// <summary>Marks a snapshot from <see cref="Snapshot"/> as the saved state.</summary>
    public void CaptureSnapshot(string snapshot) => _savedJson = snapshot;

    public bool IsDirty(T state) =>
        _savedJson is null || !string.Equals(_savedJson, Serialize(state), StringComparison.Ordinal);

    public T Restore()
    {
        if (_savedJson is null)
            throw new InvalidOperationException("No saved snapshot to restore.");

        return Deserialize(_savedJson);
    }

    private static string Serialize(T state) => JsonSerializer.Serialize(state, JsonOptions);

    private static T Deserialize(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize settings snapshot.");
}
