using System.Text.Json;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Features.CustomNav;

internal static class CustomNavLayoutParser
{
    public static CustomNavLayoutDto ParseOrDefault(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CustomNavLayoutDto.Disabled();

        try
        {
            return JsonSerializer.Deserialize<CustomNavLayoutDto>(json) ?? CustomNavLayoutDto.Disabled();
        }
        catch (JsonException)
        {
            return CustomNavLayoutDto.Disabled();
        }
    }
}
