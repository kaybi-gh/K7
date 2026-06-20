using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class ExplorePage
{
    private List<LibraryGroupDto> _libraryGroups = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraryGroups = await LibraryService.GetLibraryGroupsAsync();
        }
        catch
        {
            _libraryGroups = [];
        }
    }

    private static string GetLibraryIconName(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => "film-strip",
        LibraryMediaType.Serie => "television",
        LibraryMediaType.Music => "music-notes",
        _ => "folder"
    };
}
