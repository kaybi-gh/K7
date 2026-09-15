using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Pages.Dialogs;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class CustomNavLayoutFields
{
    [Parameter, EditorRequired] public CustomNavLayoutEditModel Model { get; set; } = default!;
    [Parameter, EditorRequired] public IReadOnlyList<LibraryGroupDto> Groups { get; set; } = [];
    [Parameter] public IReadOnlyList<LiteCollectionDto> Collections { get; set; } = [];
    [Parameter] public IReadOnlyList<LitePlaylistDto> Playlists { get; set; } = [];
    [Parameter, EditorRequired] public string Description { get; set; } = "";
    [Parameter, EditorRequired] public string EnabledLabel { get; set; } = "";
    [Parameter, EditorRequired] public string ItemsEmpty { get; set; } = "";
    [Parameter] public string? EnabledHint { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback Changed { get; set; }

    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    private static readonly string[] RowPageKeys = ["home", "explore-feeds"];
    private static readonly string[] BarPageKeys = ["home", "explore", "myspace", "settings", "media"];

    private IReadOnlyList<string> DeviceKeys =>
        Model.Placement == CustomNavPlacement.Bar
            ? ["desktop", "tv"]
            : ["desktop", "phone", "tv"];

    private IReadOnlyList<ButtonGroupOption<CustomNavPlacement>> PlacementOptions =>
    [
        new(CustomNavPlacement.FeedRow, L["PlacementFeedRow"]),
        new(CustomNavPlacement.Bar, L["PlacementBar"])
    ];

    private bool IsListDisabled(string _) => Disabled;

    private Task NotifyAsync() => Changed.InvokeAsync();

    private Task OnEnabledChanged(bool value)
    {
        Model.Enabled = value;
        return NotifyAsync();
    }

    private Task OnPlacementChanged(CustomNavPlacement value)
    {
        Model.Placement = value;
        if (value == CustomNavPlacement.Bar)
        {
            Model.ShowOnPhone = false;
            if (!Model.ShowOnDesktop && !Model.ShowOnTv)
                Model.ShowOnDesktop = true;
        }

        return NotifyAsync();
    }

    private Task OnBarShowIconsChanged(bool value)
    {
        Model.BarShowIcons = value;
        return NotifyAsync();
    }

    private Task OnFeedRowTitleChanged(string? value)
    {
        Model.FeedRowTitle = value ?? "";
        return NotifyAsync();
    }

    private string GetDeviceLabel(string key) => key switch
    {
        "desktop" => L["DeviceDesktop"],
        "phone" => L["DevicePhone"],
        "tv" => L["DeviceTv"],
        _ => key
    };

    private bool IsDeviceChecked(string key) => key switch
    {
        "desktop" => Model.ShowOnDesktop,
        "phone" => Model.ShowOnPhone,
        "tv" => Model.ShowOnTv,
        _ => false
    };

    private Task OnDeviceChecked((string Item, bool Checked) value)
    {
        switch (value.Item)
        {
            case "desktop":
                Model.ShowOnDesktop = value.Checked;
                break;
            case "phone":
                Model.ShowOnPhone = value.Checked;
                break;
            case "tv":
                Model.ShowOnTv = value.Checked;
                break;
        }

        return NotifyAsync();
    }

    private string GetRowPageLabel(string key) => key switch
    {
        "home" => L["FeedRowScopeHome"],
        "explore-feeds" => L["FeedRowScopeExploreFeeds"],
        _ => key
    };

    private bool IsRowPageChecked(string key) => key switch
    {
        "home" => Model.ShowRowOnHome,
        "explore-feeds" => Model.ShowRowOnExploreFeeds,
        _ => false
    };

    private Task OnRowPageChecked((string Item, bool Checked) value)
    {
        switch (value.Item)
        {
            case "home":
                Model.ShowRowOnHome = value.Checked;
                break;
            case "explore-feeds":
                Model.ShowRowOnExploreFeeds = value.Checked;
                break;
        }

        return NotifyAsync();
    }

    private string GetBarPageLabel(string key) => key switch
    {
        "home" => L["ScopeHome"],
        "explore" => L["ScopeExplore"],
        "myspace" => L["ScopeMySpace"],
        "settings" => L["ScopeSettings"],
        "media" => L["ScopeMedia"],
        _ => key
    };

    private bool IsBarPageChecked(string key) => key switch
    {
        "home" => Model.ShowBarOnHome,
        "explore" => Model.ShowBarOnExplore,
        "myspace" => Model.ShowBarOnMySpace,
        "settings" => Model.ShowBarOnSettings,
        "media" => Model.ShowBarOnMedia,
        _ => false
    };

    private Task OnBarPageChecked((string Item, bool Checked) value)
    {
        switch (value.Item)
        {
            case "home":
                Model.ShowBarOnHome = value.Checked;
                break;
            case "explore":
                Model.ShowBarOnExplore = value.Checked;
                break;
            case "myspace":
                Model.ShowBarOnMySpace = value.Checked;
                break;
            case "settings":
                Model.ShowBarOnSettings = value.Checked;
                break;
            case "media":
                Model.ShowBarOnMedia = value.Checked;
                break;
        }

        return NotifyAsync();
    }

    private Task DeleteItem(CustomNavItemEditModel item)
    {
        Model.Items.Remove(item);
        return NotifyAsync();
    }

    private async Task AddItemAsync()
    {
        var model = await EditItemDialogAsync(null);
        if (model is null)
            return;

        Model.Items.Add(model);
        await NotifyAsync();
    }

    private async Task EditItemAsync(CustomNavItemEditModel item)
    {
        var updated = await EditItemDialogAsync(item);
        if (updated is null)
            return;

        var index = Model.Items.IndexOf(item);
        if (index < 0)
            return;

        Model.Items[index] = updated;
        await NotifyAsync();
    }

    private async Task<CustomNavItemEditModel?> EditItemDialogAsync(CustomNavItemEditModel? initial)
    {
        var parameters = new K7DialogParameters<CustomNavItemDialog>();
        parameters.Add(d => d.Groups, Groups);
        parameters.Add(d => d.Collections, Collections);
        parameters.Add(d => d.Playlists, Playlists);
        if (initial is not null)
            parameters.Add(d => d.InitialModel, initial);

        var dialog = await DialogService.ShowAsync<CustomNavItemDialog>(
            initial is null ? L["AddItem"] : L["EditItem"],
            parameters);
        var result = await dialog.Result;
        return result.Canceled ? null : result.Data as CustomNavItemEditModel;
    }

    private string GetItemTitle(CustomNavItemEditModel item) =>
        CustomNavHrefHelper.GetTitle(item.ToDto(), Groups, key => NavL[key], Collections, Playlists);

    private string GetKindLabel(CustomNavItemKind kind) => kind switch
    {
        CustomNavItemKind.LibraryGroup => L["KindLibraryGroup"],
        CustomNavItemKind.LibraryBrowse => L["KindLibraryBrowse"],
        CustomNavItemKind.AppRoute => L["KindAppRoute"],
        CustomNavItemKind.AdminRoute => L["KindAdminRoute"],
        CustomNavItemKind.Collection => L["KindCollection"],
        CustomNavItemKind.Playlist => L["KindPlaylist"],
        CustomNavItemKind.DynamicPlaylist => L["KindPlaylist"],
        _ => kind.ToString()
    };
}
