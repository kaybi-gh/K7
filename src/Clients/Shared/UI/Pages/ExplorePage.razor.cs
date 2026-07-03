using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class ExplorePage
{
    [SupplyParameterFromQuery(Name = "library-group")]
    public Guid? LibraryGroupId { get; set; }

    [Inject] private ISocialUserService SocialUserService { get; set; } = default!;

    private List<LibraryGroupDto> _libraryGroups = [];
    private LibraryGroupDto? _activeGroup;
    private bool _loading = true;
    private bool _showSocialDirectory;
    private bool _isTv;
    private Guid? _loadedGroupId;

    private string PageTitleText => _activeGroup is not null
        ? $"{_activeGroup.Title} - {L["PageTitle"]}"
        : L["PageTitle"];

    private string _libraryGroupPageClass => _isTv
        ? "tv-feed-page"
        : "explore-group-page page-scrollable";

    private string? _libraryGroupInitialFocus => _isTv
        ? "[data-carousel-item] a, [data-carousel-item] button"
        : null;

    protected override async Task OnInitializedAsync()
    {
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        await LoadAsync();
    }

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
            var groupsTask = LibraryService.GetLibraryGroupsAsync();
            var discoveryTask = SocialUserService.GetSocialDiscoveryStateAsync();
            await Task.WhenAll(groupsTask, discoveryTask);
            _libraryGroups = await groupsTask;
            _showSocialDirectory = (await discoveryTask).ShowDirectory;
        }
        catch
        {
            _libraryGroups = [];
            _showSocialDirectory = false;
        }

        _activeGroup = LibraryGroupId.HasValue
            ? _libraryGroups.FirstOrDefault(g => g.Id == LibraryGroupId.Value)
            : null;
        _loadedGroupId = LibraryGroupId;
        _loading = false;
    }

    private static string GetLibraryIconName(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => "film-strip",
        LibraryMediaType.Serie => "television",
        LibraryMediaType.Music => "music-notes",
        _ => "folder"
    };
}
