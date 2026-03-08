namespace K7.Clients.MAUI.Interfaces;

public interface ICodecService
{
    Task<bool> GetHdrSupportAsync();
    Task<string[]> GetSupportedVideoCodecsAsync();
    Task<string[]> GetSupportedAudioCodecsAsync();
    Task<string[]> GetSupportedContainersAsync();
}
