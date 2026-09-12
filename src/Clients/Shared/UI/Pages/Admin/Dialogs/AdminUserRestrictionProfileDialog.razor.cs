using K7.Clients.Shared.UI;
using K7.Shared.Dtos.Restrictions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public sealed record UserRestrictionAssignmentResult(
    Guid? ProfileId,
    bool AgeRestrictionEnabled,
    DateOnly? DateOfBirth,
    bool HideUnratedTitles);

public partial class AdminUserRestrictionProfileDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IStringLocalizer<AdminUserRestrictionProfileDialog> L { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public Guid? CurrentProfileId { get; set; }
    [Parameter] public bool CurrentAgeRestrictionEnabled { get; set; }
    [Parameter] public DateOnly? CurrentDateOfBirth { get; set; }
    [Parameter] public bool CurrentHideUnratedTitles { get; set; } = true;

    private bool _isLoading = true;
    private List<ContentRestrictionProfileDto> _profiles = [];
    private Guid? _selectedProfileId;
    private bool _ageRestrictionEnabled;
    private bool _hideUnratedTitles = true;
    private string _dateOfBirthText = "";
    private string? _validationError;

    protected override async Task OnInitializedAsync()
    {
        _selectedProfileId = CurrentProfileId;
        _ageRestrictionEnabled = CurrentAgeRestrictionEnabled;
        _hideUnratedTitles = CurrentHideUnratedTitles;
        _dateOfBirthText = CurrentDateOfBirth?.ToString("yyyy-MM-dd") ?? "";
        try
        {
            _profiles = await K7ServerService.GetContentRestrictionProfilesAsync();
        }
        catch
        {
            _profiles = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Cancel() => Dialog.Cancel();

    private void Submit()
    {
        _validationError = null;
        DateOnly? dateOfBirth = null;
        if (!string.IsNullOrWhiteSpace(_dateOfBirthText)
            && DateOnly.TryParse(_dateOfBirthText, out var parsed))
            dateOfBirth = parsed;

        if (_ageRestrictionEnabled && dateOfBirth is null)
        {
            _validationError = L["DateOfBirthRequired"];
            return;
        }

        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            _validationError = L["DateOfBirthInFuture"];
            return;
        }

        Dialog.Close(K7DialogResult.Ok(new UserRestrictionAssignmentResult(
            _selectedProfileId,
            _ageRestrictionEnabled,
            dateOfBirth,
            _hideUnratedTitles)));
    }
}
