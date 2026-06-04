using System.Buffers;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Modules.SerialProvider.Protocol.Parsers;

public class HexParser : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.IsSingleSegment ? frame.First.Span.ToArray() : frame.ToArray();
        var text = BitConverter.ToString(arr).Replace("-", " ");
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, text);
    }
}
