using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Shell of the admin Advanced settings: one index (transcoding, link previews) shared by the
/// pages that edit them. Each page renders its own form as <see cref="ChildContent"/>.
/// </summary>
public partial class AdvancedSettingsSplit
{
    public const string BasePath = "/admin/advanced";
    public const string Transcoding = "transcoding";
    public const string LinkPreviews = "link-previews";

    /// <summary>Sub-page being rendered: <see cref="Transcoding"/> or <see cref="LinkPreviews"/>.</summary>
    [Parameter, EditorRequired] public string Section { get; set; } = Transcoding;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>False on the bare index address, which shows the index alone on narrow screens and TV.</summary>
    private bool DetailOpen =>
        !string.Equals(CustomNavPath.From(Navigation), BasePath, StringComparison.OrdinalIgnoreCase);

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    // A page that serves several addresses is not rendered again when only the address
    // changes, so the shell follows the address itself to open and close the sub-page.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);
}
