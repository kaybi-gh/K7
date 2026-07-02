using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class LibraryBrowseAdvancedFiltersDialog
{
    [Inject] private IStringLocalizer<SmartPlaylistDialog> SpL { get; set; } = default!;
    [Inject] private IStringLocalizer<LibraryBrowseFilters> L { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public RuleGroupDto InitialFilter { get; set; } = MediaBrowseFilterPresets.Empty;
    [Parameter] public MediaType MediaType { get; set; }
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }
    [Parameter] public MediaTagsDto? Tags { get; set; }

    private RuleGroupDto _filter = MediaBrowseFilterPresets.Empty;
    private IReadOnlyList<RuleFieldDescriptorDto> _fieldDescriptors = [];

    protected override void OnParametersSet()
    {
        _filter = InitialFilter;
        _fieldDescriptors = RuleFieldLocalization.Localize(
            RuleFieldCatalog.GetDescriptors(MediaType),
            SpL,
            L,
            Tags);
    }

    private void OnFilterChanged(RuleGroupDto value) => _filter = value;

    private void Cancel() => Dialog.Cancel();

    private void Apply() => Dialog.Close(K7DialogResult.Ok(_filter));

    private async Task<IReadOnlyList<string>> SearchSuggestionsAsync(
        string field,
        string searchText,
        CancellationToken cancellationToken) =>
        await MediaBrowseTagSearch.SearchAsync(
            MediaService,
            field,
            searchText,
            LibraryIds,
            LibraryGroupIds,
            MediaType,
            cancellationToken);
}
