using K7.Clients.Shared.Mappings;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsHiddenPage
{
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private List<MediaCardViewModel> _items = [];
    private HashSet<string> _processing = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var exclusions = await UserAdminService.GetSelfMediaExclusionsAsync();
            _items = exclusions
                .Select(m => m.ToCardViewModel(ApiClient, n => string.Format(S["SeasonNumber"], n)))
                .Where(vm => vm is not null)
                .Select(vm => vm!)
                .ToList();
        }
        catch { }
        _isLoading = false;
    }

    private async Task UnhideAsync(MediaCardViewModel item)
    {
        _processing.Add(item.Id);
        try
        {
            await UserAdminService.ToggleMediaExclusionAsync(Guid.Parse(item.Id));
            _items.Remove(item);
            Snackbar.Add(string.Format(S["Unhidden"], item.Title), K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _processing.Remove(item.Id);
        }
    }
}
