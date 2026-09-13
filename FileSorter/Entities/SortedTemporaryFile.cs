namespace FileSorter;

internal sealed record SortedTemporaryFile(
    long Index,
    string Path,
    long MaximumRecordMemoryBytes);
