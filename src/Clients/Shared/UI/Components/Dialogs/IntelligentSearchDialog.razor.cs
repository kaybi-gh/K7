using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class IntelligentSearchDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public IntelligentSearchKind? FixedKind { get; set; }
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }

    private IntelligentSearchKind _kind = IntelligentSearchKind.Sonic;
    private string _query = string.Empty;
    private bool _loading;

    protected override void OnParametersSet()
    {
        if (FixedKind is { } fixedKind)
            _kind = fixedKind;
    }

    private void OnKindChanged(IntelligentSearchKind kind) => _kind = kind;

    private string GetPlaceholder() =>
        _kind == IntelligentSearchKind.Sonic
            ? L["SonicPlaceholder"]
            : L["LyricsPlaceholder"];

    private string GetHelperText() =>
        _kind == IntelligentSearchKind.Sonic
            ? L["SonicHint"]
            : L["LyricsHint"];

    private void Cancel() => Dialog.Close(K7DialogResult.Cancel());

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_query))
            return;

        _loading = true;
        try
        {
            var request = new IntelligentSearchRequest(_kind, _query.Trim());
            var trackIds = await IntelligentSearchHelper.SearchTrackIdsAsync(MusicIntelligence, request);
            if (trackIds.Count == 0)
            {
                Snackbar.Add(L["NoResults"], K7Severity.Warning);
                return;
            }

            var tracks = await IntelligentSearchHelper.LoadScopedTracksAsync(
                MediaService, trackIds, LibraryIds, LibraryGroupIds);

            if (tracks.Count == 0)
            {
                Snackbar.Add(L["NoResults"], K7Severity.Warning);
                return;
            }

            var queueItems = IntelligentSearchHelper.ToQueueItems(tracks, S["Untitled"]);
            await Audio.PlayRadioAsync(queueItems, GetRadioTitle(request));
            Snackbar.Add(string.Format(L["Playing"], queueItems.Count), K7Severity.Info);
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch
        {
            Snackbar.Add(L["Error"], K7Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private string GetRadioTitle(IntelligentSearchRequest request) =>
        request.Kind == IntelligentSearchKind.Sonic
            ? string.Format(L["RadioTitleSonic"], request.Query)
            : string.Format(L["RadioTitleLyrics"], request.Query);
}
