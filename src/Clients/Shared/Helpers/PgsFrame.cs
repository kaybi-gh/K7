namespace K7.Clients.Shared.Helpers;

/// <summary>
/// One PGS screen: what is on the subtitle plane from <see cref="TimeSeconds"/> until the
/// next frame replaces it. A frame without objects clears the plane.
/// </summary>
public sealed record PgsFrame(
    double TimeSeconds,
    int PlaneWidth,
    int PlaneHeight,
    IReadOnlyList<PgsFrameObject> Objects)
{
    public bool IsClear => Objects.Count == 0;
}

/// <summary>
/// A decoded bitmap placed on the subtitle plane. <see cref="Rgba"/> is straight
/// (not premultiplied) RGBA, 4 bytes per pixel, row by row.
/// </summary>
public sealed record PgsFrameObject(int X, int Y, int Width, int Height, byte[] Rgba);
