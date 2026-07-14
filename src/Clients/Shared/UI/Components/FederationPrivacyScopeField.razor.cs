using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class FederationPrivacyScopeField
{
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter] public VisibilityScope Value { get; set; }
    [Parameter] public EventCallback<VisibilityScope> ValueChanged { get; set; }
    [Parameter] public FederationContentType ContentType { get; set; }
    [Parameter] public List<FederationVisibilityGrantDto> Grants { get; set; } = [];
    [Parameter] public EventCallback<List<FederationVisibilityGrantDto>> GrantsChanged { get; set; }
    [Parameter] public EventCallback FormChanged { get; set; }

    private async Task OnScopeChanged(VisibilityScope scope)
    {
        await ValueChanged.InvokeAsync(scope);
        await FormChanged.InvokeAsync();
    }

    private async Task OnGrantsChanged(List<FederationVisibilityGrantDto> grants)
    {
        await GrantsChanged.InvokeAsync(grants);
        await FormChanged.InvokeAsync();
    }
}
