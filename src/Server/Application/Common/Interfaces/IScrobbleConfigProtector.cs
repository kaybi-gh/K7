namespace K7.Server.Application.Common.Interfaces;

public interface IScrobbleConfigProtector
{
    string Protect(string json);
    string Unprotect(string protectedJson);
}
