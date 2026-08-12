using System.Text.Json;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Services;

/// <summary>
/// Home layout is stored via <c>SettingKey&lt;string&gt;</c>, so handlers serialize the DTO to JSON
/// and the settings service JSON-encodes that string again. Readers that go through GetAsync unwrap
/// once; raw <c>setting.Value</c> access must use this helper.
/// </summary>
public static class HomeLayoutSettingSerializer
{
    public static bool TryDeserialize(string value, out HomeLayoutDto? layout)
    {
        layout = null;

        try
        {
            var json = UnwrapStoredValue(value);
            layout = JsonSerializer.Deserialize<HomeLayoutDto>(json);
            return layout is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string Serialize(HomeLayoutDto layout)
    {
        // Preserve SettingKey<string> double-encoding so GetAsync<string> keeps working.
        return JsonSerializer.Serialize(JsonSerializer.Serialize(layout));
    }

    private static string UnwrapStoredValue(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '"')
        {
            var unwrapped = JsonSerializer.Deserialize<string>(value);
            if (unwrapped is not null)
                return unwrapped;
        }

        return value;
    }
}
