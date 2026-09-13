namespace FileSorter.Tests;

public sealed class TextRecordParserTests
{
    private readonly TextRecordParser _parser = new();

    [Theory]
    [InlineData("0.x", 0, 2)]
    [InlineData("2147483647.x", 2147483647, 11)]
    [InlineData("001.x", 1, 4)]
    [InlineData("1. text", 1, 2)]
    [InlineData("1.a.b", 1, 2)]
    public void TryParse_ValidLine_ReturnsRecord(
        string line,
        int expectedNumber,
        int expectedStringStartIndex)
    {
        var parsed = _parser.TryParse(line, 42, out var record, out var errorMessage);

        Assert.True(parsed);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(line, record.OriginalLine);
        Assert.Equal(expectedNumber, record.Number);
        Assert.Equal(expectedStringStartIndex, record.StringStartIndex);
        Assert.Equal(42, record.SourceSequence);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".x")]
    [InlineData("x")]
    [InlineData("1")]
    [InlineData("-1.x")]
    [InlineData("+1.x")]
    [InlineData(" 1.x")]
    [InlineData("1 .x")]
    [InlineData("2147483648.x")]
    [InlineData("1.")]
    public void TryParse_InvalidLine_ReturnsReason(string line)
    {
        var parsed = _parser.TryParse(line, 1, out _, out var errorMessage);

        Assert.False(parsed);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }
}
