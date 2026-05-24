using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class Playlists
{
    private List<LitePlaylistDto> _playlists = [];
    private bool _loading = true;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

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
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreatePlaylistDialog>("Nouvelle playlist", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadPlaylistsAsync();
    }

    private async Task OpenCreateSmartPlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<SmartPlaylistDialog>("Nouvelle smart playlist", null, options);
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
            Snackbar.Add("Erreur lors de la création", K7Severity.Error);
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
        return ApiClient.GetAbsoluteUri(
            playlist.CoverPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
    }

    private static CreateSmartPlaylistRequest BuildPresetRequest(Preset preset) => preset switch
    {
        Preset.RecentlyAdded => new()
        {
            Title = "Ajouts recents",
            Description = "Medias ajoutes dans les 30 derniers jours",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(SmartPlaylistField.DateAdded), RuleOperator.InLast, "30"),
            OrderBy = SmartPlaylistOrderBy.DateAdded,
            OrderDescending = true,
            Limit = 100
        },
        Preset.MostPlayed => new()
        {
            Title = "Les plus ecoutes",
            Description = "Morceaux les plus joues",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(SmartPlaylistField.PlayCount), RuleOperator.GreaterThan, "0"),
            OrderBy = SmartPlaylistOrderBy.PlayCount,
            OrderDescending = true,
            Limit = 50
        },
        Preset.NeverPlayed => new()
        {
            Title = "Jamais ecoutes",
            Description = "Morceaux jamais joues",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(SmartPlaylistField.PlayCount), RuleOperator.Equals, "0"),
            OrderBy = SmartPlaylistOrderBy.Random,
            OrderDescending = false,
            Limit = 50
        },
        Preset.HighlyRated => new()
        {
            Title = "Mieux notes",
            Description = "Morceaux notes 8 ou plus",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(SmartPlaylistField.Rating), RuleOperator.GreaterThanOrEqual, "8"),
            OrderBy = SmartPlaylistOrderBy.Rating,
            OrderDescending = true,
            Limit = 100
        },
        Preset.RecentlyPlayed => new()
        {
            Title = "Ecoutes recemment",
            Description = "Morceaux ecoutes dans les 7 derniers jours",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(SmartPlaylistField.LastPlayed), RuleOperator.InLast, "7"),
            OrderBy = SmartPlaylistOrderBy.LastPlayed,
            OrderDescending = true
        },
        _ => new() { Title = "Smart Playlist", MediaType = MediaType.MusicTrack }
    };

    private static RuleGroupDto BuildSingleRule(string field, RuleOperator op, string? value) => new()
    {
        MatchCondition = RuleMatchCondition.All,
        Items = [new ConditionRuleItemDto { Field = field, Operator = op, Value = value }]
    };

    internal enum Preset { RecentlyAdded, MostPlayed, NeverPlayed, HighlyRated, RecentlyPlayed }
}
