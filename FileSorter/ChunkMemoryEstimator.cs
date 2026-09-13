namespace FileSorter;

internal static class ChunkMemoryEstimator
{
    // Approximate managed-memory cost of a .NET string object itself. The text
    // is counted separately below because a string stores UTF-16 characters.
    private const long StringObjectOverheadBytes = 32;

    // Allows for the TextRecord value, the List<T> slot, and allocator/runtime
    // bookkeeping. It deliberately leaves a safety margin; it is not an exact
    // CLR layout calculation, which may vary between runtime versions.
    private const long RecordAndListOverheadBytes = 64;

    public static long Estimate(TextRecord record)
    {
        return checked(
            StringObjectOverheadBytes
            + (record.OriginalLine.Length * sizeof(char))
            + RecordAndListOverheadBytes);
    }
}
