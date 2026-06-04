using System.Buffers;
using System.Text;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Modules.SerialProvider.Protocol.Parsers;

public class TextParser(Encoding encoding) : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.IsSingleSegment ? frame.First.Span.ToArray() : frame.ToArray();
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, encoding.GetString(arr));
    }
}
