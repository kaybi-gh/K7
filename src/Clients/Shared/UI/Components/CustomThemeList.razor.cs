using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// The themes somebody made: a user's own themes, or the themes of the server in the admin.
/// Lists them with edit, export and delete, and adds new ones from the editor or a theme file.
/// The host stores the changes it is told about.
/// </summary>
public partial class CustomThemeList
{
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Parameter, EditorRequired] public IReadOnlyList<CustomThemeDto> Themes { get; set; } = [];

    /// <summary>True for the themes of the server: they get a server id and their own delete warning.</summary>
    [Parameter] public bool Server { get; set; }

    [Parameter] public int MaxCount { get; set; } = AppearancePreferencesDto.MaxCustomThemes;

    [Parameter] public bool Disabled { get; set; }

    /// <summary>A theme was created or imported. It already carries its final id.</summary>
    [Parameter] public EventCallback<CustomThemeDto> OnAdded { get; set; }

    /// <summary>A theme was edited. Same id, new name or colors.</summary>
    [Parameter] public EventCallback<CustomThemeDto> OnEdited { get; set; }

    /// <summary>The user confirmed deleting a theme.</summary>
    [Parameter] public EventCallback<CustomThemeDto> OnDeleted { get; set; }

    private bool AddDisabled => Disabled || Themes.Count >= MaxCount;

    private static K7DialogOptions DialogOptions =>
        new() { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };

    private async Task CreateAsync()
    {
        if (await ShowEditorAsync(null, L["CreateTheme"]) is { } theme)
            await AddAsync(theme);
    }

    private async Task ImportAsync()
    {
        var parameters = new K7DialogParameters<ThemeFileDialog>
        {
            { x => x.Import, true }
        };
        var dialog = await DialogService.ShowAsync<ThemeFileDialog>(L["ImportTheme"], parameters, DialogOptions);
        if (await dialog.Result is { Canceled: false, Data: CustomThemeDto theme })
            await AddAsync(theme);
    }

    private async Task AddAsync(CustomThemeDto theme)
    {
        if (AddDisabled)
            return;

        await OnAdded.InvokeAsync(Server ? theme with { Id = ServerThemes.NewId() } : theme);
    }

    private async Task EditAsync(CustomThemeDto theme)
    {
        if (await ShowEditorAsync(theme, theme.Name) is { } edited)
            await OnEdited.InvokeAsync(edited);
    }

    private async Task ExportAsync(CustomThemeDto theme)
    {
        var parameters = new K7DialogParameters<ThemeFileDialog>
        {
            { x => x.Json, ThemeFile.Serialize(theme) },
            { x => x.FileName, $"{theme.Id}.json" }
        };
        await DialogService.ShowAsync<ThemeFileDialog>(string.Format(L["ExportTheme"], theme.Name), parameters, DialogOptions);
    }

    private async Task DeleteAsync(CustomThemeDto theme)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteThemeTitle"],
            string.Format(L[Server ? "DeleteServerThemeMessage" : "DeleteThemeMessage"], theme.Name),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);
        if (confirmed is true)
            await OnDeleted.InvokeAsync(theme);
    }

    private async Task<CustomThemeDto?> ShowEditorAsync(CustomThemeDto? theme, string title)
    {
        var parameters = new K7DialogParameters<ThemeEditorDialog>
        {
            { x => x.Theme, theme }
        };
        var dialog = await DialogService.ShowAsync<ThemeEditorDialog>(title, parameters, DialogOptions);
        return await dialog.Result is { Canceled: false, Data: CustomThemeDto edited } ? edited : null;
    }
}
