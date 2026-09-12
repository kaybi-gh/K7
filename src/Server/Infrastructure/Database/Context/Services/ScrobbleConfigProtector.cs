using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace K7.Server.Infrastructure.Database.Context.Services;

public sealed class ScrobbleConfigProtector(IDataProtectionProvider dataProtectionProvider) : IScrobbleConfigProtector
{
    private const string Prefix = "k7dp:";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("K7.ScrobblerAccounts.v1");

    public string Protect(string json)
    {
        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(json));
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedJson)
    {
        if (!protectedJson.StartsWith(Prefix, StringComparison.Ordinal))
            return protectedJson;

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedJson[Prefix.Length..]);
            return Encoding.UTF8.GetString(_protector.Unprotect(protectedBytes));
        }
        catch (CryptographicException)
        {
            return protectedJson;
        }
        catch (FormatException)
        {
            return protectedJson;
        }
    }
}
