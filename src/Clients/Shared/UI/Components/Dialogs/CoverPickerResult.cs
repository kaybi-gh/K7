using Microsoft.AspNetCore.Components.Forms;

namespace K7.Clients.Shared.UI.Components.Dialogs;

/// <summary>Result returned by K7CoverPickerDialog.</summary>
public sealed class CoverPickerResult
{
    public Guid? SourcePictureId { get; init; }
    public IBrowserFile? File { get; init; }
}
