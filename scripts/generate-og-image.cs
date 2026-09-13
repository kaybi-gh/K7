#:package SkiaSharp@4.151.2
#:package SkiaSharp.NativeAssets.Win32@4.151.2
#:package Svg.Skia@5.2.3
#:property ManagePackageVersionsCentrally=false

using SkiaSharp;
using Svg.Skia;

var repoRoot = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
var svgPath = Path.Combine(repoRoot, "branding", "og-image.svg");
var pngPath = Path.Combine(repoRoot, "src", "Clients", "Shared", "UI", "wwwroot", "assets", "og-image.png");

using var svg = new SKSvg();
if (svg.Load(svgPath) is null || svg.Picture is null)
    throw new InvalidOperationException($"Failed to load {svgPath}");

const int width = 1200;
const int height = 630;
var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
using var surface = SKSurface.Create(info);
var canvas = surface.Canvas;
canvas.Clear(new SKColor(0x0d, 0x09, 0x07));
canvas.DrawPicture(svg.Picture);
canvas.Flush();

using var image = surface.Snapshot();
using var data = image.Encode(SKEncodedImageFormat.Png, 100)
    ?? throw new InvalidOperationException("Failed to encode OG PNG");
Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
using var stream = File.Open(pngPath, FileMode.Create, FileAccess.Write, FileShare.None);
data.SaveTo(stream);
Console.WriteLine(pngPath);
