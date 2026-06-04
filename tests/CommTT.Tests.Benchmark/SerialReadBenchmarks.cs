using System.Buffers;
using BenchmarkDotNet.Attributes;
using CommTT.Modules.SerialProvider.Protocol.Parsers;
using CommTT.Modules.SerialProvider.Protocol.Splitters;

namespace CommTT.Tests.Benchmark;

[MemoryDiagnoser]
public class SerialReadBenchmarks
{
    private readonly byte[] _data = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();

    [Benchmark]
    public void FixedLengthSplitter_1K()
    {
        var splitter = new FixedLengthSplitter(1024);
        var seq = new ReadOnlySequence<byte>(_data);
        splitter.TrySplit(seq, out _, out _);
    }

    [Benchmark]
    public void HexParser_1K()
    {
        var parser = new HexParser();
        var seq = new ReadOnlySequence<byte>(_data);
        parser.Parse(seq);
    }
}
