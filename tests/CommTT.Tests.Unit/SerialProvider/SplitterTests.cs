using System.Buffers;
using CommTT.Modules.SerialProvider.Protocol.Splitters;

namespace CommTT.Tests.Unit.SerialProvider;

public class SplitterTests
{
    [Theory]
    [InlineData(new byte[] { 0x01, 0x02, 0x03 }, 3, true)]
    [InlineData(new byte[] { 0x01 }, 3, false)]
    public void FixedLengthSplitter_Tests(byte[] data, int length, bool expected)
    {
        var splitter = new FixedLengthSplitter(length);
        var buffer = new ReadOnlySequence<byte>(data);
        bool result = splitter.TrySplit(buffer, out _, out _);
        result.Should().Be(expected);
    }

    [Fact]
    public void DelimiterSplitter_Should_Split_On_Delimiter()
    {
        var splitter = new DelimiterSplitter(new byte[] { 0x0D, 0x0A });
        var buffer = new ReadOnlySequence<byte>(new byte[] { 0x01, 0x02, 0x0D, 0x0A, 0x03 });
        bool result = splitter.TrySplit(buffer, out _, out _);
        result.Should().BeTrue();
    }

    [Fact]
    public void DelimiterSplitter_Fuzz_RandomData_Should_Not_Throw()
    {
        var rng = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            var bytes = new byte[rng.Next(0, 256)];
            rng.NextBytes(bytes);
            var splitter = new DelimiterSplitter(new byte[] { 0x0D, 0x0A });
            var buffer = new ReadOnlySequence<byte>(bytes);
            var act = () => splitter.TrySplit(buffer, out _, out _);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void DelimiterSplitter_Should_Handle_Short_Buffer()
    {
        var splitter = new DelimiterSplitter(new byte[] { 0x0D, 0x0A });
        var buffer = new ReadOnlySequence<byte>(new byte[] { 0x01 });
        bool result = splitter.TrySplit(buffer, out _, out _);
        result.Should().BeFalse();
    }

    [Fact]
    public void TimeoutSplitter_Should_Timeout_After_Delay()
    {
        var splitter = new TimeoutSplitter(TimeSpan.FromMilliseconds(1));
        var buffer = new ReadOnlySequence<byte>(new byte[] { 0x01, 0x02 });
        splitter.TrySplit(buffer, out _, out _);
        Thread.Sleep(5);
        var result = splitter.TrySplit(buffer, out var frame, out var _);
        result.Should().BeTrue();
        frame.Length.Should().Be(2);
    }
}
