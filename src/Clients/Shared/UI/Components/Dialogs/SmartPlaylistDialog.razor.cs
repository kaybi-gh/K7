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

public partial class SmartPlaylistDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid? SmartPlaylistId { get; set; }
    [Parameter] public string? InitialTitle { get; set; }
    [Parameter] public string? InitialDescription { get; set; }
    [Parameter] public MediaType InitialMediaType { get; set; } = MediaType.MusicTrack;
    [Parameter] public RuleGroupDto? InitialRuleFilter { get; set; }
    [Parameter] public int? InitialLimit { get; set; }
    [Parameter] public SmartPlaylistOrderBy InitialOrderBy { get; set; } = SmartPlaylistOrderBy.DateAdded;
    [Parameter] public bool InitialOrderDescending { get; set; } = true;

    private bool _isEdit => SmartPlaylistId.HasValue;

    private string _title = "";
    private string? _description;
    private MediaType _mediaType = MediaType.MusicTrack;
    private RuleGroupDto _ruleFilter = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    private IReadOnlyList<RuleFieldDescriptorDto> _fieldDescriptors = [];
    private IReadOnlyList<SmartPlaylistOrderBy> _orderByOptions = [];
    private MediaTagsDto? _tags;
    private int? _limit;
    private SmartPlaylistOrderBy _orderBy = SmartPlaylistOrderBy.DateAdded;
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
        _orderBy = SmartPlaylistOrderByCatalog.Normalize(_orderBy, _mediaType);
        _orderByOptions = SmartPlaylistOrderByCatalog.GetOptions(_mediaType);
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
        _orderByOptions = SmartPlaylistOrderByCatalog.GetOptions(_mediaType);
        _orderBy = SmartPlaylistOrderByCatalog.Normalize(_orderBy, _mediaType);
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
                await K7ServerService.UpdateSmartPlaylistAsync(SmartPlaylistId!.Value, new UpdateSmartPlaylistRequest
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
                Dialog.Close(K7DialogResult.Ok(SmartPlaylistId!.Value));
            }
            else
            {
                var id = await K7ServerService.CreateSmartPlaylistAsync(new CreateSmartPlaylistRequest
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

    private string GetOrderByLabel(SmartPlaylistOrderBy order) => order switch
    {
        SmartPlaylistOrderBy.Title => L["OrderByTitle"],
        SmartPlaylistOrderBy.DateAdded => L["OrderByDateAdded"],
        SmartPlaylistOrderBy.LastPlayed => L["OrderByLastPlayed"],
        SmartPlaylistOrderBy.PlayCount => L["OrderByPlayCount"],
        SmartPlaylistOrderBy.Rating => L["OrderByRating"],
        SmartPlaylistOrderBy.Year => L["OrderByYear"],
        SmartPlaylistOrderBy.Random => L["OrderByRandom"],
        SmartPlaylistOrderBy.ArtistName => L["OrderByArtist"],
        SmartPlaylistOrderBy.AlbumTitle => L["OrderByAlbum"],
        SmartPlaylistOrderBy.TrackNumber => L["OrderByTrackNumber"],
        SmartPlaylistOrderBy.Duration => L["OrderByDuration"],
        _ => order.ToString()
    };
}
