using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public partial class EpisodeListItem
{
    [Inject] private IMediaService K7ServerService { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Parameter, EditorRequired]
    public required LiteSerieEpisodeDto Episode { get; set; }

    [Parameter]
    public string? StillUrl { get; set; }

    [Parameter]
    public bool IsExpanded { get; set; }

    [Parameter]
    public EventCallback OnToggleExpand { get; set; }

    [Parameter]
    public EventCallback OnPlay { get; set; }

    private SerieEpisodeDto? _fullEpisode;

    protected override async Task OnParametersSetAsync()
    {
        if (IsExpanded && _fullEpisode?.Id != Episode.Id)
        {
            var media = await K7ServerService.GetMediaAsync(Episode.Id);
            _fullEpisode = media as SerieEpisodeDto;
        }
    }

    private async Task HandlePlay()
    {
        await OnPlay.InvokeAsync();
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Code is "Enter" or "Space")
        {
            await OnToggleExpand.InvokeAsync();
        }
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h{ts.Minutes:00}"
            : $"{ts.Minutes}min";
    }

    private async Task OpenIndexedFilesDialogAsync()
    {
        if (_fullEpisode is null) return;
        var parameters = new K7DialogParameters<IndexedFilesDialog>
        {
            { x => x.Media, _fullEpisode }
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<IndexedFilesDialog>(S["IndexedVersions"], parameters, options);
    }

    private VideoFileMetadataDto? GetVideoMetadata()
    {
        return _fullEpisode?.IndexedFiles?
            .FirstOrDefault(f => f.Id == Episode.IndexedFileId)
            ?.FileMetadata as VideoFileMetadataDto;
    }
}
