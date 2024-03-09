namespace MediaServer.Application.Services;

public static class PictureDownloaderService
{
    public static async Task<bool> TryDownloadImageAsync(string imageUrl, string destinationPath)
    {
        try
        {
            using var httpClient = new HttpClient();
            byte[] imageData = await httpClient.GetByteArrayAsync(imageUrl);
            File.WriteAllBytes(destinationPath, imageData);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
