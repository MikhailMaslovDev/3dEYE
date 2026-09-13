namespace FileSorter;

internal sealed record SortingStatus(
    SortingPhase Phase,
    long ProcessedLines,
    long ProcessedBytes,
    int TemporaryFileCount,
    double BytesPerSecond);
