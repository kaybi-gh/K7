using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SyncPlayDialog : IDisposable
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private View _view = View.Main;
    private Guid _currentDeviceId;
    private bool _isCreating;

    private enum View { Main, Members }

    protected override void OnInitialized()
    {
        var storedId = DeviceStorage.Get(PreferenceKeys.DEVICE_ID);
        if (!string.IsNullOrEmpty(storedId) && Guid.TryParse(storedId, out var parsed))
            _currentDeviceId = parsed;

        SyncPlay.GroupUpdated += OnGroupUpdated;
        SyncPlay.ErrorReceived += OnSyncPlayError;
        UpdateHeaderActions();
    }

    private void OnSyncPlayError(string errorCode)
    {
        InvokeAsync(() =>
        {
            _isCreating = false;

            var message = errorCode switch
            {
                "device_not_registered" => L["SyncPlayDeviceNotRegistered"],
                "hub_not_connected" => L["SyncPlayHubNotConnected"],
                _ => null
            };

            if (message is not null)
                Snackbar.Add(message, K7Severity.Warning);

            StateHasChanged();
        });
    }

    private void OnGroupUpdated()
    {
        InvokeAsync(() =>
        {
            _isCreating = false;
            UpdateHeaderActions();
            StateHasChanged();
        });
    }

    private void UpdateHeaderActions()
    {
        if (!SyncPlay.IsInGroup)
        {
            Dialog.SetHeaderActions(null);
            return;
        }

        Dialog.SetHeaderActions(BuildHeaderActions);
    }

    private void BuildHeaderActions(RenderTreeBuilder builder)
    {
        var seq = 0;
        var hasMedia = SyncPlay.CurrentGroup?.CurrentMedia is not null;

        builder.OpenElement(seq++, "div");
        builder.AddAttribute(seq++, "class", "d-flex align-center gap-1");

        // Rejoin (only shown when media is playing)
        if (hasMedia)
        {
            builder.OpenComponent<K7IconButton>(seq++);
            builder.AddAttribute(seq++, "Icon", Phosphor.Play);
            builder.AddAttribute(seq++, "Size", "sm");
            builder.AddAttribute(seq++, "AriaLabel", L["RejoinPlayback"]);
            builder.AddAttribute(seq++, "OnClick", EventCallback.Factory.Create(this, Rejoin));
            builder.CloseComponent();
        }

        // Chat/Status tab
        builder.OpenElement(seq++, "button");
        builder.AddAttribute(seq++, "class", $"syncplay-dialog-tab-btn focusable{(_view == View.Main ? " active" : "")}");
        builder.AddAttribute(seq++, "type", "button");
        builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => SetView(View.Main)));

        builder.OpenComponent<K7Icon>(seq++);
        builder.AddAttribute(seq++, "Icon", Phosphor.ChatCircle);
        builder.CloseComponent();

        builder.CloseElement(); // button

        // Members tab
        builder.OpenElement(seq++, "button");
        builder.AddAttribute(seq++, "class", $"syncplay-dialog-tab-btn focusable{(_view == View.Members ? " active" : "")}");
        builder.AddAttribute(seq++, "type", "button");
        builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => SetView(View.Members)));

        builder.OpenComponent<K7Icon>(seq++);
        builder.AddAttribute(seq++, "Icon", Phosphor.Users);
        builder.CloseComponent();

        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "syncplay-dialog-members-count");
        builder.AddContent(seq++, SyncPlay.CurrentGroup?.Participants.Count ?? 0);
        builder.CloseElement();

        builder.CloseElement(); // button

        // Leave
        builder.OpenComponent<K7IconButton>(seq++);
        builder.AddAttribute(seq++, "Icon", Phosphor.SignOut);
        builder.AddAttribute(seq++, "Size", "sm");
        builder.AddAttribute(seq++, "Color", "danger");
        builder.AddAttribute(seq++, "AriaLabel", L["LeaveGroup"]);
        builder.AddAttribute(seq++, "OnClick", EventCallback.Factory.Create(this, Leave));
        builder.CloseComponent();

        builder.CloseElement(); // div
    }

    private void SetView(View view)
    {
        _view = view;
        UpdateHeaderActions();
    }

    private void ToggleMembers()
    {
        _view = _view == View.Members ? View.Main : View.Members;
        UpdateHeaderActions();
    }

    private void Rejoin()
    {
        Dialog.Close();
        SyncPlay.RequestRejoin();
    }

    private async Task InviteUsers()
    {
        await DialogService.ShowAsync<SyncPlayInviteUsersDialog>(L["InviteUsers"]);
    }

    private async Task CopyLink()
    {
        await SyncPlay.GetInviteLinkAsync();
    }

    private async Task Leave()
    {
        Dialog.Close();
        await SyncPlay.LeaveGroupAsync();
    }

    private async Task Kick(Guid targetDeviceId)
    {
        await SyncPlay.KickAsync(targetDeviceId);
    }

    private async Task CreateGroup()
    {
        if (_isCreating)
            return;

        _isCreating = true;
        StateHasChanged();

        Guid? mediaReferenceId = null;
        string? mediaTitle = null;
        double mediaDuration = 0;
        string? mediaCoverUrl = null;
        double position = 0;
        var isPlaying = false;

        var videoSource = PlayerService.Source;
        if (videoSource?.MediaId is not null && PlayerService.IsVisible)
        {
            mediaReferenceId = videoSource.MediaId.Value;
            mediaTitle = videoSource.Title ?? "";
            mediaDuration = PlayerService.Duration;
            mediaCoverUrl = videoSource.CoverUrl;
            position = PlayerService.CurrentTime;
            isPlaying = PlayerService.PlaybackState == PlaybackState.Playing;
        }
        else if (Audio.CurrentTrack is { } track)
        {
            mediaReferenceId = track.MediaId;
            mediaTitle = track.Title;
            mediaDuration = Audio.Duration;
            mediaCoverUrl = track.CoverUrl;
            position = Audio.CurrentTime;
            isPlaying = Audio.PlaybackState == PlaybackState.Playing;
        }

        await SyncPlay.CreateGroupAsync(mediaReferenceId, mediaTitle, mediaDuration, mediaCoverUrl, position, isPlaying);
    }

    private string FormatState()
    {
        var group = SyncPlay.CurrentGroup;
        if (group is null) return "";
        return group.State switch
        {
            SyncPlayGroupState.Playing => L["Playing"],
            SyncPlayGroupState.Paused => L["Paused"],
            SyncPlayGroupState.WaitingForReady => L["Buffering"],
            _ => ""
        };
    }

    public void Dispose()
    {
        SyncPlay.GroupUpdated -= OnGroupUpdated;
        SyncPlay.ErrorReceived -= OnSyncPlayError;
    }
}
