namespace K7.Shared.Interfaces;

public interface IK7ServerService
{
    HttpClient HttpClient { get; }
    Uri? GetAbsoluteUri(string? relativePath);
}
