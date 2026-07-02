using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class LinkDeviceAuthorize
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

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

        await JSRuntime.InvokeVoidAsync("eval", """
            (function () {
                var segments = document.querySelectorAll('.code-segment');
                var hidden = document.getElementById('user-code-hidden');
                if (!hidden || segments.length === 0) return;

                var initial = hidden.value || '';
                if (initial) {
                    var parts = initial.replace(/[^0-9]/g, '');
                    for (var i = 0; i < segments.length; i++) {
                        segments[i].value = parts.substring(i * 4, (i + 1) * 4);
                    }
                }

                function syncHidden() {
                    var vals = [];
                    segments.forEach(function (s) { vals.push(s.value); });
                    hidden.value = vals.join('-');
                }

                segments.forEach(function (seg, idx) {
                    seg.addEventListener('input', function () {
                        this.value = this.value.replace(/[^0-9]/g, '');
                        syncHidden();
                        if (this.value.length >= 4 && idx < segments.length - 1) {
                            segments[idx + 1].focus();
                        }
                    });

                    seg.addEventListener('keydown', function (e) {
                        if (e.key === 'Backspace' && this.value.length === 0 && idx > 0) {
                            segments[idx - 1].focus();
                        }
                    });

                    seg.addEventListener('paste', function (e) {
                        e.preventDefault();
                        var pasted = (e.clipboardData || window.clipboardData).getData('text');
                        var digits = pasted.replace(/[^0-9]/g, '');
                        for (var i = 0; i < segments.length; i++) {
                            segments[i].value = digits.substring(i * 4, (i + 1) * 4);
                        }
                        syncHidden();
                        var lastFilled = Math.min(Math.floor(digits.length / 4), segments.length - 1);
                        segments[lastFilled].focus();
                    });
                });

                if (segments[0]) segments[0].focus();
            })();
            """);
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
