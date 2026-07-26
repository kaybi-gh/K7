using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SharedProfileHostSettingsDialog
{
    private const long MaxAvatarSize = 2 * 1024 * 1024;
    private readonly string _avatarFileInputId = $"shared-profile-avatar-{Guid.NewGuid():N}";

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Parameter] public SharedProfileDto Group { get; set; } = default!;

    [Inject] private ISharedProfileService SharedProfileService { get; set; } = default!;
    [Inject] private IPlaylistService PlaylistService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool _loading = true;
    private bool _saving;
    private bool _avatarChanged;
    private string? _avatarUrl;
    private string? _avatarError;
    private VideoPlaybackPolicySettingsDto _videoPolicy = new();
    private AudioPlaybackPolicySettingsDto _audioPolicy = new();
    private Guid? _restrictionProfileId;
    private List<ContentRestrictionProfileDto> _restrictionProfiles = [];
    private List<LitePlaylistDto> _playlists = [];
    private HashSet<Guid> _sharedPlaylistIds = [];
    private HashSet<Guid> _initialSharedPlaylistIds = [];
    private List<K7AvatarGroupItem> _memberAvatarItems = [];

    private string _avatarLetter =>
        string.IsNullOrEmpty(Group.Name) ? "?" : Group.Name[..1].ToUpperInvariant();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _avatarUrl = Group.AvatarUrl;
            _memberAvatarItems = Group.Members
                .Select(m => new K7AvatarGroupItem
                {
                    UserId = m.UserId,
                    Image = m.AvatarUrl,
                    Letter = string.IsNullOrEmpty(m.DisplayName) ? "?" : m.DisplayName[..1].ToUpperInvariant()
                })
                .ToList();

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

    private async Task UploadAvatar()
    {
        _avatarError = null;
        await JSRuntime.InvokeVoidAsync("K7.clickById", _avatarFileInputId);
    }

    private async Task OnAvatarFileSelected(InputFileChangeEventArgs e)
    {
        _avatarError = null;
        var file = e.File;

        if (file.Size > MaxAvatarSize)
        {
            _avatarError = L["AvatarTooLarge"];
            return;
        }

        _saving = true;
        try
        {
            using var memoryStream = new MemoryStream();
            await using (var browserStream = file.OpenReadStream(MaxAvatarSize))
            {
                await browserStream.CopyToAsync(memoryStream);
            }

            memoryStream.Position = 0;
            await SharedProfileService.UploadAvatarAsync(Group.Id, memoryStream, file.Name);

            var groups = await SharedProfileService.GetSharedProfilesAsync();
            var refreshed = groups.FirstOrDefault(g => g.Id == Group.Id);
            _avatarUrl = refreshed?.AvatarUrl;
            _avatarChanged = true;
        }
        catch (Exception ex)
        {
            _avatarError = string.Format(S["ErrorWithDetails"], ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task RemoveAvatar()
    {
        _avatarError = null;
        _saving = true;
        try
        {
            await SharedProfileService.RemoveAvatarAsync(Group.Id);
            _avatarUrl = null;
            _avatarChanged = true;
        }
        catch (Exception ex)
        {
            _avatarError = string.Format(S["ErrorWithDetails"], ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel()
    {
        if (_avatarChanged)
            Dialog.Close(K7DialogResult.Ok(true));
        else
            Dialog.Cancel();
    }

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
