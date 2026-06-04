using CommTT.Application.Interfaces;
using Prism.Mvvm;

namespace CommTT.Modules.SerialProvider.ViewModels;

public class MonitorViewModel : BindableBase
{
    private readonly IMetricsAggregator _metrics;

    public long TotalFrames => _metrics.TotalFrames;
    public long TotalBytes => _metrics.TotalBytes;
    public double BytesPerSecond => _metrics.BytesPerSecond;

    public MonitorViewModel(IMetricsAggregator metrics)
    {
        _metrics = metrics;
    }
}
