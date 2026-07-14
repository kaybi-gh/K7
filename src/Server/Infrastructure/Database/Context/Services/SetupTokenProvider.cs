using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Infrastructure.Database.Context.Services;

public sealed class SetupTokenProvider : ISetupTokenProvider
{
    private string? _currentToken;

    public string? CurrentToken => _currentToken;

    public void SetToken(string token) => _currentToken = token;

    public void Clear() => _currentToken = null;
}
