using System.Buffers.Binary;
using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class Fmp4OpenGopSyncTests
{
    [Test]
    public void TryDemoteCraFirstSample_ShouldClearSyncFlag_WhenFirstVclIsCra()
    {
        var segment = BuildSegment(hevcNalType: 21, firstSampleFlags: 0x0200_0000u, defaultSampleFlags: 0x0101_0000u);

        var ok = Fmp4OpenGopSync.TryDemoteCraFirstSample(segment, out var patched, out var detail);

        ok.Should().BeTrue(detail);
        detail.Should().Be("demoted-cra-sync");
        ReadFirstSampleFlags(patched).Should().Be(0x0101_0000u);
    }

    [Test]
    public void TryDemoteCraFirstSample_ShouldLeaveIdr_WhenFirstVclIsIdr()
    {
        var segment = BuildSegment(hevcNalType: 20, firstSampleFlags: 0x0200_0000u, defaultSampleFlags: 0x0101_0000u);

        var ok = Fmp4OpenGopSync.TryDemoteCraFirstSample(segment, out _, out var detail);

        ok.Should().BeFalse();
        detail.Should().Be("idr");
    }

    [Test]
    public void TryDemoteCraFirstSample_ShouldSkip_WhenAlreadyNonSync()
    {
        var segment = BuildSegment(hevcNalType: 21, firstSampleFlags: 0x0101_0000u, defaultSampleFlags: 0x0101_0000u);

        var ok = Fmp4OpenGopSync.TryDemoteCraFirstSample(segment, out _, out var detail);

        ok.Should().BeFalse();
        detail.Should().Be("already-nonsync");
    }

    private static uint ReadFirstSampleFlags(byte[] segment)
    {
        var trun = IndexOf(segment, "trun"u8);
        trun.Should().BeGreaterThan(0);
        // type at trun; payload starts +4; first_sample_flags after ver/flags+count+data_offset
        return BinaryPrimitives.ReadUInt32BigEndian(segment.AsSpan(trun + 4 + 12, 4));
    }

    private static int IndexOf(byte[] data, ReadOnlySpan<byte> fourcc)
    {
        for (var i = 0; i + 4 <= data.Length; i++)
        {
            if (data[i] == fourcc[0]
                && data[i + 1] == fourcc[1]
                && data[i + 2] == fourcc[2]
                && data[i + 3] == fourcc[3])
            {
                return i;
            }
        }

        return -1;
    }

    private static byte[] BuildSegment(int hevcNalType, uint firstSampleFlags, uint defaultSampleFlags)
    {
        using var tfhd = new MemoryStream();
        WriteU32(tfhd, 0x020038u);
        WriteU32(tfhd, 1);
        WriteU32(tfhd, 984);
        WriteU32(tfhd, 100);
        WriteU32(tfhd, defaultSampleFlags);

        using var tfdt = new MemoryStream();
        WriteU32(tfdt, 0x0100_0000u);
        WriteU32(tfdt, 0);
        WriteU32(tfdt, 0);

        using var trun = new MemoryStream();
        WriteU32(trun, 0x00000B05u);
        WriteU32(trun, 1);
        WriteU32(trun, 8);
        WriteU32(trun, firstSampleFlags);
        WriteU32(trun, 984);
        WriteU32(trun, 8);
        WriteU32(trun, 0);

        var mfhd = BuildBox("mfhd", [0, 0, 0, 0, 0, 0, 0, 1]);
        var traf = BuildBox(
            "traf",
            Concat(BuildBox("tfhd", tfhd.ToArray()), BuildBox("tfdt", tfdt.ToArray()), BuildBox("trun", trun.ToArray())));
        var moof = BuildBox("moof", Concat(mfhd, traf));

        var nal = new byte[6];
        nal[0] = (byte)(hevcNalType << 1);
        nal[1] = 1;
        var mdatPayload = new byte[4 + nal.Length];
        BinaryPrimitives.WriteUInt32BigEndian(mdatPayload, (uint)nal.Length);
        Buffer.BlockCopy(nal, 0, mdatPayload, 4, nal.Length);
        return Concat(moof, BuildBox("mdat", mdatPayload));
    }

    private static byte[] BuildBox(string type, byte[] payload)
    {
        var size = 8 + payload.Length;
        var box = new byte[size];
        box[0] = (byte)(size >> 24);
        box[1] = (byte)(size >> 16);
        box[2] = (byte)(size >> 8);
        box[3] = (byte)size;
        box[4] = (byte)type[0];
        box[5] = (byte)type[1];
        box[6] = (byte)type[2];
        box[7] = (byte)type[3];
        Buffer.BlockCopy(payload, 0, box, 8, payload.Length);
        return box;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    private static void WriteU32(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}
