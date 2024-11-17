namespace K7.Server.Domain.Interfaces;
public interface IFileIndexerService
{
    public Task IndexAsync(Library library, CancellationToken cancellationToken);
}
