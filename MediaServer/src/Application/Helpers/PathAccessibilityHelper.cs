using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MediaServer.Application.Helpers;

public static class PathAccessibilityHelper
{
    public static bool IsDirectoryAccessible(string path)
    {
        try
        {
            DirectoryInfo directoryInfo = new(path);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    directoryInfo.UnixFileMode = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.SetUser | UnixFileMode.SetGroup;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    DirectorySecurity security = directoryInfo.GetAccessControl();
                    security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                    directoryInfo.SetAccessControl(security);
                }
            }

            return directoryInfo.IsWritable();
        }
        catch
        {
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
