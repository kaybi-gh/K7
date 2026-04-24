using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class K7CoverPickerDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public List<LibraryPictureDto> Pictures { get; set; } = [];
    [Parameter] public string FromMediaText { get; set; } = "Pick from library";
    [Parameter] public string UploadText { get; set; } = "Upload a file";
    [Parameter] public string ChooseFileText { get; set; } = "Choose image...";
    [Parameter] public string NoPicturesText { get; set; } = "No pictures available";
    [Parameter] public string CancelText { get; set; } = "Cancel";
    [Parameter] public string ConfirmText { get; set; } = "Select";

    private int _tab;
    private Guid? _selectedSourceId;
    private IBrowserFile? _file;
    private List<LibraryPictureDto> _pictures = [];

    protected override void OnParametersSet()
    {
        _pictures = Pictures;
    }

    private bool CanConfirm => _tab == 0 ? _selectedSourceId.HasValue : _file is not null;

    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        _file = e.File;
        _selectedSourceId = null;
    }

    private void Confirm()
    {
        var result = _tab == 0
            ? new CoverPickerResult { SourcePictureId = _selectedSourceId }
            : new CoverPickerResult { File = _file };
        Dialog.Close(K7DialogResult.Ok(result));
    }

    private void Cancel() => Dialog.Cancel();
}
