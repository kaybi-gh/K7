using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class RatingStars
{
    [Parameter, EditorRequired]
    public Guid MediaId { get; set; }

    [Parameter]
    public int? Value { get; set; }

    [Parameter]
    public EventCallback<int?> ValueChanged { get; set; }

    [Parameter]
    public string Size { get; set; } = "sm";

    private bool _canRate;

    private int StarCount => Value.HasValue ? (int)Math.Ceiling(Value.Value / 2.0) : 0;

    protected override async Task OnInitializedAsync()
    {
        _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
    }

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
            // Silently fail - optimistic UI
        }
    }
}
