namespace FileGenerator;

internal sealed record GenerationStatus(
    long WrittenBytes,
    long WrittenLines,
    double BytesPerSecond,
    long TargetBytes);
