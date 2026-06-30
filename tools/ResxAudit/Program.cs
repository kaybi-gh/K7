using System.Text.RegularExpressions;
using System.Xml.Linq;

var repoRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

var uiRoot = Path.Combine(repoRoot, "src", "Clients", "Shared", "UI");
var resRoot = Path.Combine(uiRoot, "Resources");

var resByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
var resKeysByPath = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

foreach (var file in Directory.EnumerateFiles(resRoot, "*.resx", SearchOption.AllDirectories))
{
    if (file.EndsWith(".en.resx", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    var rel = Path.GetRelativePath(resRoot, file).Replace('\\', '/');
    var name = Path.GetFileNameWithoutExtension(file);
    if (!resByName.TryGetValue(name, out var list))
    {
        list = [];
        resByName[name] = list;
    }

    list.Add(rel);
    resKeysByPath[rel] = LoadResxKeys(file);
}

var localizerPattern = new Regex(
    @"IStringLocalizer<(\w+)>\s+(\w+)",
    RegexOptions.Compiled);
var keyPattern = new Regex(
    @"\b(\w+)\[\s*""([^""]+)""\s*\]",
    RegexOptions.Compiled);

var missingKeys = new List<string>();

foreach (var path in Directory.EnumerateFiles(uiRoot, "*.*", SearchOption.AllDirectories))
{
    if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
    {
        continue;
    }

    if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
        && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    var text = File.ReadAllText(path);
    var localizers = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (Match m in localizerPattern.Matches(text))
    {
        var typeName = m.Groups[1].Value;
        var varName = m.Groups[2].Value;
        localizers.TryAdd(varName, typeName);
    }

    if (localizers.Count == 0)
    {
        continue;
    }

    var fileRel = Path.GetRelativePath(uiRoot, path).Replace('\\', '/');
    foreach (Match match in keyPattern.Matches(text))
    {
        var varName = match.Groups[1].Value;
        var key = match.Groups[2].Value;
        if (!localizers.TryGetValue(varName, out var typeName))
        {
            continue;
        }

        var resPath = ResolveResxPath(typeName, uiRoot, resRoot, resByName);
        if (resPath is null)
        {
            missingKeys.Add($"{fileRel}: {varName}[\"{key}\"] -> missing resx for {typeName}");
            continue;
        }

        if (!resKeysByPath.TryGetValue(resPath, out var keys) || !keys.Contains(key))
        {
            missingKeys.Add($"{fileRel}: {varName}[\"{key}\"] -> missing in Resources/{resPath}");
        }
    }
}

var missingPlacement = AuditPlacement(uiRoot, resRoot, resByName);

Console.WriteLine("=== PLACEMENT ISSUES ===");
foreach (var line in missingPlacement)
{
    Console.WriteLine(line);
}

Console.WriteLine($"Count: {missingPlacement.Count}");
Console.WriteLine();
Console.WriteLine("=== MISSING KEYS (first 100) ===");
foreach (var line in missingKeys.Take(100))
{
    Console.WriteLine(line);
}

Console.WriteLine($"Count: {missingKeys.Count}");
if (missingKeys.Count > 100)
{
    Console.WriteLine($"... and {missingKeys.Count - 100} more");
}

Console.WriteLine();
Console.WriteLine("=== FR/EN KEY PARITY (first 50) ===");
var parityIssues = new List<string>();
foreach (var file in Directory.EnumerateFiles(resRoot, "*.resx", SearchOption.AllDirectories))
{
    if (file.EndsWith(".en.resx", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    var enFile = Path.ChangeExtension(file, null) + ".en.resx";
    if (!File.Exists(enFile))
    {
        continue;
    }

    var frKeys = LoadResxKeys(file);
    var enKeys = LoadResxKeys(enFile);
    var rel = Path.GetRelativePath(resRoot, file).Replace('\\', '/');
    foreach (var key in frKeys.Except(enKeys).OrderBy(x => x))
    {
        parityIssues.Add($"{rel}: missing in .en -> {key}");
    }

    foreach (var key in enKeys.Except(frKeys).OrderBy(x => x))
    {
        parityIssues.Add($"{rel}: missing in default -> {key}");
    }
}

foreach (var line in parityIssues.Take(50))
{
    Console.WriteLine(line);
}

Console.WriteLine($"Count: {parityIssues.Count}");

static HashSet<string> LoadResxKeys(string path)
{
    var doc = XDocument.Load(path);
    return doc.Descendants("data")
        .Select(e => e.Attribute("name")?.Value)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Select(v => v!)
        .ToHashSet(StringComparer.Ordinal);
}

static string? FindSource(string uiRoot, string typeName)
{
    foreach (var ext in new[] { ".razor", ".cs" })
    {
        var matches = Directory
            .EnumerateFiles(uiRoot, $"{typeName}{ext}", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        if (matches.Count > 0)
        {
            return matches[0];
        }
    }

    return null;
}

static string? ResolveResxPath(
    string typeName,
    string uiRoot,
    string resRoot,
    Dictionary<string, List<string>> resByName)
{
    if (typeName == "SharedResource")
    {
        return "SharedResource.resx";
    }

    if (resByName.TryGetValue(typeName, out var paths))
    {
        return paths[0];
    }

    var src = FindSource(uiRoot, typeName);
    if (src is null)
    {
        return null;
    }

    var srcRel = Path.GetRelativePath(uiRoot, src).Replace('\\', '/');
    var srcDir = Path.GetDirectoryName(srcRel)?.Replace('\\', '/') ?? string.Empty;
    return string.IsNullOrEmpty(srcDir)
        ? $"{typeName}.resx"
        : $"{srcDir}/{typeName}.resx";
}

static List<string> AuditPlacement(
    string uiRoot,
    string resRoot,
    Dictionary<string, List<string>> resByName)
{
    var pattern = new Regex(@"IStringLocalizer<(\w+)>", RegexOptions.Compiled);
    var usages = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    foreach (var path in Directory.EnumerateFiles(uiRoot, "*.*", SearchOption.AllDirectories))
    {
        if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        {
            continue;
        }

        if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var text = File.ReadAllText(path);
        foreach (Match match in pattern.Matches(text))
        {
            var typeName = match.Groups[1].Value;
            if (typeName == "SharedResource")
            {
                continue;
            }

            usages.TryAdd(typeName, []);
        }
    }

    var issues = new List<string>();
    foreach (var typeName in usages.Keys.OrderBy(x => x))
    {
        var src = FindSource(uiRoot, typeName);
        if (src is null)
        {
            continue;
        }

        var srcRel = Path.GetRelativePath(uiRoot, src).Replace('\\', '/');
        var srcDir = Path.GetDirectoryName(srcRel)?.Replace('\\', '/') ?? string.Empty;
        var expected = string.IsNullOrEmpty(srcDir)
            ? $"{typeName}.resx"
            : $"{srcDir}/{typeName}.resx";

        if (!resByName.TryGetValue(typeName, out var actualPaths))
        {
            issues.Add($"MISSING: {typeName} -> Resources/{expected}");
            continue;
        }

        if (!actualPaths[0].Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"MISMATCH: {typeName} expected {expected} | actual {actualPaths[0]}");
        }
    }

    foreach (var (name, paths) in resByName.OrderBy(x => x.Key))
    {
        if (name == "SharedResource")
        {
            continue;
        }

        if (FindSource(uiRoot, name) is null)
        {
            issues.Add($"ORPHAN: {name} at {paths[0]}");
        }
    }

    return issues;
}
