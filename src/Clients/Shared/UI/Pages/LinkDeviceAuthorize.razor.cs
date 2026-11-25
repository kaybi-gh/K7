using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class LinkDeviceAuthorize
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private ISharedProfileService SharedProfileService { get; set; } = default!;

    private ElementReference _formRef;
    private ElementReference _hiddenRef;
    private ElementReference _segment0Ref;
    private ElementReference _segment1Ref;
    private ElementReference _segment2Ref;
    private string? _userCode;
    private string? _error;
    private bool _approved;
    private List<ManagedAccountDto> _managedAccounts = [];
    private Guid? _targetUserId;
    private List<DeviceLinkSharedProfileDto> _linkProfiles = [];
    private readonly HashSet<Guid> _selectedProfileIds = [];
    private int _profileLoad;

    private ManagedAccountDto? SelectedAccount =>
        _managedAccounts.FirstOrDefault(account => account.Id == _targetUserId);

    protected override async Task OnInitializedAsync()
    {
        _userCode = GetQueryValue("user_code");
        _error = GetQueryValue("error");
        _approved = GetQueryValue("approved") == "true";

        if (_approved)
            return;

        try
        {
            _managedAccounts = await UserAdminService.GetManagedAccountsAsync();
        }
        catch
        {
            // Not a guardian, or the list is unavailable: the page links the signed-in user as before.
            _managedAccounts = [];
        }

        // After a refused attempt the server sends the choice back, so a retry does not
        // silently fall back to the signed-in user.
        if (Guid.TryParse(GetQueryValue("target_user_id"), out var targetUserId)
            && _managedAccounts.Any(account => account.Id == targetUserId))
        {
            _targetUserId = targetUserId;
        }

        await LoadProfilesAsync();
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

    private string GetErrorMessage() => _error switch
    {
        "invalid_pin" => L["InvalidPin"].Value,
        "access_denied" => L["AccessDenied"].Value,
        _ => L["InvalidCode"].Value
    };

    private static string GetName(ManagedAccountDto account) =>
        account.DisplayName ?? account.UserName ?? account.Id.ToString();

    private static string GetLetter(string name) =>
        name.Length > 0 ? name[..1].ToUpperInvariant() : "?";

    private async Task SelectTarget(Guid? targetUserId)
    {
        if (_targetUserId == targetUserId)
            return;

        _targetUserId = targetUserId;
        _selectedProfileIds.Clear();
        await LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        var generation = ++_profileLoad;
        try
        {
            var profiles = await SharedProfileService.GetDeviceLinkSharedProfilesAsync(_targetUserId);
            if (generation != _profileLoad)
                return;

            _linkProfiles = profiles.ToList();
        }
        catch
        {
            if (generation == _profileLoad)
                _linkProfiles = [];
        }
    }

    private void ToggleProfile(Guid id, ChangeEventArgs args)
    {
        var selected = args.Value is bool on
            ? on
            : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;

        if (selected)
            _selectedProfileIds.Add(id);
        else
            _selectedProfileIds.Remove(id);
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
