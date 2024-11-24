namespace K7.Clients.Shared.Domain.Interfaces;

public interface IDeviceService
{
    public string GetFormFactor();
    public string GetPlatform();
    public Task<List<string>> GetSupportedCodecsAsync();
}
