using K7.Clients.Shared.Domain.Interfaces;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.Components;

public partial class RatingStars
{
    [Parameter, EditorRequired]
    public Guid MediaId { get; set; }

    [Parameter]
    public int? Value { get; set; }

    [Parameter]
    public EventCallback<int?> ValueChanged { get; set; }

    [Parameter]
    public Size Size { get; set; } = Size.Small;

    private int StarCount => Value.HasValue ? (int)Math.Ceiling(Value.Value / 2.0) : 0;

    private string GetStarIcon(int star) =>
        star <= StarCount ? Icons.Material.Filled.Star : Icons.Material.Outlined.StarBorder;

    private Color GetStarColor(int star) =>
        star <= StarCount ? Color.Warning : Color.Default;

    private async Task OnStarClick(int star)
    {
        var newValue = star * 2;

        // Clicking the same star clears the rating
        if (newValue == Value)
        {
            newValue = 0;
        }

        Value = newValue > 0 ? newValue : null;
        await ValueChanged.InvokeAsync(Value);

        try
        {
            await K7ServerService.RateMediaAsync(MediaId, newValue);
        }
        catch
        {
            // Silently fail — optimistic UI
        }
    }
}
