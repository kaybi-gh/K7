namespace K7.Clients.Shared.Helpers;

public static class MpcExeLocator
{
    private static readonly string[] FileNames =
    [
        "mpc-hc64.exe",
        "mpc-hc.exe",
        "mpc-be64.exe",
        "mpc-be.exe"
    ];

    public static string? TryFind(Func<string, bool>? fileExists = null)
    {
        var exists = fileExists ?? File.Exists;

        foreach (var candidate in EnumerateCandidates())
        {
            if (exists(candidate))
                return candidate;
        }

        return null;
    }

    public static IReadOnlyList<string> EnumerateCandidates()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        var folders = new[]
        {
            "MPC-HC",
            "MPC-BE",
            "clsid2",
            Path.Combine("Programs", "MPC-HC"),
            Path.Combine("Programs", "MPC-BE")
        };

        var list = new List<string>();
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            foreach (var folder in folders)
            {
                foreach (var name in FileNames)
                    list.Add(Path.Combine(root, folder, name));
            }
        }

        return list;
    }
}
