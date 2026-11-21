namespace Wessl.App.Services;

public sealed class ContainerPolling(ContainerService containers, AppSettings? settings = null)
{
    private CancellationTokenSource? _visibleSession;

    public TimeSpan ActiveRefreshInterval =>
        TimeSpan.FromSeconds(settings?.RefreshIntervalSeconds ?? 10);

    public TimeSpan StatsRefreshInterval =>
        TimeSpan.FromSeconds(settings?.StatsIntervalSeconds ?? 1);

    public void Start()
    {
        if (_visibleSession is not null)
        {
            return;
        }

        _visibleSession = new CancellationTokenSource();
        _ = containers.RefreshAsync(_visibleSession.Token);
    }

    public void Stop()
    {
        var session = _visibleSession;
        _visibleSession = null;
        if (session is not null)
        {
            session.Cancel();
            session.Dispose();
            containers.StopUsagePolling();
        }
    }

    public void TickStats()
    {
        if (_visibleSession is { } session)
        {
            _ = containers.RefreshUsagesAsync(session.Token);
        }
    }

    public void Tick()
    {
        if (_visibleSession is { } session)
        {
            _ = containers.RefreshAsync(session.Token, skipIfBusy: true);
        }
    }

    public Task RefreshAsync() =>
        containers.RefreshAsync(_visibleSession?.Token ?? CancellationToken.None, skipIfBusy: true);
}
