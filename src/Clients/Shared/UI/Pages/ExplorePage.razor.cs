using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class ExplorePage
{
    private List<LibraryDto> _libraries = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
        }
        catch
        {
            _libraries = [];
        }
    }

    private static string GetLibraryIconName(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => "film-strip",
        LibraryMediaType.Serie => "television",
        LibraryMediaType.Music => "music-notes",
        _ => "folder"
    };

    private static (string GradientStart, string GradientEnd, string IconColor) GetLibraryCardColors(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => ("rgba(120,30,30,0.85)", "rgba(20,10,10,0.9)", "rgba(180,40,40,0.6)"),
        LibraryMediaType.Serie => ("rgba(20,60,120,0.85)", "rgba(10,15,40,0.9)", "rgba(30,80,160,0.6)"),
        LibraryMediaType.Music => ("rgba(80,20,100,0.85)", "rgba(15,10,30,0.9)", "rgba(110,30,140,0.6)"),
        _ => ("rgba(30,60,40,0.85)", "rgba(10,20,15,0.9)", "rgba(40,80,55,0.6)")
    };
}
