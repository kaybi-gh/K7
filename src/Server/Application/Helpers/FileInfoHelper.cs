using System.Collections.Concurrent;

namespace K7.Server.Application.Helpers;
public static class FileInfoHelper
{
    public static List<FileInfo> GetAllFileInfosRecursively(string rootDirectory)
    {
        ConcurrentBag<FileInfo> fileInfos = [];

        try
        {
            if (!Directory.Exists(rootDirectory))
            {
                throw new Exception();
            }

            var allFiles = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories);
            //.Where(f => f.LastWriteTime >= since || f.CreationTime >= since); // TODO - Only get latest?
            Parallel.ForEach(allFiles, filePath => fileInfos.Add(new FileInfo(filePath)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving files in directory {rootDirectory}: {ex.Message}");
        }

        return [.. fileInfos];
    }
}
