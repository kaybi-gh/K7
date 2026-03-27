using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class AddToPlaylistDialog
{
    [Inject] private IPlaylistService K7ServerService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public Guid MediaId { get; set; }

    private List<LitePlaylistDto> _playlists = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadPlaylists();
    }

    private async Task LoadPlaylists()
    {
        _loading = true;
        var result = await K7ServerService.GetPlaylistsAsync(1, 100);
        _playlists = result?.Items?.Where(p => !p.IsSmartPlaylist).ToList() ?? [];
        _loading = false;
    }

    private async Task SelectPlaylist(LitePlaylistDto playlist)
    {
        try
        {
            await K7ServerService.AddPlaylistItemAsync(playlist.Id, MediaId);
            Snackbar.Add($"Ajouté à « {playlist.Title} »", Severity.Success);
            MudDialog.Close(DialogResult.Ok(playlist.Id));
        }
        catch
        {
            Snackbar.Add("Erreur lors de l'ajout", Severity.Error);
        }
    }

    private async Task CreateNewPlaylist()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreatePlaylistDialog>("Nouvelle playlist", options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid newId })
        {
            try
            {
                await K7ServerService.AddPlaylistItemAsync(newId, MediaId);
                Snackbar.Add("Ajouté à la nouvelle playlist", Severity.Success);
                MudDialog.Close(DialogResult.Ok(newId));
            }
            catch
            {
                Snackbar.Add("Playlist créée mais erreur lors de l'ajout", Severity.Warning);
                MudDialog.Close(DialogResult.Ok(newId));
            }
        }
        else
        {
            await LoadPlaylists();
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
