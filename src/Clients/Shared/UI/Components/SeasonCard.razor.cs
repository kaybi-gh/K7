using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public partial class SeasonCard
{
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter, EditorRequired]
    public required LiteSerieSeasonDto Season { get; set; }

    [Parameter]
    public string? PosterUrl { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Code is "Enter" or "Space")
        {
            await OnClick.InvokeAsync();
        }
    }
}
