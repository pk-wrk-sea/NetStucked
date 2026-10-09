using System.Windows;
using System.Windows.Media.Imaging;

namespace NetStucked.Desktop.Services;

public static class BrandAssets
{
    public static BitmapSource Logo { get; } = Load();
    public static BitmapSource Mark { get; } = CropMark();
    public static BitmapFrame Icon { get; } = LoadIcon();
    private static BitmapFrame LoadIcon()
    {
        var icon = BitmapFrame.Create(new Uri("pack://application:,,,/NetStucked;component/Resources/Brand/NetStucked.ico"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        icon.Freeze(); return icon;
    }
    private static BitmapSource Load()
    {
        var logo = new BitmapImage(new Uri("pack://application:,,,/NetStucked;component/Resources/Brand/NetStuckedLogo.png"));
        logo.Freeze(); return logo;
    }
    private static BitmapSource CropMark()
    {
        // Present the supplied symbol without altering the canonical source PNG.
        var mark = new CroppedBitmap(Logo, new Int32Rect(360, 114, 680, 490));
        mark.Freeze(); return mark;
    }
}
