using CommTT.Domain.Models;

namespace CommTT.Application.Interfaces;

public interface IMetricsAggregator
{
    void RecordFrame(CommDataFrame frame);
    long TotalBytes { get; }
    long TotalFrames { get; }
    double BytesPerSecond { get; }
}
