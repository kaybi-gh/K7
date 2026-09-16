namespace K7.Clients.Shared.Interfaces;

public interface ILocalPathPicker
{
    bool CanPick { get; }

    Task<string?> PickFolderAsync(string? initialPath = null);

    Task<string?> PickFileAsync(IReadOnlyList<string> extensions, string? initialPath = null);
}
