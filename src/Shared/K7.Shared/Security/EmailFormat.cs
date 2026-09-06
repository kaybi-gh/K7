using System.Net.Mail;

namespace K7.Shared.Security;

public static class EmailFormat
{
    public static bool IsValidOptional(string? email) =>
        string.IsNullOrWhiteSpace(email) || IsValidRequired(email);

    public static bool IsValidRequired(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var trimmed = email.Trim();
        return MailAddress.TryCreate(trimmed, out var address)
            && string.Equals(address.Address, trimmed, StringComparison.Ordinal)
            && address.Host.Contains('.', StringComparison.Ordinal);
    }
}
