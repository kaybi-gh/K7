using NUnit.Framework;

namespace MediaServer.Tests.Helpers.Fixtures;

[TestFixture]
public class FileFixture
{
    public static readonly string TestDirectoryPath = Path.Combine(Path.GetTempPath(), "TestDirectory");

    [OneTimeSetUp]
    protected void RunBeforeAnyTests()
    {
        Directory.CreateDirectory(TestDirectoryPath);
    }

    [OneTimeTearDown]
    protected void RunAfterAnyTests()
    {
        Directory.Delete(TestDirectoryPath, true);
    }

    protected static List<FileInfo> CreateTestFiles()
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
            files.Add(CreateFile(subDirectoryPath, $"SubTestFile{i}.txt", $"Subdirectory test content {i}"));
        }

        return files;
    }
    protected static FileInfo CreateTestFile(string fileName, string content)
    {
        return CreateFile(TestDirectoryPath, fileName, content);
    }

    private static FileInfo CreateFile(string path, string fileName, string content)
    {
        string filePath = Path.Combine(path, fileName);
        File.WriteAllText(filePath, content);
        return new FileInfo(filePath);
    }
}
