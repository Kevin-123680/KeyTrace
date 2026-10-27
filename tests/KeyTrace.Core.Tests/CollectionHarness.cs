using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Collection;
using KeyTrace.Core.Input;
using KeyTrace.Core.Models;
using KeyTrace.Core.Settings;

namespace KeyTrace.Core.Tests;

/// <summary>把采集链路上所有可替换部件组装起来的测试夹具。</summary>
internal sealed class CollectionHarness : IDisposable
{
    public CollectionHarness(Action<AppSettings>? configure = null)
    {
        Temp = new TempRepository();
        Aggregator = new DataAggregator(() => new ScreenArea(0, 0, 1920, 1080));
        Clock = new FakeClock();
        Settings = new SettingsService(new MemorySettingsStore());

        if (configure is not null)
        {
            Settings.Update(configure);
        }

        Service = new CollectionService(
            Source,
            Aggregator,
            Temp.Repository,
            Settings,
            Clock,
            foreground: Foreground,
            suppressionCacheMilliseconds: 0);
    }

    public TempRepository Temp { get; }

    public DataAggregator Aggregator { get; }

    public FakeClock Clock { get; }

    public StubForegroundProvider Foreground { get; } = new();

    public FakeInputSource Source { get; } = new();

    public SettingsService Settings { get; }

    public CollectionService Service { get; }

    public DateOnly Today => DateOnly.FromDateTime(Clock.Now.DateTime);

    public void Dispose()
    {
        Service.Dispose();
        Temp.Dispose();
    }
}
