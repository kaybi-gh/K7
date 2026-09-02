using Android.Media;
using K7.Server.Domain.Common;

namespace K7.Clients.MAUI.Platforms.Android.Services;

/// <summary>
/// MediaCodecList.REGULAR_CODECS profile/level/max-res tokens for Direct Play.
/// MIME presence alone advertises hevc for a Main-only decoder, then Main 10 Direct Play 1004s.
/// </summary>
internal static class AndroidMediaCodecProfiles
{
    internal static string[] Collect()
    {
        try
        {
            var list = new MediaCodecList(MediaCodecListKind.RegularCodecs);
            var infos = list.GetCodecInfos();
            if (infos is null)
                return [];

            var hevcMain = false;
            var hevcMain10 = false;
            var hevcDv = false;
            var av1Main = false;
            var av1Main10 = false;
            var hevcLevel = 0;
            var av1Level = 0;
            var hevcMaxW = 0;
            var hevcMaxH = 0;
            var av1MaxW = 0;
            var av1MaxH = 0;

            foreach (var info in infos)
            {
                if (info is null || info.IsEncoder)
                    continue;

                if (OperatingSystem.IsAndroidVersionAtLeast(29) && info.IsAlias)
                    continue;

                var types = info.GetSupportedTypes();
                if (types is null)
                    continue;

                foreach (var mime in types)
                {
                    if (string.IsNullOrEmpty(mime))
                        continue;

                    MediaCodecInfo.CodecCapabilities? caps;
                    try
                    {
                        caps = info.GetCapabilitiesForType(mime);
                    }
                    catch
                    {
                        continue;
                    }

                    if (caps is null)
                        continue;

                    if (mime.Equals("video/dolby-vision", StringComparison.OrdinalIgnoreCase))
                    {
                        hevcDv = true;
                        continue;
                    }

                    if (mime.Equals("video/hevc", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadHevc(caps, ref hevcMain, ref hevcMain10, ref hevcLevel, ref hevcMaxW, ref hevcMaxH);
                        continue;
                    }

                    if (mime.Equals("video/av01", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadAv1(caps, ref av1Main, ref av1Main10, ref av1Level, ref av1MaxW, ref av1MaxH);
                    }
                }
            }

            var tokens = new List<string>();
            if (hevcMain)
                tokens.Add(VideoDecoderProfileTokens.HevcMain);
            if (hevcMain10)
                tokens.Add(VideoDecoderProfileTokens.HevcMain10);
            if (hevcDv)
                tokens.Add(VideoDecoderProfileTokens.HevcDolbyVision);
            if (hevcLevel > 0)
                tokens.Add(VideoDecoderProfileTokens.Level("hevc", hevcLevel));
            if (hevcMaxW > 0 && hevcMaxH > 0)
                tokens.Add(VideoDecoderProfileTokens.MaxResolution("hevc", hevcMaxW, hevcMaxH));
            if (av1Main)
                tokens.Add(VideoDecoderProfileTokens.Av1Main);
            if (av1Main10)
                tokens.Add(VideoDecoderProfileTokens.Av1Main10);
            if (av1Level > 0)
                tokens.Add(VideoDecoderProfileTokens.Level("av1", av1Level));
            if (av1MaxW > 0 && av1MaxH > 0)
                tokens.Add(VideoDecoderProfileTokens.MaxResolution("av1", av1MaxW, av1MaxH));

            return [.. tokens];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-Codec] Profile probe failed: {ex}");
            return [];
        }
    }

    private static void ReadHevc(
        MediaCodecInfo.CodecCapabilities caps,
        ref bool main,
        ref bool main10,
        ref int maxLevel,
        ref int maxW,
        ref int maxH)
    {
        var levels = caps.ProfileLevels;
        if (levels is not null)
        {
            foreach (var pl in levels)
            {
                if (pl is null)
                    continue;

                var profile = (int)pl.Profile;
                if (profile == 1)
                    main = true;
                if (profile is 2 or 4096 or 8192)
                    main10 = true;

                var levelIdc = HevcCodecLevelToIdc((int)pl.Level);
                if (levelIdc > maxLevel)
                    maxLevel = levelIdc;
            }
        }

        TryGrowSize(caps, ref maxW, ref maxH);
    }

    private static void ReadAv1(
        MediaCodecInfo.CodecCapabilities caps,
        ref bool main,
        ref bool main10,
        ref int maxLevel,
        ref int maxW,
        ref int maxH)
    {
        var levels = caps.ProfileLevels;
        if (levels is not null)
        {
            foreach (var pl in levels)
            {
                if (pl is null)
                    continue;

                var profile = (int)pl.Profile;
                if (profile == 1)
                    main = true;
                if (profile is 2 or 4096 or 8192)
                    main10 = true;

                var seqLevel = Av1CodecLevelToSeqLevelIdx((int)pl.Level);
                if (seqLevel > maxLevel)
                    maxLevel = seqLevel;
            }
        }

        TryGrowSize(caps, ref maxW, ref maxH);
    }

    private static void TryGrowSize(MediaCodecInfo.CodecCapabilities caps, ref int maxW, ref int maxH)
    {
        try
        {
            var video = caps.VideoCapabilities;
            var w = RangeUpper(video?.SupportedWidths);
            var h = RangeUpper(video?.SupportedHeights);
            if (w > maxW)
                maxW = w;
            if (h > maxH)
                maxH = h;
        }
        catch
        {
        }
    }

    private static int RangeUpper(global::Android.Util.Range? range)
    {
        if (range?.Upper is Java.Lang.Number number)
            return number.IntValue();

        return 0;
    }

    // MediaCodec HEVC levels are bit flags, not general_level_idc. Convert so the
    // server can compare against ffprobe (level 4.0 -> 120).
    private static int HevcCodecLevelToIdc(int codecLevel) => codecLevel switch
    {
        0x1 or 0x2 => 30,
        0x4 or 0x8 => 60,
        0x10 or 0x20 => 63,
        0x40 or 0x80 => 90,
        0x100 or 0x200 => 93,
        0x400 or 0x800 => 120,
        0x1000 or 0x2000 => 123,
        0x4000 or 0x8000 => 150,
        0x10000 or 0x20000 => 153,
        0x40000 or 0x80000 => 156,
        0x100000 or 0x200000 => 180,
        0x400000 or 0x800000 => 183,
        0x1000000 or 0x2000000 => 186,
        _ => 0
    };

    // MediaCodec AV1 levels are bit flags; ffprobe stores seq_level_idx.
    private static int Av1CodecLevelToSeqLevelIdx(int codecLevel) => codecLevel switch
    {
        0x1 => 0,
        0x2 => 1,
        0x4 => 2,
        0x8 => 3,
        0x10 => 4,
        0x20 => 5,
        0x40 => 6,
        0x80 => 7,
        0x100 => 8,
        0x200 => 9,
        0x400 => 10,
        0x800 => 11,
        0x1000 => 12,
        0x2000 => 13,
        0x4000 => 14,
        0x8000 => 15,
        0x10000 => 16,
        0x20000 => 17,
        0x40000 => 18,
        0x80000 => 19,
        0x100000 => 20,
        0x200000 => 21,
        0x400000 => 22,
        0x800000 => 23,
        _ => 0
    };
}
