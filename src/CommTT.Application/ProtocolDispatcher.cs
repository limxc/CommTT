using System.IO.Pipelines;
using CommTT.Application.Interfaces;
using CommTT.Application.Logging;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CommTT.Application;

public class ProtocolDispatcher
{
    private readonly IProtocolSplitter _splitter;
    private readonly IProtocolParser _parser;
    private readonly IMetricsAggregator _metrics;
    private readonly IAlertEngine _alert;
    private readonly ILogger<ProtocolDispatcher> _logger;
    private readonly ProtocolExceptionLogger _exceptionLogger;

    public event EventHandler<CommEventArgs>? FrameDispatched;

    public ProtocolDispatcher(
        IProtocolSplitter splitter,
        IProtocolParser parser,
        IMetricsAggregator metrics,
        IAlertEngine alert,
        ILogger<ProtocolDispatcher> logger,
        ProtocolExceptionLogger exceptionLogger)
    {
        _splitter = splitter;
        _parser = parser;
        _metrics = metrics;
        _alert = alert;
        _logger = logger;
        _exceptionLogger = exceptionLogger;
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
                    try
                    {
                        var dataFrame = _parser.Parse(frame);
                        _metrics.RecordFrame(dataFrame);
                        _alert.Check(dataFrame);
                        FrameDispatched?.Invoke(this, new CommEventArgs { Frame = dataFrame });
                    }
                    catch (Exception ex)
                    {
                        var length = Math.Min((int)frame.Length, 128);
                        var rawBytes = new byte[length];
                        var pos = 0;
                        foreach (var segment in frame.Slice(0, length))
                        {
                            segment.Span.CopyTo(rawBytes.AsSpan(pos));
                            pos += segment.Length;
                        }
                        var rawHex = Convert.ToHexString(rawBytes);
                        _exceptionLogger.Write("Unknown", "", ex.GetType().Name, ex.Message, "ProtocolDispatcher.Parse", rawHex);
                        _logger.LogError(ex, "Frame parse error");
                        throw;
                    }

                    reader.AdvanceTo(consumed, buffer.End);
                    buffer = buffer.Slice(consumed);
                }

                reader.AdvanceTo(buffer.End);
            }
        }
        catch (Exception ex)
        {
            _exceptionLogger.Write("Unknown", "", ex.GetType().Name, ex.Message, "ProtocolDispatcher.OnBufferRead", "");
            _logger.LogError(ex, "Buffer read error");
            reader.Complete(ex);
            throw;
        }
    }
}
