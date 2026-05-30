using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Extensions;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminFederationPanel
{
    [Inject] private IFederationService FederationService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private List<PeerServerDto>? _peers;

    protected override async Task OnInitializedAsync()
    {
        await LoadPeers();
    }

    private async Task LoadPeers()
    {
        _isLoading = true;
        try
        {
            _peers = await FederationService.GetPeerServersAsync();
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
            await LoadPeers();
        }
        catch
        {
            Snackbar.Add(L["RequestError"], K7Severity.Error);
        }
    }

    private async Task AcceptPeer(PeerServerDto peer)
    {
        try
        {
            await FederationService.AcceptPeerAsync(peer.Id);
            Snackbar.Add(L["PeerAccepted"], K7Severity.Success);
            await LoadPeers();
        }
        catch
        {
            Snackbar.Add(L["AcceptError"], K7Severity.Error);
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

    private async Task RevokePeer(PeerServerDto peer)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["RevokeTitle"],
            string.Format(L["RevokeMessage"], peer.Name),
            yesText: L["RevokeConfirm"],
            cancelText: L["Cancel"]);

        if (confirmed is not true)
            return;

        try
        {
            await FederationService.RevokePeerAsync(peer.Id);
            Snackbar.Add(L["PeerRevoked"], K7Severity.Success);
            await LoadPeers();
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
        _ => "default"
    };

    private string GetStatusLabel(PeerStatus status) => status switch
    {
        PeerStatus.Active => L["StatusActive"],
        PeerStatus.Pending => L["StatusPending"],
        PeerStatus.Revoked => L["StatusRevoked"],
        _ => L["StatusUnknown"]
    };
}
