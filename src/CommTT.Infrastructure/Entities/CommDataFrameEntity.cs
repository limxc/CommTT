namespace CommTT.Infrastructure.Entities;

public class CommDataFrameEntity
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public byte[] Raw { get; set; } = Array.Empty<byte>();
    public string? ParsedText { get; set; }
}
