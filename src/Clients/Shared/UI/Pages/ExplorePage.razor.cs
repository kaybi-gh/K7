using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class ExplorePage
{
    [SupplyParameterFromQuery(Name = "library-group")]
    public Guid? LibraryGroupId { get; set; }

    [Inject] private ISocialUserService SocialUserService { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;

    private List<LibraryGroupDto> _libraryGroups = [];
    private LibraryGroupDto? _activeGroup;
    private bool _loading = true;
    private bool _showSocialDirectory;
    private GeneralPreferencesDto _generalPreferences = new();
    private Guid? _loadedGroupId;

    private string PageTitleText => _activeGroup is not null
        ? $"{_activeGroup.Title} - {L["PageTitle"]}"
        : L["PageTitle"];

    protected override async Task OnInitializedAsync() => await LoadAsync();

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedGroupId == LibraryGroupId)
            return;

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _libraryGroups = await LibraryService.GetLibraryGroupsAsync();
        }
        catch
        {
            _libraryGroups = [];
        }

        try
        {
            var discovery = await SocialUserService.GetSocialDiscoveryStateAsync();
            _showSocialDirectory = discovery.ShowDirectory;
        }
        catch
        {
            _showSocialDirectory = false;
        }

        try
        {
            _generalPreferences = await UserPreferencesService.GetEffectiveGeneralPreferencesAsync();
        }
        catch
        {
            _generalPreferences = new GeneralPreferencesDto();
        }

        _activeGroup = LibraryGroupId.HasValue
            ? _libraryGroups.FirstOrDefault(g => g.Id == LibraryGroupId.Value)
            : null;
        _loadedGroupId = LibraryGroupId;
        _loading = false;
    }

    private void OnCategoryClick(Guid groupId)
    {
        var group = _libraryGroups.FirstOrDefault(item => item.Id == groupId);
        var action = ExploreNavigationHelper.ResolveTapAction(
            groupId,
            group?.ExploreTapAction ?? ExploreTapAction.Suggestions,
            _generalPreferences);
        NavigateToGroup(groupId, action);
    }

    private void NavigateToGroup(Guid groupId, ExploreTapAction action) =>
        NavigationManager.NavigateTo(ExploreNavigationHelper.GetCategoryHref(groupId, action));

    private static string GetLibraryIconName(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => "film-strip",
        LibraryMediaType.Serie => "television",
        LibraryMediaType.Music => "music-notes",
        _ => "folder"
    };
}
