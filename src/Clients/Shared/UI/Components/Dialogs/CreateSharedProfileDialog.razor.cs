using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class CreateSharedProfileDialog
{
    [Inject] private ISharedProfileService SharedProfileService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public SharedProfileDto? EditGroup { get; set; }

    private List<SharedProfileMemberCandidateDto> _candidates = [];
    private HashSet<Guid> _selectedMemberIds = [];
    private Guid _hostUserId;
    private string _name = "";
    private string _pin = "";
    private string _pinConfirm = "";
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
                _selectedMemberIds.Add(me.Id);
                _hostUserId = me.Id;
            }

            if (EditGroup is not null)
            {
                _name = EditGroup.Name;
                _selectedMemberIds = EditGroup.Members.Select(m => m.UserId).ToHashSet();
                _hostUserId = EditGroup.HostUserId;
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

    private void ToggleMember(Guid id, bool selected)
    {
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

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (_selectedMemberIds.Count < 2 || string.IsNullOrWhiteSpace(_name))
            return;

        if (!string.IsNullOrEmpty(_pin) && _pin != _pinConfirm)
        {
            Snackbar.Add(L["PinMismatch"], K7Severity.Error);
            return;
        }

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
                    Pin = string.IsNullOrWhiteSpace(_pin) ? null : _pin
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

                if (_pin != _pinConfirm)
                {
                    Snackbar.Add(L["PinMismatch"], K7Severity.Error);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_pin) || EditGroup.HasPin)
                    await SharedProfileService.SetPinAsync(EditGroup.Id, string.IsNullOrWhiteSpace(_pin) ? null : _pin);
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
