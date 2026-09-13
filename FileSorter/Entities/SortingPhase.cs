namespace FileSorter;

internal enum SortingPhase
{
    Preparing,
    ReadingInput,
    SortingChunk,
    MergingFiles,
    Completed,
    Cancelled
}
