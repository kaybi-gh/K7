using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Extensions;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminFederationPanel : IDisposable
{
    [Inject] private IFederationService FederationService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    private bool _isLoading = true;
    private bool _federationEnabled;
    private bool _invitationsEnabled;
    private Guid? _testingPeerId;
    private List<PeerServerDto>? _peers;
    private List<PeerRequestDto>? _requests;

    private List<PeerServerDto> _outboundPeers => _peers?.Where(p => !p.IsProvider).ToList() ?? [];
    private List<PeerServerDto> _inboundPeers => _peers?.Where(p => p.IsProvider).ToList() ?? [];

    protected override async Task OnInitializedAsync()
    {
        var flags = await ServerPreferencesService.GetServerFeatureFlagsAsync();
        _federationEnabled = flags?.FederationEnabled ?? false;
        _invitationsEnabled = flags?.FederationInvitationsEnabled ?? true;

        if (_federationEnabled)
        {
            await LoadData();
            await HubClient.JoinAdminFederationGroupAsync();
            HubClient.PeerStateChanged += OnPeerStateChanged;
            HubClient.PeerRequestReceived += OnPeerRequestReceived;
            HubClient.PeerTestResultReceived += OnPeerTestResultReceived;
        }
        else
        {
            _isLoading = false;
        }
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _peers = await FederationService.GetPeerServersAsync();
            _requests = await FederationService.GetPeerRequestsAsync();
        }
        catch
        {
            Snackbar.Add(L["LoadError"], K7Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OpenRequestDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<RequestPeerDialog>(L["AddPeerTitle"], null, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string remoteUrl)
            return;

        try
        {
            await FederationService.RequestPeerAsync(remoteUrl);
            Snackbar.Add(L["RequestSent"], K7Severity.Success);
            await LoadData();
        }
        catch
        {
            Snackbar.Add(L["RequestError"], K7Severity.Error);
        }
    }

    private async Task AcceptRequest(PeerRequestDto request)
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AcceptPeerRequestDialog>(L["AcceptTitle"], null, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not AcceptPeerResult acceptResult)
            return;

        try
        {
            await FederationService.AcceptPeerAsync(request.Id, acceptResult.LibraryIds, acceptResult.AutoShareNewLibraries);
            Snackbar.Add(L["PeerAccepted"], K7Severity.Success);
            await LoadData();
        }
        catch
        {
            Snackbar.Add(L["AcceptError"], K7Severity.Error);
        }
    }

    private async Task RejectRequest(PeerRequestDto request)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["RejectTitle"],
            string.Format(L["RejectMessage"], request.RequesterName),
            yesText: L["RejectConfirm"],
            cancelText: L["Cancel"]);

        if (confirmed is not true)
            return;

        try
        {
            await FederationService.RejectPeerAsync(request.Id);
            Snackbar.Add(L["PeerRejected"], K7Severity.Success);
            await LoadData();
        }
        catch
        {
            Snackbar.Add(L["RejectError"], K7Severity.Error);
        }
    }

    private async Task EditPeer(PeerServerDto peer)
    {
        var parameters = new K7DialogParameters { ["Peer"] = peer };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<PeerSettingsDialog>(L["SettingsTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not UpdatePeerRequest request)
            return;

        try
        {
            await FederationService.UpdatePeerAsync(peer.Id, request);
            Snackbar.Add(L["PeerUpdated"], K7Severity.Success);
            await LoadData();
        }
        catch
        {
            Snackbar.Add(L["UpdateError"], K7Severity.Error);
        }
    }

    private async Task TestPeer(PeerServerDto peer)
    {
        _testingPeerId = peer.Id;
        try
        {
            var reachable = await FederationService.TestPeerAsync(peer.Id);
            Snackbar.Add(
                reachable ? L["TestSuccess"] : L["TestFailed"],
                reachable ? K7Severity.Success : K7Severity.Warning);
            await LoadData();
        }
        catch
        {
            Snackbar.Add(L["TestFailed"], K7Severity.Error);
        }
        finally
        {
            _testingPeerId = null;
        }
    }

    private async Task SyncPeer(PeerServerDto peer)
    {
        try
        {
            await FederationService.SyncPeerAsync(peer.Id);
            Snackbar.Add(L["SyncStarted"], K7Severity.Success);
        }
        catch
        {
            Snackbar.Add(L["SyncError"], K7Severity.Error);
        }
    }

    private async Task DeletePeer(PeerServerDto peer)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteTitle"],
            string.Format(L["DeleteMessage"], peer.BaseUrl),
            yesText: L["DeleteConfirm"],
            cancelText: L["Cancel"]);

        if (confirmed is not true)
            return;

        try
        {
            await FederationService.RevokePeerAsync(peer.Id);
            Snackbar.Add(L["PeerDeleted"], K7Severity.Success);
            await LoadData();
        }
        catch
        {
            Snackbar.Add(L["DeleteError"], K7Severity.Error);
        }
    }

    private async Task RevokePeer(PeerServerDto peer)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["RevokeTitle"],
            string.Format(L["RevokeMessage"], peer.BaseUrl),
            yesText: L["RevokeConfirm"],
            cancelText: L["Cancel"]);

        if (confirmed is not true)
            return;

        try
        {
            await FederationService.RevokePeerAsync(peer.Id);
            Snackbar.Add(L["PeerRevoked"], K7Severity.Success);
            await LoadData();
        }
        catch
        {
            Snackbar.Add(L["RevokeError"], K7Severity.Error);
        }
    }

    private static string GetStatusColor(PeerStatus status) => status switch
    {
        PeerStatus.Active => "success",
        PeerStatus.Pending => "warning",
        PeerStatus.Revoked => "danger",
        PeerStatus.Rejected => "danger",
        _ => "default"
    };

    private string GetStatusLabel(PeerStatus status) => status switch
    {
        PeerStatus.Active => L["StatusActive"],
        PeerStatus.Pending => L["StatusPending"],
        PeerStatus.Revoked => L["StatusRevoked"],
        PeerStatus.Rejected => L["StatusRejected"],
        _ => L["StatusUnknown"]
    };

    private void OnPeerStateChanged(Guid peerId, int newStatus)
    {
        InvokeAsync(async () =>
        {
            await LoadData();
            StateHasChanged();
        });
    }

    private void OnPeerRequestReceived(PeerRequestDto request)
    {
        InvokeAsync(async () =>
        {
            await LoadData();
            StateHasChanged();
        });
    }

    private void OnPeerTestResultReceived(Guid peerId, bool reachable)
    {
        InvokeAsync(async () =>
        {
            await LoadData();
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        HubClient.PeerStateChanged -= OnPeerStateChanged;
        HubClient.PeerRequestReceived -= OnPeerRequestReceived;
        HubClient.PeerTestResultReceived -= OnPeerTestResultReceived;
        _ = HubClient.LeaveAdminFederationGroupAsync();
    }
}
