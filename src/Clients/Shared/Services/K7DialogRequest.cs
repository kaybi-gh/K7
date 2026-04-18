using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

public sealed class K7DialogRequest
{
    public required Type Type { get; init; }
    public required string Title { get; init; }
    public K7DialogParameters? Parameters { get; init; }
    public K7DialogOptions? Options { get; init; }
}
