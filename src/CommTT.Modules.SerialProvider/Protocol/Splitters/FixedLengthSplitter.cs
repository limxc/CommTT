using System.Buffers;
using CommTT.Domain.Interfaces;

namespace CommTT.Modules.SerialProvider.Protocol.Splitters;

public class FixedLengthSplitter(int length) : IProtocolSplitter
{
    public bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed)
    {
        frame = default; consumed = buffer.Start;
        if (buffer.Length < length) return false;
        frame = buffer.Slice(0, length);
        consumed = buffer.GetPosition(length);
        return true;
    }
}
