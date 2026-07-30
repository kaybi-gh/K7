using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class DynamicPlaylistDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid? DynamicPlaylistId { get; set; }
    [Parameter] public string? InitialTitle { get; set; }
    [Parameter] public string? InitialDescription { get; set; }
    [Parameter] public MediaType InitialMediaType { get; set; } = MediaType.MusicTrack;
    [Parameter] public RuleGroupDto? InitialRuleFilter { get; set; }
    [Parameter] public int? InitialLimit { get; set; }
    [Parameter] public DynamicPlaylistOrderBy InitialOrderBy { get; set; } = DynamicPlaylistOrderBy.DateAdded;
    [Parameter] public bool InitialOrderDescending { get; set; } = true;

    private bool _isEdit => DynamicPlaylistId.HasValue;

    private string _title = "";
    private string? _description;
    private MediaType _mediaType = MediaType.MusicTrack;
    private RuleGroupDto _ruleFilter = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    private IReadOnlyList<RuleFieldDescriptorDto> _fieldDescriptors = [];
    private IReadOnlyList<DynamicPlaylistOrderBy> _orderByOptions = [];
    private MediaTagsDto? _tags;
    private int? _limit;
    private DynamicPlaylistOrderBy _orderBy = DynamicPlaylistOrderBy.DateAdded;
    private bool _orderDescending = true;
    private bool _isSubmitting;

    protected override async Task OnInitializedAsync()
    {
        _title = InitialTitle ?? "";
        _description = InitialDescription;
        _mediaType = InitialMediaType;
        _limit = InitialLimit;
        _orderBy = InitialOrderBy;
        _orderDescending = InitialOrderDescending;
        _ruleFilter = InitialRuleFilter ?? new RuleGroupDto { MatchCondition = RuleMatchCondition.All, Items = [] };

        await LoadTagsAsync();
        RefreshFieldDescriptors();
        _orderBy = DynamicPlaylistOrderByCatalog.Normalize(_orderBy, _mediaType);
        _orderByOptions = DynamicPlaylistOrderByCatalog.GetOptions(_mediaType);
    }

    private async Task LoadTagsAsync()
    {
        try
        {
            _tags = await MediaService.GetMediaTagsAsync(new GetMediaTagsQuery());
        }
        catch
        {
            _tags = null;
        }
    }

    private void RefreshFieldDescriptors()
    {
        var allowedFields = RuleFieldCatalog.GetDescriptors(_mediaType)
            .Select(d => d.FieldName)
            .ToHashSet(StringComparer.Ordinal);

        _ruleFilter = RuleFieldCatalog.Sanitize(_ruleFilter, allowedFields);
        _fieldDescriptors = RuleFieldLocalization.Localize(
            RuleFieldCatalog.GetDescriptors(_mediaType),
            L,
            BrowseL,
            _tags);
        _orderByOptions = DynamicPlaylistOrderByCatalog.GetOptions(_mediaType);
        _orderBy = DynamicPlaylistOrderByCatalog.Normalize(_orderBy, _mediaType);
    }

    private void OnMediaTypeChanged(MediaType mediaType)
    {
        _mediaType = mediaType;
        RefreshFieldDescriptors();
    }

    private void OnRuleFilterChanged(RuleGroupDto value) => _ruleFilter = value;

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_title))
            return;

        _isSubmitting = true;
        try
        {
            if (_isEdit)
            {
                await K7ServerService.UpdateDynamicPlaylistAsync(DynamicPlaylistId!.Value, new UpdateDynamicPlaylistRequest
                {
                    Title = _title.Trim(),
                    Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                    MediaType = _mediaType,
                    RuleFilter = _ruleFilter,
                    Limit = _limit,
                    OrderBy = _orderBy,
                    OrderDescending = _orderDescending
                });
                Snackbar.Add(L["Updated"], K7Severity.Success);
                Dialog.Close(K7DialogResult.Ok(DynamicPlaylistId!.Value));
            }
            else
            {
                var id = await K7ServerService.CreateDynamicPlaylistAsync(new CreateDynamicPlaylistRequest
                {
                    Title = _title.Trim(),
                    Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                    MediaType = _mediaType,
                    RuleFilter = _ruleFilter,
                    Limit = _limit,
                    OrderBy = _orderBy,
                    OrderDescending = _orderDescending
                });
                Snackbar.Add(L["Created"], K7Severity.Success);
                Dialog.Close(K7DialogResult.Ok(id));
            }
        }
        catch
        {
            Snackbar.Add(L["SaveError"], K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task<IReadOnlyList<string>> SearchSuggestionsAsync(
        string field,
        string searchText,
        CancellationToken cancellationToken) =>
        await MediaBrowseTagSearch.SearchAsync(
            MediaService,
            field,
            searchText,
            libraryIds: null,
            libraryGroupIds: null,
            _mediaType,
            cancellationToken);

    private string GetOrderByLabel(DynamicPlaylistOrderBy order) => order switch
    {
        DynamicPlaylistOrderBy.Title => L["OrderByTitle"],
        DynamicPlaylistOrderBy.DateAdded => L["OrderByDateAdded"],
        DynamicPlaylistOrderBy.LastPlayed => L["OrderByLastPlayed"],
        DynamicPlaylistOrderBy.PlayCount => L["OrderByPlayCount"],
        DynamicPlaylistOrderBy.Rating => L["OrderByRating"],
        DynamicPlaylistOrderBy.Year => L["OrderByYear"],
        DynamicPlaylistOrderBy.Random => L["OrderByRandom"],
        DynamicPlaylistOrderBy.ArtistName => L["OrderByArtist"],
        DynamicPlaylistOrderBy.AlbumTitle => L["OrderByAlbum"],
        DynamicPlaylistOrderBy.TrackNumber => L["OrderByTrackNumber"],
        DynamicPlaylistOrderBy.Duration => L["OrderByDuration"],
        _ => order.ToString()
    };
}
