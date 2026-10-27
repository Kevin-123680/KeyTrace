using KeyTrace.App.Infrastructure;
using KeyTrace.Core.Stats;

namespace KeyTrace.App.ViewModels;

/// <summary>五个页面的基类。</summary>
public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel(string title, string subtitle, string glyph)
    {
        Title = title;
        Subtitle = subtitle;
        Glyph = glyph;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string Glyph { get; }

    /// <summary>进入页面时重新加载数据。</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>主窗口计时器（约 2 秒一次）推送的在途数据。</summary>
    public virtual void OnLiveTick(TodayLive live)
    {
    }
}
