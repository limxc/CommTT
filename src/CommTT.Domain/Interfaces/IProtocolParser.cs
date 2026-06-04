using System.Buffers;
using CommTT.Domain.Models;

namespace CommTT.Domain.Interfaces;

public interface IProtocolParser
{
    CommDataFrame Parse(ReadOnlySequence<byte> frame);
}
