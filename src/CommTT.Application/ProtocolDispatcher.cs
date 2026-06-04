using System.Buffers;
using System.IO.Pipelines;
using CommTT.Application.Interfaces;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Application;

public class ProtocolDispatcher
{
    private readonly IProtocolSplitter _splitter;
    private readonly IProtocolParser _parser;
    private readonly IMetricsAggregator _metrics;
    private readonly IAlertEngine _alert;

    public event EventHandler<CommEventArgs>? FrameDispatched;

    public ProtocolDispatcher(
        IProtocolSplitter splitter,
        IProtocolParser parser,
        IMetricsAggregator metrics,
        IAlertEngine alert)
    {
        _splitter = splitter;
        _parser = parser;
        _metrics = metrics;
        _alert = alert;
    }

    public void OnBufferRead(PipeReader reader)
    {
        try
        {
            while (reader.TryRead(out var readResult))
            {
                var buffer = readResult.Buffer;

                while (_splitter.TrySplit(buffer, out var frame, out var consumed))
                {
                    var dataFrame = _parser.Parse(frame);
                    _metrics.RecordFrame(dataFrame);
                    _alert.Check(dataFrame);
                    FrameDispatched?.Invoke(this, new CommEventArgs { Frame = dataFrame });

                    reader.AdvanceTo(consumed, buffer.End);
                    buffer = buffer.Slice(consumed);
                }

                reader.AdvanceTo(buffer.End);
            }
        }
        catch (Exception ex)
        {
            reader.Complete(ex);
            throw;
        }
    }
}
