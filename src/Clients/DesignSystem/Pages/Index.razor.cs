using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.DesignSystem.Pages;

public partial class Index
{
    private IReadOnlyList<string> UncataloguedComponents { get; set; } = [];

    protected override void OnInitialized()
    {
        var demoed = new HashSet<string>(StringComparer.Ordinal)
        {
            "K7Alert", "K7Button", "K7Chip", "K7Icon", "K7Avatar", "K7Divider", "K7EmptyState",
            "K7ProgressBar", "K7Skeleton", "K7Spinner", "K7FabButton", "K7IconButton",
            "K7IconToggleButton", "K7Paper", "K7Image", "K7TextField", "K7Select", "K7DateRangePicker",
            "K7Slider", "K7Switch", "K7CheckboxList", "K7ExpansionPanel", "K7Table", "K7Menu",
            "K7CategoryCard", "K7DataTable", "K7BackToTop", "K7JumpIndex", "MediaCard", "Carousel",
            "RatingStars", "EpisodeListItem", "PersonRole", "BrowseView"
        };

        var assembly = typeof(K7.Clients.Shared.UI.Components.K7Button).Assembly;
        UncataloguedComponents = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("K7", StringComparison.Ordinal))
            .Select(t => t.Name)
            .Except(demoed, StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }
}
