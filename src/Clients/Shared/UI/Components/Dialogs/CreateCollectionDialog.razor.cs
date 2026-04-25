using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class CreateCollectionDialog
{
    [Inject] private ICollectionService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private string _title = "";
    private string? _description;
    private MediaType? _mediaType;
    private bool _isPublic;
    private bool _isSubmitting;

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_title)) return;

        _isSubmitting = true;
        try
        {
            var id = await K7ServerService.CreateCollectionAsync(new CreateCollectionRequest
            {
                Title = _title.Trim(),
                Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                IsPublic = _isPublic,
                MediaType = _mediaType
            });

            Snackbar.Add(L["Created"], K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(id));
        }
        catch
        {
            Snackbar.Add(L["Error"], K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
