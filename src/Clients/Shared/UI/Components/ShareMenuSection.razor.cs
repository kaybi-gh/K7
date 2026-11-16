using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// "Share" entry of a media menu. It opens the dialog that creates a public link to the
/// media and everything below it. Hidden unless public shares are on and the user holds
/// the share capability.
/// </summary>
public partial class ShareMenuSection
{
    [Inject] private IMediaSharingAccess SharingAccess { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Parameter, EditorRequired] public Guid MediaId { get; set; }

    /// <summary>Type of the media, when the caller knows it. Decides which options the dialog offers.</summary>
    [Parameter] public MediaType? MediaType { get; set; }

    /// <summary>Shown as the dialog title.</summary>
    [Parameter] public string? MediaTitle { get; set; }

    private bool _canShare;

    protected override async Task OnInitializedAsync() =>
        _canShare = (await SharingAccess.GetAsync()).CanCreate;

    private async Task OpenShareDialogAsync()
    {
        var parameters = new K7DialogParameters<ShareMediaDialog>
        {
            { x => x.MediaId, MediaId },
            { x => x.MediaType, MediaType ?? default }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var title = string.IsNullOrWhiteSpace(MediaTitle)
            ? L["DialogTitle"].Value
            : string.Format(L["DialogTitleFor"], MediaTitle);

        await DialogService.ShowAsync<ShareMediaDialog>(title, parameters, options);
    }
}
