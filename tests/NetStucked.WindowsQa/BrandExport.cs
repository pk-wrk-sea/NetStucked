using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetStucked.Desktop.Services;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    // Packaging the unchanged brand through WPF's normal display pipeline.
    private static int ExportBrand(string directory)
    {
        _ = new Application(); Directory.CreateDirectory(directory);
        int[] sizes = [16, 20, 24, 32, 48, 64, 128, 256];
        byte[][] frames = sizes.Select(size => Png(RenderBrand(size, size, false))).ToArray();
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "NetStucked.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
            }
            foreach (var frame in frames) writer.Write(frame);
        }
        File.WriteAllBytes(Path.Combine(directory, "WizardMark.png"), Png(RenderBrand(256, 256, false)));
        File.WriteAllBytes(Path.Combine(directory, "WizardLogo.png"), Png(RenderBrand(328, 628, true)));
        File.WriteAllBytes(Path.Combine(directory, "NetStuckedBanner.png"), Png(RenderBrand(640, 400, true)));
        var decoder = BitmapDecoder.Create(new Uri(Path.GetFullPath(Path.Combine(directory, "NetStucked.ico"))), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Require(decoder.Frames.Count == sizes.Length && decoder.Frames.All(f => f.PixelWidth == f.PixelHeight), "Windows icon decodes all eight standard sizes");
        Console.WriteLine("Brand packaging exported from the original PNG; no logo shape or text was redrawn.");
        return 0;
    }
    private static RenderTargetBitmap RenderBrand(int width, int height, bool full)
    {
        var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var drawing = visual.RenderOpen())
        {
            if (full) drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 248, 252)), null, new Rect(0, 0, width, height));
            var image = full ? BrandAssets.Logo : BrandAssets.Mark;
            double scale = Math.Min(width * (full ? .90 : .94) / image.Width, height * (full ? .86 : .94) / image.Height);
            double w = image.Width * scale, h = image.Height * scale;
            drawing.DrawImage(image, new Rect((width - w) / 2, (height - h) / 2, w, h));
        }
        var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); result.Render(visual); return result;
    }
    private static byte[] Png(BitmapSource source)
    { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using var output = new MemoryStream(); encoder.Save(output); return output.ToArray(); }
}
