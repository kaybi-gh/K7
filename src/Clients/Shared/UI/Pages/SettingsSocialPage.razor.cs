using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsSocialPage : IDisposable
{
    private sealed record SocialFormState(
        FederationPrivacySettingsDto Privacy,
        ReviewPreferencesDto ReviewPreferences);

    private static readonly FederationContentType[] ContentTypes =
    [
        FederationContentType.Reviews,
        FederationContentType.Collections,
        FederationContentType.Playlists,
        FederationContentType.DynamicPlaylists,
        FederationContentType.PlaybackHistory
    ];

    private bool _loading = true;
    private FederationPrivacySettingsDto _privacy = new();
    private ReviewPreferencesDto _reviewPreferences = new();
    private List<FederationVisibilityGrantDto> _shareGrants = [];
    private List<FederationVisibilityGrantDto> _viewGrants = [];
    private readonly SettingsFormTracker<SocialFormState> _formTracker = new();
    private readonly SettingsAutoSaveSession<SocialFormState> _autoSave;

    public SettingsSocialPage()
    {
        _autoSave = new SettingsAutoSaveSession<SocialFormState>(
            _formTracker,
            () => _loading ? null : GetFormState(),
            PersistAsync,
            work => InvokeAsync(work),
            StateHasChanged,
            ReportError);
    }

    public void Dispose() => _autoSave.Dispose();

    protected override void OnAfterRender(bool firstRender) => _autoSave.OnAfterRender();

    protected override async Task OnInitializedAsync()
    {
        _privacy = await ReviewService.GetFederationPrivacyAsync();
        _reviewPreferences = await ReviewService.GetReviewPreferencesAsync();
        _shareGrants = _privacy.Share.Grants.ToList();
        _viewGrants = _privacy.View.Grants.ToList();
        _privacy.Share.Grants = _shareGrants;
        _privacy.View.Grants = _viewGrants;
        CaptureFormState();
        _loading = false;
    }

    private SocialFormState GetFormState()
    {
        var privacy = _privacy with
        {
            Share = _privacy.Share with { Grants = _shareGrants.ToList() },
            View = _privacy.View with { Grants = _viewGrants.ToList() }
        };
        return new SocialFormState(privacy, _reviewPreferences);
    }

    private void CaptureFormState() => _formTracker.Capture(GetFormState());

    private void OnFormChanged() => StateHasChanged();

    private string GetContentLabel(FederationContentType contentType) => contentType switch
    {
        FederationContentType.Reviews => L["Reviews"],
        FederationContentType.Collections => L["Collections"],
        FederationContentType.Playlists => L["Playlists"],
        FederationContentType.DynamicPlaylists => L["DynamicPlaylists"],
        _ => L["PlaybackHistory"]
    };

    private VisibilityScope GetScope(FederationContentType contentType, bool share)
    {
        var side = share ? _privacy.Share : _privacy.View;
        return contentType switch
        {
            FederationContentType.Reviews => side.Reviews,
            FederationContentType.Collections => side.Collections,
            FederationContentType.Playlists => side.Playlists,
            FederationContentType.DynamicPlaylists => side.DynamicPlaylists,
            _ => side.PlaybackHistory
        };
    }

    private void SetShareScope(FederationContentType contentType, VisibilityScope scope, bool share)
    {
        if (share)
        {
            switch (contentType)
            {
                case FederationContentType.Reviews: _privacy.Share.Reviews = scope; break;
                case FederationContentType.Collections: _privacy.Share.Collections = scope; break;
                case FederationContentType.Playlists: _privacy.Share.Playlists = scope; break;
                case FederationContentType.DynamicPlaylists: _privacy.Share.DynamicPlaylists = scope; break;
                case FederationContentType.PlaybackHistory: _privacy.Share.PlaybackHistory = scope; break;
            }
        }
        else
        {
            switch (contentType)
            {
                case FederationContentType.Reviews: _privacy.View.Reviews = scope; break;
                case FederationContentType.Collections: _privacy.View.Collections = scope; break;
                case FederationContentType.Playlists: _privacy.View.Playlists = scope; break;
                case FederationContentType.DynamicPlaylists: _privacy.View.DynamicPlaylists = scope; break;
                case FederationContentType.PlaybackHistory: _privacy.View.PlaybackHistory = scope; break;
            }
        }

        OnFormChanged();
    }

    private void OnShareGrantsChanged(List<FederationVisibilityGrantDto> grants)
    {
        _shareGrants = grants;
        OnFormChanged();
    }

    private void OnViewGrantsChanged(List<FederationVisibilityGrantDto> grants)
    {
        _viewGrants = grants;
        OnFormChanged();
    }

    private async Task PersistAsync()
    {
        _privacy.Share.Grants = _shareGrants;
        _privacy.View.Grants = _viewGrants;
        await ReviewService.UpdateFederationPrivacyAsync(_privacy);
        await ReviewService.UpdateReviewPreferencesAsync(_reviewPreferences);
    }

    private void ReportError(Exception ex) =>
        Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
}
