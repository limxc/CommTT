using CommTT.Application.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Application.Services;

public class MetricsAggregator : IMetricsAggregator
{
    private long _totalBytes;
    private long _totalFrames;
    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

    public void RecordFrame(CommDataFrame frame)
    {
        Interlocked.Add(ref _totalBytes, frame.Raw.Length);
        Interlocked.Increment(ref _totalFrames);
    }
    public long TotalBytes => Interlocked.Read(ref _totalBytes);
    public long TotalFrames => Interlocked.Read(ref _totalFrames);
    public double BytesPerSecond => _sw.Elapsed.TotalSeconds > 0 ? TotalBytes / _sw.Elapsed.TotalSeconds : 0;
}
