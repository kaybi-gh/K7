namespace K7.Clients.Shared.Models;

public sealed class K7DialogResult
{
    public bool Canceled { get; init; }
    public object? Data { get; init; }

    public static K7DialogResult Ok<T>(T data) => new() { Data = data };
    public static K7DialogResult Ok() => new();
    public static K7DialogResult Cancel() => new() { Canceled = true };
}
