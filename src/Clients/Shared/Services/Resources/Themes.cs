using K7.Clients.Shared.Models;
using MudBlazor;
using System.Collections.Frozen;

namespace K7.Clients.Shared.Services.Resources;

public static class Themes
{
    public static readonly ThemeWrapper MudBlazorDefaultTheme = new("MudBlazor default theme", new());

    public static readonly ThemeWrapper Plex = new("Plex", new()
    {
        PaletteLight = new PaletteLight()
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
            TextPrimary = "#ffffff",
            //Background = "#1d1d1d",
            Background = "#212121",
            DrawerText = "#ffffff",
            DrawerBackground = "#0c0c0c",
            Surface = "#282a2d",
            Success = "#4db288",
            AppbarBackground = "#0c0c0c",
            //DarkContrastText = "#ffffff",
        },
        Typography = new Typography()
        {
            Default = { FontFamily = ["Manrope", "Helvetica", "Arial", "sans-serif"] },
            H1 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            H2 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            H3 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            H4 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            H5 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            H6 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            Body1 = { FontFamily = ["Manrope", "Helvetica", "Arial", "sans-serif"] },
            Body2 = { FontFamily = ["Manrope", "Helvetica", "Arial", "sans-serif"] },
            Button = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            Caption = { FontFamily = ["Manrope", "Helvetica", "Arial", "sans-serif"] },
            Subtitle1 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            Subtitle2 = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] },
            Overline = { FontFamily = ["Epilogue", "Helvetica", "Arial", "sans-serif"] }
        }
    });

    public static readonly FrozenSet<ThemeWrapper> Collection = new List<ThemeWrapper>
    {
        MudBlazorDefaultTheme,
        Plex
    }.ToFrozenSet();
}
