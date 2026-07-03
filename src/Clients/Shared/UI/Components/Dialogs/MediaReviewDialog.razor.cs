using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class MediaReviewDialog
{
    [Inject] private IReviewService ReviewService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid MediaId { get; set; }
    [Parameter] public string? MediaTitle { get; set; }

    private bool _loading = true;
    private bool _isSubmitting;
    private bool _hasReview;
    private int? _rating;
    private string _text = string.Empty;
    private string? _emoji;

    protected override async Task OnInitializedAsync()
    {
        var state = await ReviewService.GetMyMediaReviewAsync(MediaId);
        if (state is not null)
        {
            _rating = state.Rating;
            _hasReview = state.HasReview;
            _text = state.Text ?? string.Empty;
            _emoji = state.Emoji;
        }

        _loading = false;
    }

    private Task OnRatingChanged(int? value)
    {
        _rating = value;
        return Task.CompletedTask;
    }

    private void Cancel() => Dialog.Cancel();

    private async Task OpenEmojiPickerAsync()
    {
        var parameters = new K7DialogParameters<K7EmojiPickerDialog>();
        parameters.Add(p => p.InitialEmoji, _emoji);
        parameters.Add(p => p.CancelText, S["Cancel"]);
        parameters.Add(p => p.ConfirmText, S["Confirm"]);

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<K7EmojiPickerDialog>(L["EmojiOptional"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
            _emoji = result.Data as string;
    }

    private void ClearEmoji() => _emoji = null;

    private async Task SaveAsync()
    {
        if (_rating is null or <= 0)
        {
            Snackbar.Add(L["RatingRequired"], K7Severity.Warning);
            return;
        }

        _isSubmitting = true;
        try
        {
            await ReviewService.UpsertMediaReviewAsync(MediaId, new UpsertMediaReviewRequest
            {
                Rating = _rating.Value,
                Text = _text.Trim(),
                Emoji = _emoji
            });

            Snackbar.Add(L["Saved"], K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch
        {
            Snackbar.Add(L["SaveError"], K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task DeleteAsync()
    {
        _isSubmitting = true;
        try
        {
            await ReviewService.DeleteMediaReviewAsync(MediaId);
            Snackbar.Add(L["Deleted"], K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch
        {
            Snackbar.Add(L["DeleteError"], K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
