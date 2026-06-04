using System.Buffers;
using CommTT.Domain.Interfaces;

namespace CommTT.Modules.SerialProvider.Protocol.Splitters;

public class DelimiterSplitter(byte[] delimiter) : IProtocolSplitter
{
    public bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed)
    {
        frame = default; consumed = buffer.Start;
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadTo(out ReadOnlySequence<byte> _, delimiter))
        {
            return true;
        }
        return false;
    }
}
