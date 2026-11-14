using K7.Server.Domain.Enums;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Dialogs;

/// <summary>
/// Shares a custom theme as a small JSON document: shows it to copy or download, or reads
/// one that was pasted or picked from a file.
/// </summary>
public partial class ThemeFileDialog
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    /// <summary>True reads a theme, false shows <see cref="Json"/>.</summary>
    [Parameter] public bool Import { get; set; }
    [Parameter] public string Json { get; set; } = "";
    [Parameter] public string FileName { get; set; } = "k7-theme.json";

    private readonly string _fileInputId = $"theme-file-{Guid.NewGuid():N}";
    private string? _json;
    private string? _error;

    /// <summary>Blob downloads only work in a browser, not in the app's web view.</summary>
    private bool CanDownload => DeviceService.GetClientType() == ClientType.Web;

    private void Cancel() => Dialog.Cancel();

    private async Task ChooseFileAsync() => await JS.InvokeVoidAsync("K7.clickById", _fileInputId);

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        try
        {
            await using var stream = e.File.OpenReadStream(ThemeFile.MaxLength);
            using var reader = new StreamReader(stream);
            _json = await reader.ReadToEndAsync();
            ImportTheme();
        }
        catch (IOException)
        {
            _error = L[$"Error_{ThemeFileError.TooLarge}"];
        }
    }

    private void ImportTheme()
    {
        var error = ThemeFile.TryParse(_json, out var theme);
        if (error != ThemeFileError.None || theme is null)
        {
            _error = L[$"Error_{error}"];
            return;
        }

        if (ThemePaletteValidator.Validate(theme.Palette).Count > 0)
        {
            _error = L["Error_Contrast"];
            return;
        }

        Dialog.Close(K7DialogResult.Ok(theme));
    }

    private async Task CopyAsync()
    {
        var copied = false;
        try
        {
            copied = await JS.InvokeAsync<bool>("K7.copyText", Json);
        }
        catch (JSException)
        {
        }

        Snackbar.Add(copied ? L["Copied"] : L["CopyFailed"], copied ? K7Severity.Success : K7Severity.Error);
    }

    private async Task DownloadAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("K7.downloadTextFile", FileName, Json, "application/json");
        }
        catch (JSException)
        {
            Snackbar.Add(L["CopyFailed"], K7Severity.Error);
        }
    }
}
