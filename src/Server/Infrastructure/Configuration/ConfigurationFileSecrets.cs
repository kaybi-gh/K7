using Microsoft.Extensions.Configuration;

namespace K7.Server.Infrastructure.Configuration;

/// <summary>
/// Resolves configuration keys ending in ":File" by reading the file path and
/// setting the parent key (Docker / Compose secrets pattern).
/// Example: Database__Password__File=/run/secrets/db_password sets Database:Password.
/// </summary>
public static class ConfigurationFileSecrets
{
    public static void AddFileSecretOverrides(this ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var child in configuration.AsEnumerable().ToList())
        {
            var key = child.Key;
            var path = child.Value;

            if (string.IsNullOrEmpty(key)
                || !key.EndsWith(":File", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var targetKey = key[..^":File".Length];
            if (string.IsNullOrEmpty(targetKey))
                continue;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Configuration secret file for '{targetKey}' was not found.",
                    path);
            }

            configuration[targetKey] = File.ReadAllText(path).TrimEnd('\r', '\n');
        }
    }
}
