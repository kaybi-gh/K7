namespace K7.Server.Application.Common.Interfaces;

public interface IClientAppPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);
    bool VerifyToken(string passwordHash, string token, string salt);
    (string password, string hash) GeneratePassword();
}
