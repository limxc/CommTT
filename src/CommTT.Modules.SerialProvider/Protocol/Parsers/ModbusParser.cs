using System.Buffers;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Modules.SerialProvider.Protocol.Parsers;

public class ModbusParser : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.ToArray();
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, $"MODBUS: {arr.Length} bytes");
    }
}
