using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SharedProfileHostSettingsDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Parameter] public SharedProfileDto Group { get; set; } = default!;

    [Inject] private ISharedProfileService SharedProfileService { get; set; } = default!;
    [Inject] private IPlaylistService PlaylistService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _loading = true;
    private bool _saving;
    private VideoPlaybackPolicySettingsDto _videoPolicy = new();
    private AudioPlaybackPolicySettingsDto _audioPolicy = new();
    private Guid? _restrictionProfileId;
    private List<ContentRestrictionProfileDto> _restrictionProfiles = [];
    private List<LitePlaylistDto> _playlists = [];
    private HashSet<Guid> _sharedPlaylistIds = [];
    private HashSet<Guid> _initialSharedPlaylistIds = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _restrictionProfileId = Group.ContentRestrictionProfileId;
            _videoPolicy = await SharedProfileService.GetVideoPlaybackPolicyAsync(Group.Id);
            _audioPolicy = await SharedProfileService.GetAudioPlaybackPolicyAsync(Group.Id);
            _restrictionProfiles = await UserAdminService.GetContentRestrictionProfilesAsync();
            var playlistPage = await PlaylistService.GetPlaylistsAsync(pageNumber: 1, pageSize: 100);
            _playlists = playlistPage?.Items?.ToList() ?? [];
            var sharedIds = await SharedProfileService.GetPlaylistIdsAsync(Group.Id);
            _sharedPlaylistIds = sharedIds.ToHashSet();
            _initialSharedPlaylistIds = sharedIds.ToHashSet();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnPlaylistShareChanged(Guid playlistId, bool share)
    {
        if (share)
            _sharedPlaylistIds.Add(playlistId);
        else
            _sharedPlaylistIds.Remove(playlistId);
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SaveAsync()
    {
        _saving = true;
        try
        {
            await SharedProfileService.UpdateVideoPlaybackPolicyAsync(Group.Id, _videoPolicy);
            await SharedProfileService.UpdateAudioPlaybackPolicyAsync(Group.Id, _audioPolicy);
            await SharedProfileService.AssignContentRestrictionAsync(Group.Id, _restrictionProfileId);

            foreach (var id in _sharedPlaylistIds.Except(_initialSharedPlaylistIds))
                await SharedProfileService.SharePlaylistAsync(Group.Id, id);

            foreach (var id in _initialSharedPlaylistIds.Except(_sharedPlaylistIds))
                await SharedProfileService.UnsharePlaylistAsync(Group.Id, id);

            Dialog.Close(K7DialogResult.Ok(true));
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
