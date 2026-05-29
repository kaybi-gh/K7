using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SyncPlayJoin : IDisposable
{
    [Parameter] public string Token { get; set; } = "";

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private JoinState _state = JoinState.Loading;
    private string _guestNickname = "";
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        SyncPlay.GroupUpdated += OnGroupJoined;
        SyncPlay.ErrorReceived += OnError;

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var isAuthenticated = authState.User.Identity?.IsAuthenticated == true;

        if (isAuthenticated)
        {
            await WaitForHubAndJoinAsync();
        }
        else
        {
            _state = JoinState.GuestNickname;
        }
    }

    private async Task WaitForHubAndJoinAsync(string? guestDisplayName = null)
    {
        if (HubClient.State == HubConnectionState.Connected)
        {
            await SyncPlay.JoinViaInviteTokenAsync(Token, guestDisplayName);
            return;
        }

        var tcs = new TaskCompletionSource();
        void OnConnected(HubConnectionState state)
        {
            if (state == HubConnectionState.Connected)
                tcs.TrySetResult();
        }

        HubClient.ConnectionStateChanged += OnConnected;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            cts.Token.Register(() => tcs.TrySetCanceled());
            await tcs.Task;
            await SyncPlay.JoinViaInviteTokenAsync(Token, guestDisplayName);
        }
        catch (TaskCanceledException)
        {
            _state = JoinState.Error;
            _error = "Connection timed out. Please try again.";
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            HubClient.ConnectionStateChanged -= OnConnected;
        }
    }

    private async Task JoinAsGuest()
    {
        if (string.IsNullOrWhiteSpace(_guestNickname)) return;

        await JS.InvokeVoidAsync("K7.unlockAudio");
        _state = JoinState.Loading;
        await WaitForHubAndJoinAsync(_guestNickname.Trim());
    }

    private void OnGroupJoined()
    {
        if (SyncPlay.IsInGroup)
        {
            _state = JoinState.Joined;
            InvokeAsync(async () =>
            {
                StateHasChanged();
                await Task.Delay(1000);
                Navigation.NavigateTo("/");
            });
        }
    }

    private void OnError(string errorCode)
    {
        _state = JoinState.Error;
        _error = errorCode switch
        {
            "invalid_invite_link" => "This invite link is invalid or expired.",
            "group_not_found" => "The group no longer exists.",
            _ => $"An error occurred: {errorCode}"
        };
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        SyncPlay.GroupUpdated -= OnGroupJoined;
        SyncPlay.ErrorReceived -= OnError;
    }

    private enum JoinState { Loading, GuestNickname, Error, Joined }
}
