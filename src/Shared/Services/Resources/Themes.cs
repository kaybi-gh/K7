using MediaClient.Shared.Models;
using MudBlazor;
using System.Collections.Frozen;

namespace MediaClient.Shared.Services.Resources;

public static class Themes
{
    public static readonly ThemeWrapper MudBlazorDefaultTheme = new("MudBlazor default theme", new());

    public static readonly ThemeWrapper MyTheme = new("My theme", new()
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
            Primary = "#ffffff",
            Secondary = "#ffffff",
            DarkContrastText = "#ffffff",
        }
    });

    public static readonly FrozenSet<ThemeWrapper> Collection = new List<ThemeWrapper>
    {
        MudBlazorDefaultTheme,
        MyTheme
    }.ToFrozenSet();
}