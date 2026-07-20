using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class LinkDeviceAuthorize
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ElementReference _formRef;
    private ElementReference _hiddenRef;
    private ElementReference _segment0Ref;
    private ElementReference _segment1Ref;
    private ElementReference _segment2Ref;
    private string? _userCode;
    private string? _error;
    private bool _approved;

    protected override void OnInitialized()
    {
        _userCode = GetQueryValue("user_code");
        _error = GetQueryValue("error");
        _approved = GetQueryValue("approved") == "true";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _approved)
            return;

        var module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/linkDeviceAuthorize.js");
        try
        {
            await module.InvokeVoidAsync(
                "initCodeSegments",
                _formRef,
                _hiddenRef,
                new[] { _segment0Ref, _segment1Ref, _segment2Ref });
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    private string? GetQueryValue(string key)
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return null;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator < 0)
                continue;

            var name = Uri.UnescapeDataString(part[..separator]);
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            return Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return null;
    }
}
