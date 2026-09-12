using System.IO;

namespace FileGenerator;

internal sealed record GenerationSettings(
    string OutputPath,
    string MinimumSizeInMegabytes,
    string Seed)
{
    private const long BytesPerMegabyte = 1024L * 1024L;

    public const int ProgressReportIntervalInLines = 10_000;

    public ValidatedGenerationSettings Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(OutputPath));
        }

        if (!long.TryParse(MinimumSizeInMegabytes, out var sizeInMegabytes)
            || sizeInMegabytes <= 0)
        {
            throw new ArgumentException(
                "Minimum size must be a positive whole number of megabytes.",
                nameof(MinimumSizeInMegabytes));
        }

        long minimumFileSizeBytes;

        try
        {
            minimumFileSizeBytes = checked(sizeInMegabytes * BytesPerMegabyte);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "Specified file size is too large.",
                nameof(MinimumSizeInMegabytes),
                exception);
        }

        if (!int.TryParse(Seed, out var seed))
        {
            throw new ArgumentException(
                "Random seed must be a valid Int32 value.",
                nameof(Seed));
        }

        var fullOutputPath = Path.GetFullPath(OutputPath);

        if (Directory.Exists(fullOutputPath))
        {
            throw new IOException("Output path must point to a file, not a directory.");
        }

        if (File.Exists(fullOutputPath))
        {
            throw new IOException($"Output file already exists: {fullOutputPath}");
        }

        return new ValidatedGenerationSettings(
            OutputPath,
            minimumFileSizeBytes,
            seed);
    }
}

internal sealed record ValidatedGenerationSettings(
    string OutputPath,
    long MinimumFileSizeBytes,
    int Seed);
