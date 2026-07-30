using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class GenreRadioDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }

    private bool _playing;
    private string? _selectedGenre;

    private void Cancel() => Dialog.Close(K7DialogResult.Cancel());

    private void OnGenreChanged(string? genre) => _selectedGenre = genre;

    private Task OnGenrePickedAsync(string? genre)
    {
        _selectedGenre = genre;
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<string>> SearchGenresAsync(string searchText, CancellationToken cancellationToken) =>
        await MediaBrowseTagSearch.SearchAsync(
            MediaService,
            nameof(DynamicPlaylistField.Genre),
            searchText,
            LibraryIds,
            LibraryGroupIds,
            MediaType.MusicTrack,
            cancellationToken);

    private async Task PlayAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedGenre) || _playing)
            return;

        _playing = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var started = await MusicRadio.StartAsync(new MusicRadioRequest
            {
                RadioType = MusicRadioType.Genre.ToString(),
                Title = string.Format(L["RadioTitle"], _selectedGenre),
                LibraryIds = LibraryIds,
                LibraryGroupIds = LibraryGroupIds,
                Genre = _selectedGenre
            });

            if (!started)
            {
                Snackbar.Add(L["EmptyRadio"], K7Severity.Info);
                return;
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        finally
        {
            _playing = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
