using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace K7.Server.Application.Helpers;

public static class PathAccessibilityHelper
{
    public static bool IsDirectoryAccessible(string path) => IsDirectoryAccessible(path, out _);

    public static bool IsDirectoryAccessible(string path, out string? error)
    {
        error = null;
        try
        {
            DirectoryInfo directoryInfo = new(path);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    directoryInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                        | UnixFileMode.SetUser | UnixFileMode.SetGroup;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    DirectorySecurity security = directoryInfo.GetAccessControl();
                    security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                    directoryInfo.SetAccessControl(security);
                }
            }

            if (!directoryInfo.IsWritable())
            {
                error = $"Directory '{path}' exists but is not writable.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Directory '{path}': {ex.Message}";
            return false;
        }
    }

    private static bool IsWritable(this DirectoryInfo directory)
    {
        try
        {
            var testPath = Path.Combine(directory.FullName, "test.txt");
            File.WriteAllText(testPath, "testFolderWriteAccess");
            File.Delete(testPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
