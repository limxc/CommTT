using System.Buffers;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Modules.SerialProvider.Protocol.Parsers;

public class CustomParser(Func<byte[], string> transform) : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.ToArray();
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, transform(arr));
    }
}
