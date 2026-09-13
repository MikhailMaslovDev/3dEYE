using System.IO;

namespace FileSorter;

internal sealed record SortSettings(
    string InputPath,
    string OutputPath,
    string MemoryPerWorkerInMegabytes,
    string WorkerCount)
{
    private const long BytesPerMegabyte = 1024L * 1024L;

    public ValidatedSortSettings Validate()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            throw new ArgumentException("Input file is required.", nameof(InputPath));
        }

        var fullInputPath = Path.GetFullPath(InputPath);

        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException("Input file does not exist.", fullInputPath);
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            throw new ArgumentException("Output file is required.", nameof(OutputPath));
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

        if (string.Equals(fullInputPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Input and output files must be different.");
        }

        if (!long.TryParse(MemoryPerWorkerInMegabytes, out var memoryPerWorkerInMegabytes)
            || memoryPerWorkerInMegabytes <= 0)
        {
            throw new ArgumentException(
                "Memory per worker must be a positive whole number of megabytes.",
                nameof(MemoryPerWorkerInMegabytes));
        }

        long memoryPerWorkerInBytes;
        long mergeMemoryBudgetInBytes;

        try
        {
            memoryPerWorkerInBytes = checked(memoryPerWorkerInMegabytes * BytesPerMegabyte);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "Memory per worker is too large.",
                nameof(MemoryPerWorkerInMegabytes),
                exception);
        }

        if (!int.TryParse(WorkerCount, out var workerCount) || workerCount <= 0)
        {
            throw new ArgumentException(
                "Worker count must be a positive whole number.",
                nameof(WorkerCount));
        }

        try
        {
            mergeMemoryBudgetInBytes = checked(memoryPerWorkerInBytes * workerCount);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "Total memory budget is too large.",
                nameof(WorkerCount),
                exception);
        }

        return new ValidatedSortSettings(
            fullInputPath,
            fullOutputPath,
            memoryPerWorkerInBytes,
            workerCount,
            mergeMemoryBudgetInBytes);
    }
}

internal sealed record ValidatedSortSettings(
    string InputPath,
    string OutputPath,
    long MemoryPerWorkerInBytes,
    int WorkerCount,
    long MergeMemoryBudgetInBytes);
