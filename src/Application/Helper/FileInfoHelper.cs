namespace MediaServer.Application.Helper;
public static class FileInfoHelper
{
    public static List<FileInfo> GetAllFileInfosRecursively(string rootDirectory)
    {
        List<FileInfo> fileInfos = [];
        Stack<string> directoriesToProcess = new();
        directoriesToProcess.Push(rootDirectory);

        while (directoriesToProcess.Count > 0)
        {
            string currentDirectory = directoriesToProcess.Pop();

            try
            {
                foreach (string filePath in Directory.GetFiles(currentDirectory))
                {
                    fileInfos.Add(new FileInfo(filePath));
                }

                foreach (string subDirectory in Directory.GetDirectories(currentDirectory))
                {
                    directoriesToProcess.Push(subDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving files in directory {currentDirectory}: {ex.Message}");
            }
        }

        return fileInfos;
    }
}
