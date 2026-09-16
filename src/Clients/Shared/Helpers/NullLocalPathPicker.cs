using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public sealed class NullLocalPathPicker : ILocalPathPicker
{
    public bool CanPick => false;

    public Task<string?> PickFolderAsync(string? initialPath = null) =>
        Task.FromResult<string?>(null);

    public Task<string?> PickFileAsync(IReadOnlyList<string> extensions, string? initialPath = null) =>
        Task.FromResult<string?>(null);
}
