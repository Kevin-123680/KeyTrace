using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KeyTrace.App.Services;

/// <summary>从程序集资源里加载多尺寸 ICO 中最大的那一帧。</summary>
public static class IconLoader
{
    private static ImageSource? _cached;

    public static ImageSource? Load()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            var decoder = BitmapDecoder.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames
                .OrderByDescending(static item => item.PixelWidth)
                .FirstOrDefault();

            if (frame is not null)
            {
                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                _cached = frame;
            }
        }
        catch
        {
            // 图标加载失败不应该阻止程序启动，托盘会退回系统默认图标。
        }

        return _cached;
    }
}
