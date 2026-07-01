using System.Text.Json;
using System.Text.Json.Serialization;

namespace K7.Shared.Json;

public static class K7JsonSerializerOptions
{
    public static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions();
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.PropertyNameCaseInsensitive = true;

        if (!options.Converters.Any(converter => converter is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }
}
