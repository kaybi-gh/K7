namespace MediaClient.Shared.Models;

public class MediaItem
{
    public required string Id { get; set; }
    public string Title { get; set; } = "";
    public string PosterPicture { get; set; } = "";
    public string BackgroundPicture { get; set; } = "";
    public string AdditionalInformations { get; set; } = "";
    public bool Watched { get; set; } = false;
    public double Progress { get; set; } = 0;
    public int Duration { get; set; } = 0;
    public int Rating { get; set; } = 0;
    public string Synopsis { get; set; } = "";
}
