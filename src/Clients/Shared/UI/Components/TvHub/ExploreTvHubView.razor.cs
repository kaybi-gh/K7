using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.TvHub;

public partial class ExploreTvHubView
{
    [Parameter, EditorRequired] public Guid GroupId { get; set; }

    private bool _loading = true;
    private LibraryGroupDto? _group;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        var snapshot = await ExploreGroupStore.EnsureGroupAsync(GroupId);
        _group = snapshot?.Group;
        _loading = false;
    }
}
