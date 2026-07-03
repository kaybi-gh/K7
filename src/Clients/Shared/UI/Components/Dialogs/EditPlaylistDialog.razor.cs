using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditPlaylistDialog
{
    [Inject] private IPlaylistService K7ServerService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid PlaylistId { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? Description { get; set; }
    [Parameter] public MediaType MediaType { get; set; } = MediaType.MusicTrack;
    [Parameter] public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Nobody;
    [Parameter] public Guid? CoverPictureId { get; set; }

    private VisibilityScope _visibilityScope = VisibilityScope.Nobody;

    private CoverPickerResult? _pendingCover;
    private Guid? _currentCoverPictureId;
    private bool _removeCover;
    private bool _isSubmitting;
    private List<LibraryPictureDto> _itemPictures = [];
    private IReadOnlyList<string> _itemPreviewUrls = [];

    private bool UsesMosaicPreview =>
        _removeCover || (!_currentCoverPictureId.HasValue && _pendingCover is null);

    private bool CanResetToMosaic =>
        (_currentCoverPictureId.HasValue && !_removeCover) || _pendingCover is not null;

    private IReadOnlyList<string> PreviewMosaicUrls
    {
        get
        {
            if (UsesMosaicPreview)
                return _itemPreviewUrls;

            if (_pendingCover?.SourcePictureId is Guid pendingId)
                return [$"/api/metadata-pictures/{pendingId}?size=Medium"];

            if (_currentCoverPictureId is Guid currentId)
                return [$"/api/metadata-pictures/{currentId}?size=Medium"];

            return _itemPreviewUrls;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _visibilityScope = VisibilityScope;
        _currentCoverPictureId = CoverPictureId;
        await LoadItemPicturesAsync();
    }

    private async Task LoadItemPicturesAsync()
    {
        var page = await K7ServerService.GetPlaylistItemsAsync(PlaylistId, pageSize: 100);
        var seen = new HashSet<Guid>();

        foreach (var item in page?.Items ?? [])
        {
            foreach (var picture in item.Pictures ?? [])
            {
                if (seen.Add(picture.Id))
                {
                    _itemPictures.Add(new LibraryPictureDto
                    {
                        Id = picture.Id,
                        Type = picture.Type,
                        DominantColor = picture.DominantColor
                    });
                }
            }
        }

        _itemPreviewUrls = _itemPictures
            .Take(4)
            .Select(p => $"/api/metadata-pictures/{p.Id}?size=Medium")
            .ToList();
    }

    private async Task OpenCoverPickerAsync()
    {
        var parameters = new K7DialogParameters<K7CoverPickerDialog>();
        parameters.Add(x => x.Pictures, _itemPictures);
        parameters.Add(x => x.CancelText, S["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, S["Save"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7CoverPickerDialog>(L["CoverLabel"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: CoverPickerResult cover })
        {
            _pendingCover = cover;
            _removeCover = false;
        }
    }

    private void ResetCoverToMosaic()
    {
        _pendingCover = null;
        _currentCoverPictureId = null;
        _removeCover = true;
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) return;

        _isSubmitting = true;
        try
        {
            await K7ServerService.UpdatePlaylistAsync(PlaylistId, new UpdatePlaylistRequest
            {
                Title = Title.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                MediaType = MediaType,
                VisibilityScope = _visibilityScope
            });

            if (_removeCover)
                await K7ServerService.RemovePlaylistCoverAsync(PlaylistId);
            else if (_pendingCover?.SourcePictureId.HasValue == true)
                await K7ServerService.SetPlaylistCoverFromPictureAsync(PlaylistId, _pendingCover.SourcePictureId.Value);
            else if (_pendingCover?.File is not null)
            {
                await using var stream = _pendingCover.File.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                await K7ServerService.UploadPlaylistCoverAsync(PlaylistId, stream, _pendingCover.File.Name);
            }

            Snackbar.Add(L["Updated"], K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(true));
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
