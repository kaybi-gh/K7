using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class ReIdentifyDialog
{
    [Inject] private IMediaService K7ServerService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public Guid? IndexedFileId { get; set; }

    [Parameter]
    public Guid? MediaId { get; set; }

    [Parameter]
    public string? InitialSearchQuery { get; set; }

    [Parameter]
    public K7.Server.Domain.Enums.MediaType? MediaType { get; set; }

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
        base.OnInitialized();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery) && string.IsNullOrWhiteSpace(_searchProviderId))
        {
            Snackbar.Add(L["EnterTitleOrId"], Severity.Warning);
            return;
        }

        _isSearching = true;
        _selectedResult = null;
        StateHasChanged();

        try
        {
            var results = await K7ServerService.SearchMetadataAsync(_searchQuery, _searchYear, _searchProviderId, MediaType);
            _results = results.ToList();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(L["SearchError"], ex.Message), Severity.Error);
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

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(L["ReidentifyError"], ex.Message), Severity.Error);
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
