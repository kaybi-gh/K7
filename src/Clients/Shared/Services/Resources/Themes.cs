using K7.Clients.Shared.Domain.Models;
using MudBlazor;
using System.Collections.Frozen;

namespace K7.Clients.Shared.Services.Resources;

public static class Themes
{
    public static readonly ThemeWrapper MudBlazorDefaultTheme = new("MudBlazor default theme", new());

    public static readonly ThemeWrapper Plex = new("Plex", new()
    {
        Palette = new PaletteLight()
        {
            Primary = "#000000",
            Secondary = "#000000",
            AppbarBackground = "#594AE2",
            DrawerBackground = "#ffffff",
            DrawerIcon = "#a5a3b3"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#e5a00d",
            Secondary = "#ffffff",
            //Background = "#1d1d1d",
            Background = "#212121",
            DrawerText = "#ffffff",
            DrawerBackground = "#0c0c0c",
            Surface = "#282a2d",
            Success = "#4db288",
            AppbarBackground = "#0c0c0c",
            //DarkContrastText = "#ffffff",
        }
    });

    public static readonly FrozenSet<ThemeWrapper> Collection = new List<ThemeWrapper>
    {
        MudBlazorDefaultTheme,
        Plex
    }.ToFrozenSet();
}