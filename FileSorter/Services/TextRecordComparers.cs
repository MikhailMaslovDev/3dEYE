namespace FileSorter;

internal static class TextRecordComparers
{
    public static IComparer<TextRecord> StableOrdinal { get; } =
        Comparer<TextRecord>.Create(CompareStableOrdinal);

    public static IComparer<TextRecord> CreateCancellableStableOrdinal(
        CancellationToken cancellationToken)
    {
        return Comparer<TextRecord>.Create((left, right) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CompareStableOrdinal(left, right);
        });
    }

    private static int CompareStableOrdinal(TextRecord left, TextRecord right)
    {
        var textComparison = left.OriginalLine.AsSpan(left.StringStartIndex)
            .CompareTo(
                right.OriginalLine.AsSpan(right.StringStartIndex),
                StringComparison.Ordinal);

        if (textComparison != 0)
        {
            return textComparison;
        }

        var numberComparison = left.Number.CompareTo(right.Number);

        return numberComparison != 0
            ? numberComparison
            : left.SourceSequence.CompareTo(right.SourceSequence);
    }
}
