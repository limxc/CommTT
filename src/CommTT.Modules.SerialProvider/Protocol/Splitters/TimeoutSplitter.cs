using System.Buffers;
using CommTT.Domain.Interfaces;

namespace CommTT.Modules.SerialProvider.Protocol.Splitters;

public class TimeoutSplitter(TimeSpan timeout) : IProtocolSplitter
{
    private DateTimeOffset _last = DateTimeOffset.MinValue;
    public bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed)
    {
        frame = default;
        consumed = buffer.Start;
        if (buffer.IsEmpty) return false;
        var now = DateTimeOffset.UtcNow;
        if ((now - _last) > timeout && _last != DateTimeOffset.MinValue)
        {
            frame = buffer.Slice(0, buffer.Length);
            consumed = buffer.End;
            _last = now;
            return true;
        }
        _last = now;
        return false;
    }
}
