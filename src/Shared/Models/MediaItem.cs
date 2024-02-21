namespace MediaClient.Shared.Models;

public class MediaItem
{
    public required string Id { get; set; }
    public string Title { get; set; } = "";
    public string Picture { get; set; } = "";
    public string AdditionalInformations { get; set; } = "";
    public bool Watched { get; set; } = false;
    public double Progress { get; set; } = 0;
}
