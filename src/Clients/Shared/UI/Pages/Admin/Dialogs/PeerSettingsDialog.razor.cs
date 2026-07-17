using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class PeerSettingsDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Parameter] public PeerServerDto? Peer { get; set; }

    private static readonly FederationContentType[] _socialContentTypes =
    [
        FederationContentType.Reviews,
        FederationContentType.Collections,
        FederationContentType.Playlists,
        FederationContentType.SmartPlaylists,
        FederationContentType.PlaybackHistory
    ];

    private bool _isLoading = true;
    private string _baseUrl = string.Empty;
    private bool _autoAdd;
    private List<LibraryDto> _libraries = [];
    private List<PeerShareAgreementDto> _inboundAgreements = [];
    private HashSet<Guid> _selectedIds = [];
    private HashSet<Guid> _shareHistoryIds = [];
    private HashSet<Guid> _enabledInboundIds = [];
    private readonly Dictionary<FederationContentType, PeerSocialAgreementDto> _socialAgreements = new();

    protected override async Task OnInitializedAsync()
    {
        if (Peer is not null)
        {
            _baseUrl = Peer.BaseUrl;
            _autoAdd = Peer.AutoAddNewLibraries;
            _selectedIds = Peer.ShareAgreements
                .Where(a => a.Direction == ShareDirection.Outbound)
                .Select(a => a.LibraryId)
                .ToHashSet();
            _shareHistoryIds = Peer.ShareAgreements
                .Where(a => a.Direction == ShareDirection.Outbound && a.SharePlaybackHistory)
                .Select(a => a.LibraryId)
                .ToHashSet();
            _enabledInboundIds = Peer.ShareAgreements
                .Where(a => a.Direction == ShareDirection.Inbound && a.IsEnabled)
                .Select(a => a.Id)
                .ToHashSet();

            foreach (var contentType in _socialContentTypes)
            {
                var existing = Peer.SocialAgreements.FirstOrDefault(a => a.ContentType == contentType);
                _socialAgreements[contentType] = existing ?? new PeerSocialAgreementDto
                {
                    Id = Guid.Empty,
                    ContentType = contentType,
                    AllowOutbound = true,
                    AllowInbound = true
                };
            }
        }

        if (Peer?.IsProvider == true)
        {
            try
            {
                _libraries = await LibraryService.GetLibrariesAsync();
            }
            finally
            {
                _isLoading = false;
            }
        }
        else if (Peer is not null)
        {
            try
            {
                _inboundAgreements = await FederationService.DiscoverPeerLibrariesAsync(Peer.Id);
                _enabledInboundIds = _inboundAgreements
                    .Where(a => a.IsEnabled)
                    .Select(a => a.Id)
                    .ToHashSet();
            }
            finally
            {
                _isLoading = false;
            }
        }
        else
        {
            _isLoading = false;
        }
    }

    private PeerSocialAgreementDto GetSocialAgreement(FederationContentType contentType) =>
        _socialAgreements[contentType];

    private void SetSocialOutbound(FederationContentType contentType, bool value)
    {
        var current = _socialAgreements[contentType];
        _socialAgreements[contentType] = current with { AllowOutbound = value };
    }

    private void SetSocialInbound(FederationContentType contentType, bool value)
    {
        var current = _socialAgreements[contentType];
        _socialAgreements[contentType] = current with { AllowInbound = value };
    }

    private string GetContentTypeLabel(FederationContentType contentType) => contentType switch
    {
        FederationContentType.Reviews => L["ContentReviews"],
        FederationContentType.Collections => L["ContentCollections"],
        FederationContentType.Playlists => L["ContentPlaylists"],
        FederationContentType.SmartPlaylists => L["ContentSmartPlaylists"],
        FederationContentType.PlaybackHistory => L["ContentPlaybackHistory"],
        _ => contentType.ToString()
    };

    private void ToggleLibrary(Guid id)
    {
        if (!_selectedIds.Remove(id))
            _selectedIds.Add(id);
        else
            _shareHistoryIds.Remove(id);
    }

    private void ToggleShareHistory(Guid id)
    {
        if (!_shareHistoryIds.Remove(id))
            _shareHistoryIds.Add(id);
    }

    private void ToggleInbound(Guid agreementId)
    {
        if (!_enabledInboundIds.Remove(agreementId))
            _enabledInboundIds.Add(agreementId);
    }

    private void Cancel() => Dialog.Cancel();

    private void Submit()
    {
        var result = new UpdatePeerRequest
        {
            BaseUrl = _baseUrl,
            SharedLibraryIds = Peer?.IsProvider == true ? _selectedIds.ToList() : null,
            SharePlaybackHistoryLibraryIds = Peer?.IsProvider == true ? _shareHistoryIds.ToList() : null,
            EnabledInboundAgreementIds = Peer?.IsProvider != true ? _enabledInboundIds.ToList() : null,
            AutoAddNewLibraries = _autoAdd,
            SocialAgreements = _socialAgreements.Values.ToList()
        };
        Dialog.Close(K7DialogResult.Ok(result));
    }
}
