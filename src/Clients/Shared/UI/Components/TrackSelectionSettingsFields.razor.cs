using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Default audio and subtitle track rows, shared by the personal settings page and the admin
/// defaults. Edits the DTOs in place and raises <see cref="Changed"/> after each change.
/// </summary>
public partial class TrackSelectionSettingsFields
{
    [Parameter, EditorRequired] public TrackSelectionPreferencesDto? Preferences { get; set; }

    /// <summary>Holds "remember track selection". Null hides that row.</summary>
    [Parameter] public VideoPlayerSettingsDto? Settings { get; set; }

    [Parameter] public IReadOnlyList<LibraryDto> Libraries { get; set; } = [];
    [Parameter] public Guid? SelectedLibraryId { get; set; }
    [Parameter] public EventCallback<Guid?> LibraryScopeChanged { get; set; }
    [Parameter] public EventCallback Changed { get; set; }
    [Parameter] public string? Description { get; set; }

    /// <summary>Localizer of the hosting page (personal and admin pages keep their own wording).</summary>
    [Parameter, EditorRequired] public IStringLocalizer Localizer { get; set; } = default!;

    private Task NotifyChangedAsync() => Changed.InvokeAsync();

    private Task OnRememberTrackSelectionChangedAsync(bool value)
    {
        if (Settings is null)
            return Task.CompletedTask;

        Settings.RememberTrackSelection = value;
        return Changed.InvokeAsync();
    }
}
