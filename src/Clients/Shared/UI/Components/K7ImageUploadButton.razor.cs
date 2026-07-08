using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7ImageUploadButton
{
    private readonly string _inputId = $"k7-upload-{Guid.NewGuid():N}";

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Icon { get; set; } = Phosphor.Upload;
    [Parameter] public string Size { get; set; } = "sm";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Accept { get; set; } = "image/*";
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnFileSelected { get; set; }

    private async Task OpenFilePickerAsync()
    {
        if (Disabled)
            return;

        await JSRuntime.InvokeVoidAsync("eval", $"document.getElementById('{_inputId}')?.click()");
    }

    private async Task HandleFileSelectedAsync(InputFileChangeEventArgs args)
    {
        if (Disabled || !OnFileSelected.HasDelegate)
            return;

        await OnFileSelected.InvokeAsync(args);
    }
}
