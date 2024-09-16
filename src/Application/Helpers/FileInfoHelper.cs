namespace MediaServer.Application.Helpers;
public static class FileInfoHelper
{
    public static List<FileInfo> GetAllFileInfosRecursively(string rootDirectory)
    {
        List<FileInfo> fileInfos = [];

        try
        {
            if (!Directory.Exists(rootDirectory))
            {
                throw new Exception();
            }

            var allFiles = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories);
                //.Where(f => f.LastWriteTime >= since || f.CreationTime >= since); // TODO - Only get latest?
            foreach (string filePath in allFiles)
            {
                fileInfos.Add(new FileInfo(filePath));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving files in directory {rootDirectory}: {ex.Message}");
        }

        return fileInfos;
    }
}
