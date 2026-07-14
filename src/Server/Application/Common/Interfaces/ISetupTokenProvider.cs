namespace K7.Server.Application.Common.Interfaces;

public interface ISetupTokenProvider
{
    string? CurrentToken { get; }
    void SetToken(string token);
    void Clear();
}
