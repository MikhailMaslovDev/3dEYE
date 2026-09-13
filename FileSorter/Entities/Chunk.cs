namespace FileSorter;

internal sealed record Chunk(
    long Index,
    List<TextRecord> Records);
