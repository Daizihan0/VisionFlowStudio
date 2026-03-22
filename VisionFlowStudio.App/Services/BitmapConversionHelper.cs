using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace VisionFlowStudio.App.Services;

public static class BitmapConversionHelper
{
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = memory;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }

    public static string ToPngBase64(BitmapSource bitmapSource)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

        using var memory = new MemoryStream();
        encoder.Save(memory);
        return Convert.ToBase64String(memory.ToArray());
    }

    public static BitmapSource? FromClipboard()
    {
        if (!System.Windows.Clipboard.ContainsImage())
        {
            return null;
        }

        var image = System.Windows.Clipboard.GetImage();
        image?.Freeze();
        return image;
    }

    public static Bitmap ToBitmap(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        using var memory = new MemoryStream(bytes);
        using var original = new Bitmap(memory);
        return new Bitmap(original);
    }

    public static Mat ToMat(string base64)
    {
        using var bitmap = ToBitmap(base64);
        return BitmapConverter.ToMat(bitmap);
    }
}
