using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Home;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// The rows of a home page: order, visibility, and add, edit or delete a row. Used for the
/// home of one account and for the default home of the server. It edits <see cref="Rows"/> in
/// place and tells the host after each change, which decides when to save.
/// </summary>
public partial class HomeRowList
{
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IStringLocalizer<Home> HomeL { get; set; } = default!;
    [Inject] private IStringLocalizer<HomeRowDialog> DialogL { get; set; } = default!;

    [Parameter, EditorRequired] public List<HomeRowEditModel> Rows { get; set; } = [];

    [Parameter] public bool Disabled { get; set; }

    /// <summary>Rows were reordered, shown, hidden, added, edited or deleted.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    private List<LibraryDto> _libraries = [];
    private List<LibraryGroupDto> _libraryGroups = [];

    private bool AddDisabled => Disabled || Rows.Count >= HomeLayoutDto.MaxRows;

    protected override async Task OnInitializedAsync()
    {
        // The row dialog offers them as the scope of a row. Without them a row still covers
        // the whole catalog, so a failed load only hides the scope lists.
        try
        {
            var libraries = LibraryService.GetLibrariesAsync();
            var groups = LibraryService.GetLibraryGroupsAsync();
            _libraries = await libraries;
            _libraryGroups = await groups;
        }
        catch (Exception)
        {
            _libraries = [];
            _libraryGroups = [];
        }
    }

    private string GetRowTitle(string rowTitle) => HomeLayoutRowTitleHelper.Localize(HomeL, rowTitle);

    private Task OnReorderedAsync() => NotifyAsync();

    private Task SetVisibleAsync(HomeRowEditModel row, bool visible)
    {
        row.IsVisible = visible;
        return NotifyAsync();
    }

    /// <summary>The ready-made rows, with the ones already on this home marked as such.</summary>
    private List<HomeRowPresets.Preset> Presets => HomeRowPresets.All(Rows, _libraryGroups);

    private static string GetPresetIcon(HomeRowEditModel preset) =>
        preset.ContinueWatching ? Phosphor.PlayCircle
        : preset.LibraryGroupIds.Count > 0 ? Phosphor.Folders
        : Phosphor.Sparkle;

    private async Task AddPresetAsync(HomeRowEditModel preset)
    {
        if (AddDisabled)
            return;

        Rows.Add(preset);
        await NotifyAsync();
    }

    private async Task AddAsync()
    {
        if (AddDisabled || await ShowDialogAsync(L["AddRow"], null) is not { } added)
            return;

        Rows.Add(added);
        await NotifyAsync();
    }

    private async Task EditAsync(HomeRowEditModel row)
    {
        if (await ShowDialogAsync(L["EditRowTitle"], row) is not { } updated)
            return;

        var index = Rows.IndexOf(row);
        if (index < 0)
            return;

        Rows[index] = updated;
        await NotifyAsync();
    }

    private Task DeleteAsync(HomeRowEditModel row)
    {
        Rows.Remove(row);
        return NotifyAsync();
    }

    private async Task<HomeRowEditModel?> ShowDialogAsync(string title, HomeRowEditModel? row)
    {
        var parameters = new K7DialogParameters<HomeRowDialog>();
        parameters.Add(d => d.Libraries, _libraries);
        parameters.Add(d => d.LibraryGroups, _libraryGroups);
        if (row is not null)
            parameters.Add(d => d.InitialModel, row);

        // A home has one banner: the dialog does not offer it when another row already is.
        var banner = Rows.FirstOrDefault(r => r.DisplayType.IsBanner() && r.Id != row?.Id);
        if (banner is not null)
            parameters.Add(d => d.ExistingBannerTitle, GetRowTitle(banner.Title));

        var dialog = await DialogService.ShowAsync<HomeRowDialog>(title, parameters);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: HomeRowEditModel model } ? model : null;
    }

    private Task NotifyAsync()
    {
        // The banner is always the first row: dropped anywhere else, it goes back to the top.
        var banner = Rows.FirstOrDefault(r => r.DisplayType.IsBanner());
        if (banner is not null && Rows.IndexOf(banner) > 0)
        {
            Rows.Remove(banner);
            Rows.Insert(0, banner);
        }

        for (var i = 0; i < Rows.Count; i++)
            Rows[i].Order = i;

        return OnChanged.InvokeAsync();
    }
}
