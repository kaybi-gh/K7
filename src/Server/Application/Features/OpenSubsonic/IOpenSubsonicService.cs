namespace K7.Server.Application.Features.OpenSubsonic;

public interface IOpenSubsonicService
{
    Task<OpenSubsonicActionResult> ExecuteAsync(
        string action,
        IReadOnlyDictionary<string, string[]> parameters,
        string username,
        bool canWrite,
        CancellationToken cancellationToken = default);
}
