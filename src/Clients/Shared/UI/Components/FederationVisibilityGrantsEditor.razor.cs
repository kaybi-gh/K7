using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class FederationVisibilityGrantsEditor
{
    [Parameter] public FederationContentType ContentType { get; set; }
    [Parameter] public List<FederationVisibilityGrantDto> Grants { get; set; } = [];
    [Parameter] public EventCallback<List<FederationVisibilityGrantDto>> GrantsChanged { get; set; }

    private readonly List<FederationGrantTargetDto> _targets = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        _targets.Clear();
        _targets.AddRange(await ReviewService.GetFederationGrantTargetsAsync());
        _loading = false;
    }

    private bool IsTargetSelected(FederationGrantTargetDto target) =>
        Grants.Any(grant => grant.ContentType == ContentType && MatchesGrant(grant, target));

    private async Task OnTargetToggled((FederationGrantTargetDto Item, bool Checked) change)
    {
        if (change.Checked)
        {
            if (!IsTargetSelected(change.Item))
            {
                Grants.Add(new FederationVisibilityGrantDto
                {
                    ContentType = ContentType,
                    TargetUserId = change.Item.TargetUserId,
                    TargetPeerServerId = change.Item.TargetPeerServerId,
                    TargetOriginUserId = change.Item.TargetOriginUserId
                });
            }
        }
        else
        {
            Grants.RemoveAll(grant => grant.ContentType == ContentType && MatchesGrant(grant, change.Item));
        }

        await GrantsChanged.InvokeAsync(Grants);
    }

    private static bool MatchesGrant(FederationVisibilityGrantDto grant, FederationGrantTargetDto target) =>
        target.Kind switch
        {
            FederationGrantTargetKind.LocalUser =>
                grant.TargetUserId == target.TargetUserId
                && grant.TargetPeerServerId is null
                && grant.TargetOriginUserId is null,
            FederationGrantTargetKind.PeerServer =>
                grant.TargetPeerServerId == target.TargetPeerServerId
                && grant.TargetOriginUserId is null
                && grant.TargetUserId is null,
            FederationGrantTargetKind.FederatedUser =>
                grant.TargetPeerServerId == target.TargetPeerServerId
                && grant.TargetOriginUserId == target.TargetOriginUserId
                && grant.TargetUserId is null,
            _ => false
        };
}
