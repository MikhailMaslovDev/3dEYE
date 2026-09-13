namespace FileSorter;

internal readonly record struct TextRecord(
    string OriginalLine,
    int StringStartIndex,
    int Number,
    long SourceSequence);
