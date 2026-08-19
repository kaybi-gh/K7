using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class ReassignPlaybackHistoryDialog
{
    [Inject] private ISharedProfileService SharedProfiles { get; set; } = default!;
    [Inject] private IServerInfoService ServerInfo { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public Guid ReferenceId { get; set; }
    [Parameter] public IReadOnlyList<Guid>? ReferenceIds { get; set; }
    [Parameter] public Guid? CurrentSharedProfileId { get; set; }
    [Parameter] public string? MediaTitle { get; set; }
    [Parameter] public bool AsAdministrator { get; set; }

    private bool _loading = true;
    private bool _saving;
    private Guid _selectedId;
    private IReadOnlyList<SharedProfileDto> _profiles = [];

    private IReadOnlyList<Guid> TargetIds =>
        ReferenceIds is { Count: > 0 } ? ReferenceIds : [ReferenceId];

    private bool IsBulk => TargetIds.Count > 1;

    private bool HasChanged => IsBulk || ToNullable(_selectedId) != CurrentSharedProfileId;

    private bool CanSubmit => !_saving && !_loading && HasChanged;

    protected override async Task OnInitializedAsync()
    {
        _selectedId = CurrentSharedProfileId ?? Guid.Empty;
        try
        {
            _profiles = AsAdministrator
                ? await ServerInfo.GetAdminSharedProfilesAsync()
                : await SharedProfiles.GetSharedProfilesAsync();
        }
        catch
        {
            _profiles = [];
        }

        _loading = false;
    }

    private string FormatDestination(Guid id)
    {
        if (id == Guid.Empty)
            return L["PersonalProfile"];

        return _profiles.FirstOrDefault(p => p.Id == id)?.Name ?? L["PersonalProfile"];
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (!HasChanged)
            return;

        _saving = true;
        try
        {
            var destination = ToNullable(_selectedId);
            foreach (var referenceId in TargetIds)
            {
                if (AsAdministrator)
                    await ServerInfo.ReassignAdminPlaybackHistoryAsync(referenceId, destination);
                else
                    await ServerInfo.ReassignPlaybackHistoryAsync(referenceId, destination);
            }

            Dialog.Close(K7DialogResult.Ok(true));
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

    private static Guid? ToNullable(Guid id) => id == Guid.Empty ? null : id;
}
