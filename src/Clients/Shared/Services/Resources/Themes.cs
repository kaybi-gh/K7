using K7.Clients.Shared.Models;
using System.Collections.Frozen;

namespace K7.Clients.Shared.Services.Resources;

public static class Themes
{
    public static readonly ThemeDefinition DefaultDark = new("Default Dark", "default-dark");
    public static readonly ThemeDefinition DefaultLight = new("Default Light", "default-light");
    public static readonly ThemeDefinition Amoled = new("AMOLED", "amoled");

    public static readonly FrozenSet<ThemeDefinition> Collection = new List<ThemeDefinition>
    {
        DefaultDark,
        DefaultLight,
        Amoled,
    }.ToFrozenSet();
}
