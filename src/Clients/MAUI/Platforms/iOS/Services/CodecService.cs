using AudioToolbox;
using K7.Clients.MAUI.Interfaces;
using K7.Shared.Dtos.Entities;
using UIKit;
using VideoToolbox;

namespace K7.Clients.MAUI.Platforms.iOS.Services;

public class CodecService : ICodecService
{
    public Task<bool> GetHdrSupportAsync()
    {
        var screen = UIScreen.MainScreen;
        return Task.FromResult(screen.TraitCollection.DisplayGamut == UIDisplayGamut.P3);
    }
}
