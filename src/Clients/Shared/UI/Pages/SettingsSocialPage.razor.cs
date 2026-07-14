using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsSocialPage
{
    private sealed record SocialFormState(
        FederationPrivacySettingsDto Privacy,
        ReviewPreferencesDto ReviewPreferences);

    private bool _loading = true;
    private bool _saving;
    private FederationPrivacySettingsDto _privacy = new();
    private ReviewPreferencesDto _reviewPreferences = new();
    private List<FederationVisibilityGrantDto> _shareGrants = [];
    private List<FederationVisibilityGrantDto> _viewGrants = [];
    private readonly SettingsFormTracker<SocialFormState> _formTracker = new();

    private bool IsDirty => _formTracker.IsDirty(GetFormState());

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

    private void CancelChanges()
    {
        var state = _formTracker.Restore();
        _privacy = state.Privacy;
        _reviewPreferences = state.ReviewPreferences;
        _shareGrants = state.Privacy.Share.Grants.ToList();
        _viewGrants = state.Privacy.View.Grants.ToList();
        _privacy.Share.Grants = _shareGrants;
        _privacy.View.Grants = _viewGrants;
    }

    private void OnFormChanged() => StateHasChanged();

    private void SetShareScope(FederationContentType contentType, VisibilityScope scope, bool share)
    {
        if (share)
        {
            switch (contentType)
            {
                case FederationContentType.Reviews: _privacy.Share.Reviews = scope; break;
                case FederationContentType.Collections: _privacy.Share.Collections = scope; break;
                case FederationContentType.Playlists: _privacy.Share.Playlists = scope; break;
                case FederationContentType.SmartPlaylists: _privacy.Share.SmartPlaylists = scope; break;
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
                case FederationContentType.SmartPlaylists: _privacy.View.SmartPlaylists = scope; break;
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

    private async Task SaveAsync()
    {
        if (_saving)
            return;

        _saving = true;
        try
        {
            _privacy.Share.Grants = _shareGrants;
            _privacy.View.Grants = _viewGrants;
            await ReviewService.UpdateFederationPrivacyAsync(_privacy);
            await ReviewService.UpdateReviewPreferencesAsync(_reviewPreferences);
            CaptureFormState();
            Snackbar.Add(L["SaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }
}
