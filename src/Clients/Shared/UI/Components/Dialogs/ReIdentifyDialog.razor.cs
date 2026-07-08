using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class ReIdentifyDialog
{
    [Inject] private IMediaService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public Guid? IndexedFileId { get; set; }

    [Parameter]
    public Guid? MediaId { get; set; }

    [Parameter]
    public string? InitialSearchQuery { get; set; }

    [Parameter]
    public int? InitialSearchYear { get; set; }

    [Parameter]
    public K7.Server.Domain.Enums.MediaType? MediaType { get; set; }

    [Parameter]
    public Guid? LibraryId { get; set; }

    private string _searchQuery = "";
    private int? _searchYear;
    private string? _searchProviderId;

    private bool _isSearching;
    private bool _isSubmitting;

    private List<MetadataSearchResult>? _results;
    private MetadataSearchResult? _selectedResult;

    protected override void OnInitialized()
    {
        _searchQuery = InitialSearchQuery ?? "";
        _searchYear = InitialSearchYear;
        base.OnInitialized();
    }

    private async Task SearchAsync()
    {
        if (_isSearching)
            return;

        if (string.IsNullOrWhiteSpace(_searchQuery) && string.IsNullOrWhiteSpace(_searchProviderId))
        {
            Snackbar.Add(L["EnterTitleOrId"], K7Severity.Warning);
            return;
        }

        _isSearching = true;
        _selectedResult = null;
        StateHasChanged();

        try
        {
            var query = string.IsNullOrWhiteSpace(_searchQuery) ? string.Empty : _searchQuery.Trim();
            var providerId = string.IsNullOrWhiteSpace(_searchProviderId) ? null : _searchProviderId.Trim();
            var results = await K7ServerService.SearchMetadataAsync(query, _searchYear, providerId, MediaType, LibraryId);
            _results = results.ToList();
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
