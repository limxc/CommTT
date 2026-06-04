using CommTT.Domain.Models;

namespace CommTT.Tests.Unit.Domain;

public class CommDataFrameTests
{
    [Fact]
    public void Record_Should_Hold_Data()
    {
        var frame = new CommDataFrame(DateTimeOffset.UtcNow, new byte[] { 0x01, 0x02 }, "01 02");
        frame.Raw.Length.Should().Be(2);
        frame.ParsedText.Should().Be("01 02");
    }

    [Fact]
    public void Record_Should_Allow_Null_ParsedText()
    {
        var frame = new CommDataFrame(DateTimeOffset.UtcNow, new byte[] { 0x01 }, null);
        frame.ParsedText.Should().BeNull();
    }
}
