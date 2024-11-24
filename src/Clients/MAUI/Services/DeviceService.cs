using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.MAUI.Services
{
    public class DeviceService : IDeviceService
    {
        public string GetFormFactor()
        {
            return DeviceInfo.Idiom.ToString();
        }

        public string GetPlatform()
        {
            return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
        }

        public Task<List<string>> GetSupportedCodecsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
