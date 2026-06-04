using System.Buffers;
using CommTT.Modules.SerialProvider.Protocol.Parsers;

namespace CommTT.Tests.Unit.SerialProvider;

public class ParserTests
{
    [Fact]
    public void HexParser_Should_Format_Correctly()
    {
        var parser = new HexParser();
        var seq = new ReadOnlySequence<byte>(new byte[] { 0xAB, 0xCD });
        var frame = parser.Parse(seq);
        frame.ParsedText.Should().Be("AB CD");
    }

    [Fact]
    public void TextParser_Should_Decode_ASCII()
    {
        var parser = new TextParser(System.Text.Encoding.ASCII);
        var seq = new ReadOnlySequence<byte>(System.Text.Encoding.ASCII.GetBytes("Hello"));
        var frame = parser.Parse(seq);
        frame.ParsedText.Should().Be("Hello");
    }

    [Fact]
    public void PassThroughParser_Should_Return_Null_Text()
    {
        var parser = new PassThroughParser();
        var seq = new ReadOnlySequence<byte>(new byte[] { 0x01 });
        var frame = parser.Parse(seq);
        frame.ParsedText.Should().BeNull();
    }
}
