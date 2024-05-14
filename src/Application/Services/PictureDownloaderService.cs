namespace MediaServer.Application.Services;

public static class PictureDownloaderService
{
    public static async Task<bool> TryDownloadPictureAsync(string imageUri, string destinationPath)
    {
        try
        {
            using var httpClient = new HttpClient();
            byte[] imageData = await httpClient.GetByteArrayAsync(imageUri);
            FileInfo file = new(destinationPath);
            file.Directory!.Create();
            File.WriteAllBytes(destinationPath, imageData);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
