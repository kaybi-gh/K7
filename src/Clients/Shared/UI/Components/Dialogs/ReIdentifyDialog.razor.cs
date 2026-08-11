using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class ReIdentifyDialog
{
    [Inject] private IMediaService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public Guid? IndexedFileId { get; set; }

    [Parameter]
    public Guid? MediaId { get; set; }

    [Parameter]
    public string? InitialSearchQuery { get; set; }

    [Parameter]
    public string? InitialSearchArtist { get; set; }

    [Parameter]
    public string? InitialSearchAlbum { get; set; }

    [Parameter]
    public int? InitialSearchYear { get; set; }

    [Parameter]
    public MediaType? MediaType { get; set; }

    [Parameter]
    public Guid? LibraryId { get; set; }

    /// <summary>File or series folder path shown as context for the re-identify.</summary>
    [Parameter]
    public string? SourcePath { get; set; }

    /// <summary>Current provider external id so search can keep / pin the existing match.</summary>
    [Parameter]
    public string? CurrentExternalId { get; set; }

    [Parameter]
    public string? CurrentProvider { get; set; }

    private string _searchQuery = "";
    private string _searchArtist = "";
    private string _searchAlbum = "";
    private int? _searchYear;
    private string? _searchProviderId;

    private bool _isSearching;
    private bool _isSubmitting;

    private List<MetadataSearchResult>? _results;
    private MetadataSearchResult? _selectedResult;
    private bool _showProviderExternalLink = true;

    private bool IsMusicSearch => MediaType == K7.Server.Domain.Enums.MediaType.MusicAlbum;

    private string ResultArtClass =>
        IsMusicSearch ? "reidentify-result-art reidentify-result-art--cover" : "reidentify-result-art reidentify-result-art--poster";

    private string ResultGridClass =>
        IsMusicSearch ? "grid grid--cover" : "grid grid--poster";

    protected override void OnInitialized()
    {
        if (DeviceService.CachedDeviceType is { } cached)
            _showProviderExternalLink = cached != DeviceType.TV;

        if (IsMusicSearch)
        {
            _searchArtist = InitialSearchArtist ?? "";
            _searchAlbum = InitialSearchAlbum ?? InitialSearchQuery ?? "";
        }
        else
        {
            _searchQuery = InitialSearchQuery ?? "";
        }

        _searchYear = InitialSearchYear;
        base.OnInitialized();
    }

    protected override async Task OnInitializedAsync()
    {
        if (DeviceService.CachedDeviceType is null)
            _showProviderExternalLink = await DeviceService.GetDeviceTypeAsync() != DeviceType.TV;
    }

    private async Task SearchAsync()
    {
        if (_isSearching)
            return;

        var query = BuildSearchQuery();
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(_searchProviderId))
        {
            Snackbar.Add(IsMusicSearch ? L["EnterArtistOrAlbumOrId"] : L["EnterTitleOrId"], K7Severity.Warning);
            return;
        }

        _isSearching = true;
        _selectedResult = null;
        StateHasChanged();

        try
        {
            var providerId = string.IsNullOrWhiteSpace(_searchProviderId) ? null : _searchProviderId.Trim();
            var results = await K7ServerService.SearchMetadataAsync(
                query ?? string.Empty,
                _searchYear,
                providerId,
                MediaType,
                LibraryId);
            _results = results.ToList();
            await EnsureCurrentMatchIncludedAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(L["SearchError"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isSearching = false;
            StateHasChanged();
        }
    }

    private string? BuildSearchQuery()
    {
        if (IsMusicSearch)
        {
            return ReIdentifySearchDefaultsHelper.BuildMusicAlbumLuceneQuery(_searchArtist, _searchAlbum)
                ?? FirstNonEmpty(_searchAlbum, _searchArtist);
        }

        return string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// Auto-identify often finds the right id even when free-text search ranks it poorly.
    /// Keep that match visible at the top of re-identify results.
    /// </summary>
    private async Task EnsureCurrentMatchIncludedAsync()
    {
        if (_results is null
            || string.IsNullOrWhiteSpace(CurrentExternalId)
            || _results.Any(r => string.Equals(r.ExternalId, CurrentExternalId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        try
        {
            var currentHits = await K7ServerService.SearchMetadataAsync(
                string.Empty,
                year: null,
                providerId: CurrentExternalId,
                MediaType,
                LibraryId);
            var current = currentHits.FirstOrDefault(r =>
                string.IsNullOrWhiteSpace(CurrentProvider)
                || string.Equals(r.Provider, CurrentProvider, StringComparison.OrdinalIgnoreCase));
            if (current is null)
                return;

            _results.Insert(0, current);
        }
        catch
        {
            // Non-critical: search results still usable without the current match pinned.
        }
    }

    private async Task SubmitAsync()
    {
        if (_selectedResult is null) return;

        _isSubmitting = true;
        StateHasChanged();

        try
        {
            if (MediaId.HasValue)
            {
                var request = new ReidentifyMediaRequest
                {
                    SelectedProvider = _selectedResult.Provider,
                    SelectedExternalId = _selectedResult.ExternalId
                };
                await K7ServerService.ReidentifyMediaAsync(MediaId.Value, request);
            }
            else if (IndexedFileId.HasValue)
            {
                var request = new ReidentifyIndexedFileRequest
                {
                    SelectedProvider = _selectedResult.Provider,
                    SelectedExternalId = _selectedResult.ExternalId
                };
                await K7ServerService.ReidentifyIndexedFileAsync(IndexedFileId.Value, request);
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(L["ReidentifyError"], ex.Message), K7Severity.Error);
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    private void SelectResult(MetadataSearchResult result) => _selectedResult = result;

    private void OnResultKeyDown(KeyboardEventArgs args, MetadataSearchResult result)
    {
        if (args.Key is "Enter" or " " or "Spacebar")
            SelectResult(result);
    }

    private void Cancel() => Dialog.Cancel();
}
