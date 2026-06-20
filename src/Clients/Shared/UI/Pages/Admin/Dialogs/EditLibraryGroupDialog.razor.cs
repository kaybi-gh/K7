using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class EditLibraryGroupDialog
{
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public required LibraryGroupDto Group { get; set; }

    private string? _title;
    private string? _description;
    private string? _icon;
    private string _cardColor = "#781e1e";
    private CoverPickerResult? _pendingCover;
    private Guid? _currentCoverPictureId;
    private bool _removeCover;
    private bool _isSubmitting;
    private (string GradientStart, string GradientEnd, string IconColor) _previewColors;

    private string PreviewIcon => !string.IsNullOrEmpty(_icon)
        ? _icon
        : GetDefaultIcon(Group.MediaType);

    private string? PreviewImageUrl
    {
        get
        {
            if (_removeCover && _pendingCover is null)
                return null;
            if (_pendingCover?.SourcePictureId.HasValue == true)
                return $"/api/metadata-pictures/{_pendingCover.SourcePictureId}?size=Medium";
            if (_currentCoverPictureId.HasValue)
                return $"/api/metadata-pictures/{_currentCoverPictureId}?size=Medium";
            return null;
        }
    }

    private string? PreviewDominantColor =>
        PreviewImageUrl is not null && !_removeCover ? Group.CoverDominantColor : null;

    protected override void OnInitialized()
    {
        _title = Group.Title;
        _description = Group.Description;
        _icon = Group.Icon;
        _currentCoverPictureId = Group.CoverPictureId;
        _cardColor = Group.CardColor ?? LibraryGroupCardColors.GetDefaultHex(Group.MediaType);
        _previewColors = LibraryGroupCardColors.GetRgbaColors(Group.MediaType, Group.CardColor);
    }

    private async Task OnCardColorChangedAsync(string value)
    {
        _cardColor = value;
        _previewColors = LibraryGroupCardColors.ToRgba(value);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenIconPickerAsync()
    {
        var parameters = new K7DialogParameters<K7IconPickerDialog>();
        parameters.Add(x => x.InitialValue, _icon);
        parameters.Add(x => x.SearchPlaceholder, L["IconSearch"].Value);
        parameters.Add(x => x.CancelText, S["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, S["Confirm"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7IconPickerDialog>(L["IconLabel"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
            _icon = result.Data as string;
    }

    private async Task OpenCoverPickerAsync()
    {
        var pictures = new List<LibraryPictureDto>();
        foreach (var libraryId in Group.LibraryIds)
        {
            var libraryPictures = await LibraryService.GetLibraryPicturesAsync(libraryId);
            pictures.AddRange(libraryPictures);
        }

        var parameters = new K7DialogParameters<K7CoverPickerDialog>();
        parameters.Add(x => x.Pictures, pictures);
        parameters.Add(x => x.CancelText, S["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, S["Confirm"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7CoverPickerDialog>(L["CoverLabel"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: CoverPickerResult cover })
        {
            _pendingCover = cover;
            _removeCover = false;
        }
    }

    private void RemoveCover()
    {
        _pendingCover = null;
        _currentCoverPictureId = null;
        _removeCover = true;
    }

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        _isSubmitting = true;
        try
        {
            var defaultColor = LibraryGroupCardColors.GetDefaultHex(Group.MediaType);
            var cardColor = string.Equals(_cardColor, defaultColor, StringComparison.OrdinalIgnoreCase)
                ? null
                : _cardColor;

            await LibraryService.UpdateLibraryGroupAsync(Group.Id, new UpdateLibraryGroupRequest
            {
                Title = _title,
                Description = _description,
                Icon = _icon,
                CardColor = cardColor ?? string.Empty
            });

            if (_pendingCover?.SourcePictureId.HasValue == true)
            {
                await LibraryService.SetLibraryGroupCoverFromPictureAsync(Group.Id, _pendingCover.SourcePictureId.Value);
            }
            else if (_pendingCover?.File is not null)
            {
                await using var stream = _pendingCover.File.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                await LibraryService.UploadLibraryGroupCoverAsync(Group.Id, stream, _pendingCover.File.Name);
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private static string GetDefaultIcon(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => "film-strip",
        LibraryMediaType.Serie => "television",
        LibraryMediaType.Music => "music-notes",
        _ => "folder"
    };
}
