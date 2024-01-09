namespace MediaServer.Domain.Interfaces;
public interface IFileIndexerService
{
    public Task IndexAsync(Library library, CancellationToken cancellationToken);
}
