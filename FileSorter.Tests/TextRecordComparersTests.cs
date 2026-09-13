namespace FileSorter.Tests;

public sealed class TextRecordComparersTests
{
    [Fact]
    public void StableOrdinal_OrdersByTextThenNumberThenSourceSequence()
    {
        var records = new[]
        {
            Record("2.a", 4),
            Record("1.a", 3),
            Record("01.a", 2),
            Record("1.Z", 1),
            Record("1.a", 1)
        };

        Array.Sort(records, TextRecordComparers.StableOrdinal);

        Assert.Equal(
            new long[] { 1, 1, 2, 3, 4 },
            records.Select(record => record.SourceSequence));
        Assert.Equal("1.Z", records[0].OriginalLine);
    }

    [Fact]
    public void CancellableStableOrdinal_ThrowsForCancelledToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var comparer = TextRecordComparers.CreateCancellableStableOrdinal(
            cancellation.Token);

        Assert.Throws<OperationCanceledException>(() =>
            comparer.Compare(Record("1.a", 1), Record("2.a", 2)));
    }

    private static TextRecord Record(string line, long sourceSequence)
    {
        var parser = new TextRecordParser();
        Assert.True(parser.TryParse(line, sourceSequence, out var record, out _));
        return record;
    }
}
