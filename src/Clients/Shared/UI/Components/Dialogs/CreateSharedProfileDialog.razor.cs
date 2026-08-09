using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Extensions;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class CreateSharedProfileDialog
{
    [Inject] private ISharedProfileService SharedProfileService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public SharedProfileDto? EditGroup { get; set; }

    private List<SharedProfileMemberCandidateDto> _candidates = [];
    private HashSet<Guid> _selectedMemberIds = [];
    private Guid _currentUserId;
    private Guid _hostUserId;
    private string _name = "";
    private string? _pendingPin;
    private bool _pinChanged;
    private bool _hasPin;
    private bool _loading = true;
    private bool _isSubmitting;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _candidates = (await SharedProfileService.GetMemberCandidatesAsync()).ToList();
            var me = await UserAdminService.GetCurrentUserAsync();
            if (me is not null)
            {
                _currentUserId = me.Id;
                _selectedMemberIds.Add(me.Id);
                _hostUserId = me.Id;
                EnsureCandidate(me.Id, me.DisplayName ?? me.UserName, me.AvatarUrl);
            }

            if (EditGroup is not null)
            {
                _name = EditGroup.Name;
                _selectedMemberIds = EditGroup.Members.Select(m => m.UserId).ToHashSet();
                if (_currentUserId != Guid.Empty)
                    _selectedMemberIds.Add(_currentUserId);
                _hostUserId = EditGroup.HostUserId;
                _hasPin = EditGroup.HasPin;

                // Existing members may block new invitations; keep them visible for edit/host selection.
                foreach (var member in EditGroup.Members)
                    EnsureCandidate(member.UserId, member.DisplayName, member.AvatarUrl);
            }
            else if (_selectedMemberIds.Count >= 2)
            {
                _name = BuildDefaultName();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void EnsureCandidate(Guid id, string? displayName, string? avatarUrl)
    {
        if (_candidates.Any(c => c.Id == id))
            return;

        _candidates.Add(new SharedProfileMemberCandidateDto
        {
            Id = id,
            DisplayName = displayName,
            AvatarUrl = avatarUrl
        });
    }

    private string GetMemberDisplayName(Guid id) =>
        _candidates.FirstOrDefault(c => c.Id == id)?.DisplayName
        ?? EditGroup?.Members.FirstOrDefault(m => m.UserId == id)?.DisplayName
        ?? "-";

    private void ToggleMember(Guid id, bool selected)
    {
        if (id == _currentUserId)
            return;

        if (selected)
            _selectedMemberIds.Add(id);
        else
            _selectedMemberIds.Remove(id);

        if (!_selectedMemberIds.Contains(_hostUserId) && _selectedMemberIds.Count > 0)
            _hostUserId = _selectedMemberIds.First();

        if (string.IsNullOrWhiteSpace(_name) || _name == BuildDefaultName())
            _name = BuildDefaultName();
    }

    private string BuildDefaultName()
    {
        var names = _candidates
            .Where(c => _selectedMemberIds.Contains(c.Id))
            .Select(c => c.DisplayName?.Split(' ').FirstOrDefault() ?? c.DisplayName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(3)
            .ToList();

        return names.Count switch
        {
            0 => "",
            1 => names[0]!,
            2 => $"{names[0]} & {names[1]}",
            _ => string.Join(", ", names.Take(names.Count - 1)) + " & " + names[^1]
        };
    }

    private static string GetInitial(SharedProfileMemberCandidateDto candidate)
    {
        var name = candidate.DisplayName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }

    private async Task SetPinAsync()
    {
        var pin = await PromptPinAsync(L["SetPinDialogTitle"]);
        if (pin is null)
            return;

        var confirm = await PromptPinAsync(L["ConfirmPinDialogTitle"]);
        if (confirm is null)
            return;

        if (pin != confirm)
        {
            Snackbar.Add(L["PinMismatch"], K7Severity.Error);
            return;
        }

        _pendingPin = pin;
        _pinChanged = true;
        _hasPin = true;
    }

    private async Task ChangePinAsync()
    {
        var pin = await PromptPinAsync(L["NewPinDialogTitle"]);
        if (pin is null)
            return;

        var confirm = await PromptPinAsync(L["ConfirmPinDialogTitle"]);
        if (confirm is null)
            return;

        if (pin != confirm)
        {
            Snackbar.Add(L["PinMismatch"], K7Severity.Error);
            return;
        }

        _pendingPin = pin;
        _pinChanged = true;
        _hasPin = true;
    }

    private async Task RemovePinAsync()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["RemovePinDialogTitle"],
            L["RemovePinConfirm"],
            yesText: S["Confirm"],
            cancelText: S["Cancel"]);

        if (confirmed != true)
            return;

        _pendingPin = null;
        _pinChanged = true;
        _hasPin = false;
    }

    private async Task<string?> PromptPinAsync(string title)
    {
        var deviceType = await DeviceService.GetDeviceTypeAsync();
        var options = K7DialogServiceExtensions.CreatePinDialogOptions(deviceType);
        var dialog = await DialogService.ShowAsync<PinDialog>(title, null, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return null;

        return result.Data as string;
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (_selectedMemberIds.Count < 2 || string.IsNullOrWhiteSpace(_name))
            return;

        _isSubmitting = true;
        try
        {
            if (EditGroup is null)
            {
                await SharedProfileService.CreateAsync(new CreateSharedProfileRequest
                {
                    Name = _name.Trim(),
                    HostUserId = _hostUserId,
                    MemberUserIds = _selectedMemberIds.ToList(),
                    Pin = _pinChanged ? _pendingPin : null
                });
            }
            else
            {
                await SharedProfileService.UpdateAsync(EditGroup.Id, new UpdateSharedProfileRequest
                {
                    Name = _name.Trim(),
                    HostUserId = _hostUserId,
                    MemberUserIds = _selectedMemberIds.ToList()
                });

                if (_pinChanged)
                    await SharedProfileService.SetPinAsync(EditGroup.Id, _pendingPin);
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
