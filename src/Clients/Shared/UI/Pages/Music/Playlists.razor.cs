using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class Playlists
{
    private List<LitePlaylistDto> _playlists = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadPlaylistsAsync();
    }

    private async Task LoadPlaylistsAsync()
    {
        _loading = true;
        var result = await K7ServerService.GetPlaylistsAsync();
        _playlists = result?.Items?.ToList() ?? [];
        _loading = false;
    }

    private async Task OpenCreatePlaylistDialog()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreatePlaylistDialog>("Nouvelle playlist", options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadPlaylistsAsync();
    }

    private async Task OpenCreateSmartPlaylistDialog()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<SmartPlaylistDialog>("Nouvelle smart playlist", options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid id })
        {
            try { await K7ServerService.EvaluateSmartPlaylistAsync(id); } catch { }
            NavigationManager.NavigateTo($"/smart-playlists/{id}");
        }
    }

    private async Task CreatePreset(Preset preset)
    {
        var request = BuildPresetRequest(preset);
        try
        {
            var id = await K7ServerService.CreateSmartPlaylistAsync(request);
            try { await K7ServerService.EvaluateSmartPlaylistAsync(id); } catch { }
            NavigationManager.NavigateTo($"/smart-playlists/{id}");
        }
        catch
        {
            Snackbar.Add("Erreur lors de la création", Severity.Error);
        }
    }

    private void GoToPlaylist(LitePlaylistDto playlist)
    {
        var url = playlist.IsSmartPlaylist
            ? $"/smart-playlists/{playlist.Id}"
            : $"/playlists/{playlist.Id}";
        NavigationManager.NavigateTo(url);
    }

    private string? GetCoverUrl(LitePlaylistDto playlist)
    {
        return K7ServerService.GetAbsoluteUri(
            playlist.CoverPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
    }

    private static CreateSmartPlaylistRequest BuildPresetRequest(Preset preset) => preset switch
    {
        Preset.RecentlyAdded => new()
        {
            Title = "Ajouts récents",
            Description = "Médias ajoutés dans les 30 derniers jours",
            MatchCondition = SmartPlaylistMatchCondition.All,
            Rules = [new() { Field = SmartPlaylistField.DateAdded, Operator = SmartPlaylistOperator.InLast, Value = "30" }],
            OrderBy = SmartPlaylistOrderBy.DateAdded,
            OrderDescending = true,
            Limit = 100
        },
        Preset.MostPlayed => new()
        {
            Title = "Les plus écoutés",
            Description = "Morceaux les plus joués",
            MediaType = MediaType.MusicTrack,
            MatchCondition = SmartPlaylistMatchCondition.All,
            Rules = [new() { Field = SmartPlaylistField.PlayCount, Operator = SmartPlaylistOperator.GreaterThan, Value = "0" }],
            OrderBy = SmartPlaylistOrderBy.PlayCount,
            OrderDescending = true,
            Limit = 50
        },
        Preset.NeverPlayed => new()
        {
            Title = "Jamais écoutés",
            Description = "Morceaux jamais joués",
            MediaType = MediaType.MusicTrack,
            MatchCondition = SmartPlaylistMatchCondition.All,
            Rules = [new() { Field = SmartPlaylistField.PlayCount, Operator = SmartPlaylistOperator.Equals, Value = "0" }],
            OrderBy = SmartPlaylistOrderBy.Random,
            OrderDescending = false,
            Limit = 50
        },
        Preset.HighlyRated => new()
        {
            Title = "Mieux notés",
            Description = "Morceaux notés 8 ou plus",
            MatchCondition = SmartPlaylistMatchCondition.All,
            Rules = [new() { Field = SmartPlaylistField.Rating, Operator = SmartPlaylistOperator.GreaterThanOrEqual, Value = "8" }],
            OrderBy = SmartPlaylistOrderBy.Rating,
            OrderDescending = true,
            Limit = 100
        },
        Preset.RecentlyPlayed => new()
        {
            Title = "Écoutés récemment",
            Description = "Morceaux écoutés dans les 7 derniers jours",
            MatchCondition = SmartPlaylistMatchCondition.All,
            Rules = [new() { Field = SmartPlaylistField.LastPlayed, Operator = SmartPlaylistOperator.InLast, Value = "7" }],
            OrderBy = SmartPlaylistOrderBy.LastPlayed,
            OrderDescending = true
        },
        _ => new() { Title = "Smart Playlist", Rules = [] }
    };

    internal enum Preset { RecentlyAdded, MostPlayed, NeverPlayed, HighlyRated, RecentlyPlayed }
}
