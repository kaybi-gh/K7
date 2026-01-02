using K7.Clients.MAUI.Interfaces;
using UIKit;

namespace K7.Clients.MAUI.Platforms.MacCatalyst.Services;

public class CodecService : ICodecService
{
    public Task<bool> GetHdrSupportAsync()
    {
        var screen = UIScreen.MainScreen;
        return Task.FromResult(screen.TraitCollection.DisplayGamut == UIDisplayGamut.P3);
    }
}
