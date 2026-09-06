using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminAuthenticationPanel
{
    [Inject] private IServerInfoService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private AuthenticationInfoDto? _authInfo;
    private PasswordPolicyDto _policy = PasswordPolicyDto.Defaults;
    private bool _isLoading = true;
    private bool _saving;
    private readonly SettingsFormTracker<PasswordPolicyDto> _formTracker = new();

    private bool IsDirty => _formTracker.IsDirty(_policy);

    private bool ResetDisabled => !IsDirty && _policy == PasswordPolicyDto.Defaults;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authTask = K7ServerService.GetAuthenticationInfoAsync();
            var policyTask = K7ServerService.GetPasswordPolicyAsync();
            await Task.WhenAll(authTask, policyTask);
            _authInfo = authTask.Result;
            _policy = policyTask.Result;
        }
        catch
        {
            _policy = PasswordPolicyDto.Defaults;
        }
        finally
        {
            CaptureFormState();
            _isLoading = false;
        }
    }

    private void CaptureFormState() => _formTracker.Capture(_policy);

    private void CancelChanges()
    {
        _policy = _formTracker.Restore();
    }

    private void OnRequiredLengthChanged(int value)
    {
        _policy = _policy with { RequiredLength = value };
        StateHasChanged();
    }

    private void OnRequiredUniqueCharsChanged(int value)
    {
        _policy = _policy with { RequiredUniqueChars = value };
        StateHasChanged();
    }

    private void OnRequireDigitChanged(bool value)
    {
        _policy = _policy with { RequireDigit = value };
        StateHasChanged();
    }

    private void OnRequireLowercaseChanged(bool value)
    {
        _policy = _policy with { RequireLowercase = value };
        StateHasChanged();
    }

    private void OnRequireUppercaseChanged(bool value)
    {
        _policy = _policy with { RequireUppercase = value };
        StateHasChanged();
    }

    private void OnRequireNonAlphanumericChanged(bool value)
    {
        _policy = _policy with { RequireNonAlphanumeric = value };
        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (_saving || !IsDirty)
            return;

        _saving = true;
        try
        {
            await K7ServerService.UpdatePasswordPolicyAsync(_policy);
            _policy = await K7ServerService.GetPasswordPolicyAsync();
            CaptureFormState();
            Snackbar.Add(L["SaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task ResetAsync()
    {
        if (_saving)
            return;

        _saving = true;
        try
        {
            await K7ServerService.UpdatePasswordPolicyAsync(PasswordPolicyDto.Defaults);
            _policy = await K7ServerService.GetPasswordPolicyAsync();
            CaptureFormState();
            Snackbar.Add(L["ResetSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }
}
