namespace K7.Server.Application.Common.Security;

public static class PathContainmentHelper
{
    public static bool IsPathContained(string candidatePath, IEnumerable<string> allowedRoots)
    {
        var fullCandidate = Path.GetFullPath(candidatePath);
        foreach (var root in allowedRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            var fullRoot = Path.GetFullPath(root);
            if (fullRoot.Length > 1 && fullRoot.EndsWith(Path.DirectorySeparatorChar))
                fullRoot = fullRoot.TrimEnd(Path.DirectorySeparatorChar);

            if (fullCandidate.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            // "/" on Unix must use prefix "/" not "//", otherwise nothing under root matches.
            var prefix = fullRoot.Length == 1 && fullRoot[0] == Path.DirectorySeparatorChar
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;

            if (fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static void EnsurePathContained(string candidatePath, IEnumerable<string> allowedRoots, string errorMessage)
    {
        if (!IsPathContained(candidatePath, allowedRoots))
            throw new UnauthorizedAccessException(errorMessage);
    }
}
