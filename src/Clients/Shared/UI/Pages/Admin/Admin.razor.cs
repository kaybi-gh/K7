using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin;

public partial class Admin
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter] public string? Tab { get; set; }

    protected override void OnParametersSet()
    {
        var target = string.IsNullOrEmpty(Tab) ? "/admin/dashboard?sidebar=open" : $"/admin/{Tab}";
        NavigationManager.NavigateTo(target, replace: true);
    }
}
