using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

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
}
