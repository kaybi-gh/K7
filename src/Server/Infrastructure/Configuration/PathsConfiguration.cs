namespace K7.Server.Infrastructure.Configuration;

public class PathsConfiguration
{
    public string Config { get; set; } = "";
    public string Metadatas { get; set; } = "";
    public string Logs { get; set; } = "";
    public string Transcoding { get; set; } = "";
    public string FFMpegBinaryFolder { get; set; } = "";
    public string EssentiaBinaryPath { get; set; } = "essentia_streaming_extractor_music";
}
