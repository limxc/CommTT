using System.Buffers;

namespace CommTT.Domain.Interfaces;

public interface IProtocolSplitter
{
    bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed);
}
