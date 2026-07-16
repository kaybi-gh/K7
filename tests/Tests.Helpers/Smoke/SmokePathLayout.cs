namespace K7.Tests.Helpers.Smoke;

public sealed class SmokePathLayout : IDisposable
{
    public string Root { get; }
    public string Config { get; }
    public string Metadatas { get; }
    public string Logs { get; }
    public string Transcoding { get; }
    public string DatabaseName { get; }

    public SmokePathLayout()
    {
        Root = Path.Combine(Path.GetTempPath(), "k7-smoke-" + Guid.NewGuid().ToString("N"));
        Config = Path.Combine(Root, "config");
        Metadatas = Path.Combine(Root, "metadatas");
        Logs = Path.Combine(Root, "logs");
        Transcoding = Path.Combine(Root, "transcoding");
        DatabaseName = Path.Combine(Root, "k7");

        Directory.CreateDirectory(Config);
        Directory.CreateDirectory(Path.Combine(Config, "openiddict-keys"));
        Directory.CreateDirectory(Path.Combine(Config, "dataprotection-keys"));
        Directory.CreateDirectory(Metadatas);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Transcoding);
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
            return;

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // SQLite file handles may still be releasing on Windows after host shutdown.
        }
    }
}
