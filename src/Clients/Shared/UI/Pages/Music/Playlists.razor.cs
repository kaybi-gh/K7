using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class Playlists
{
    private List<LitePlaylistDto> _playlists = [];
    private bool _loading = true;
    private bool _canCreate;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _canCreate = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
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

    private async Task OpenCreateDynamicPlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<DynamicPlaylistDialog>(L["DynamicPlaylist"], null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid id })
        {
            try { await K7ServerService.EvaluateDynamicPlaylistAsync(id); } catch { }
            NavigationManager.NavigateTo($"/dynamic-playlists/{id}");
        }
    }

    private async Task CreatePreset(Preset preset)
    {
        var request = BuildPresetRequest(preset);
        try
        {
            var id = await K7ServerService.CreateDynamicPlaylistAsync(request);
            try { await K7ServerService.EvaluateDynamicPlaylistAsync(id); } catch { }
            NavigationManager.NavigateTo($"/dynamic-playlists/{id}");
        }
        catch
        {
            Snackbar.Add("Erreur lors de la création", K7Severity.Error);
        }
    }

    private string GetPlaylistHref(LitePlaylistDto playlist) =>
        playlist.IsDynamicPlaylist
            ? $"/dynamic-playlists/{playlist.Id}"
            : $"/playlists/{playlist.Id}";

    private string GetPlaylistSubtitle(LitePlaylistDto playlist) =>
        $"{playlist.ItemCount} {S["Tracks"]}";

    private static CreateDynamicPlaylistRequest BuildPresetRequest(Preset preset) => preset switch
    {
        Preset.RecentlyAdded => new()
        {
            Title = "Ajouts recents",
            Description = "Medias ajoutes dans les 30 derniers jours",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(DynamicPlaylistField.DateAdded), RuleOperator.InLast, "30"),
            OrderBy = DynamicPlaylistOrderBy.DateAdded,
            OrderDescending = true,
            Limit = 100
        },
        Preset.MostPlayed => new()
        {
            Title = "Les plus ecoutes",
            Description = "Morceaux les plus joues",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(DynamicPlaylistField.PlayCount), RuleOperator.GreaterThan, "0"),
            OrderBy = DynamicPlaylistOrderBy.PlayCount,
            OrderDescending = true,
            Limit = 50
        },
        Preset.NeverPlayed => new()
        {
            Title = "Jamais ecoutes",
            Description = "Morceaux jamais joues",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(DynamicPlaylistField.PlayCount), RuleOperator.Equals, "0"),
            OrderBy = DynamicPlaylistOrderBy.Random,
            OrderDescending = false,
            Limit = 50
        },
        Preset.HighlyRated => new()
        {
            Title = "Mieux notes",
            Description = "Morceaux notes 8 ou plus",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(DynamicPlaylistField.Rating), RuleOperator.GreaterThanOrEqual, "8"),
            OrderBy = DynamicPlaylistOrderBy.Rating,
            OrderDescending = true,
            Limit = 100
        },
        Preset.RecentlyPlayed => new()
        {
            Title = "Ecoutes recemment",
            Description = "Morceaux ecoutes dans les 7 derniers jours",
            MediaType = MediaType.MusicTrack,
            RuleFilter = BuildSingleRule(nameof(DynamicPlaylistField.LastPlayed), RuleOperator.InLast, "7"),
            OrderBy = DynamicPlaylistOrderBy.LastPlayed,
            OrderDescending = true
        },
        _ => new() { Title = "Dynamic Playlist", MediaType = MediaType.MusicTrack }
    };

    private static RuleGroupDto BuildSingleRule(string field, RuleOperator op, string? value) => new()
    {
        MatchCondition = RuleMatchCondition.All,
        Items = [new ConditionRuleItemDto { Field = field, Operator = op, Value = value }]
    };

    internal enum Preset { RecentlyAdded, MostPlayed, NeverPlayed, HighlyRated, RecentlyPlayed }
}
