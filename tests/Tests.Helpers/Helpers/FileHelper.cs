using System.IO;

namespace MediaServer.Tests.Helpers.Helpers;

public static class FileHelper
{
    public static readonly string TestDirectoryPath = Path.Combine(Path.GetTempPath(), "TestDirectory");

    public static void CreateTestDirectory()
    {
        Directory.CreateDirectory(TestDirectoryPath);
    }

    private static FileInfo CreateFile(string path, string content)
    {
        File.WriteAllText(path, content);
        return new FileInfo(path);
    }

    public static void DeleteTestDirectory()
    {
        Directory.Delete(TestDirectoryPath, true);
    }

    public static List<FileInfo> CreateTestFiles()
    {
        var files = new List<FileInfo>();
        for (int i = 1; i <= 3; i++)
        {
            files.Add(CreateTestFile($"TestFile{i}.txt", $"Test content {i}"));
        }

        string subDirectoryPath = Path.Combine(TestDirectoryPath, "Subdirectory");
        Directory.CreateDirectory(subDirectoryPath);

        for (int i = 4; i <= 6; i++)
        {
            string filePath = Path.Combine(subDirectoryPath, $"SubTestFile{i}.txt");
            files.Add(CreateFile(filePath, $"Subdirectory test content {i}"));
        }

        return files;
    }

    public static FileInfo CreateTestFile(string relativePath, string content)
    {
        var path = Path.Combine(TestDirectoryPath, relativePath);
        if (path.Contains("\\"))
        {
            var directories = path.Split("\\");
            Directory.CreateDirectory(Path.Combine(directories.Take(directories.Count() - 1).ToArray()));
        }
        return CreateFile(path, content);
    }

    public static void DeleteTestFile(string relativePath)
    {
        var path = Path.Combine(TestDirectoryPath, relativePath);
        File.Delete(path);
    }
}
