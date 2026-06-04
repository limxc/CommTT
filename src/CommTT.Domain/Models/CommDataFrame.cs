namespace CommTT.Domain.Models;

public record CommDataFrame(DateTimeOffset Timestamp, ReadOnlyMemory<byte> Raw, string? ParsedText = null);
