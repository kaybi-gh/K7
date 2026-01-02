namespace K7.Server.Domain.Interfaces;
public interface IFileIndexer
{
    public Task IndexAsync(Library library, CancellationToken cancellationToken);
}
