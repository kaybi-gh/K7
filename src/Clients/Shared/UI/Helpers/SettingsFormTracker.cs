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
