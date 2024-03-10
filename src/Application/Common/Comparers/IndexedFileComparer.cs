using MediaServer.Domain.Entities;

namespace MediaServer.Application.Common.Comparers;
public class IndexedFileComparer : IEqualityComparer<IndexedFile>
{
    public bool Equals(IndexedFile? x, IndexedFile? y)
    {
        return x?.Path == y?.Path && x?.Hash == y?.Hash && x?.Size == y?.Size;
    }

    public int GetHashCode(IndexedFile obj)
        => obj.Hash.GetHashCode();
}
