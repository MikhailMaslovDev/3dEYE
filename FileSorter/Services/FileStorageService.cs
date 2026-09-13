using System.IO;
using System.Text;

namespace FileSorter;

internal sealed class FileStorageService
{
    internal const int StreamBufferSize = 64 * 1024;

    // Both StreamReader and StreamWriter use a FileStream plus their own
    // byte/character buffers. Four configured buffers reserve 256 KiB.
    internal const long TemporaryStreamMemoryReservationBytes =
        (long)StreamBufferSize * 4;

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly TextRecordParser _recordParser = new();

    public string CreateWorkingDirectory(string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("Output directory cannot be determined.");

        var directoryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputPath)}.sorting-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    public async IAsyncEnumerable<Chunk> ReadChunksAsync(
        ValidatedSortSettings settings,
        InvalidLinesLogger invalidLinesLogger,
        IProgress<SortingStatus>? progress,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var inputStream = new FileStream(
            settings.InputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(
            inputStream,
            Utf8WithoutBom,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: StreamBufferSize,
            leaveOpen: false);

        var records = new List<TextRecord>();
        long estimatedMemoryBytes = 0;
        long chunkIndex = 0;
        long lineNumber = 0;
        long processedBytes = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } originalLine)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lineNumber++;
            processedBytes += Utf8WithoutBom.GetByteCount(originalLine) + 1;

            if (!_recordParser.TryParse(originalLine, lineNumber, out var record, out var errorMessage))
            {
                await invalidLinesLogger.WriteAsync(
                    lineNumber,
                    errorMessage,
                    originalLine,
                    cancellationToken);
                continue;
            }

            var recordMemoryBytes = ChunkMemoryEstimator.Estimate(record);

            if (records.Count > 0
                && estimatedMemoryBytes + recordMemoryBytes > settings.MemoryPerWorkerInBytes)
            {
                yield return new Chunk(chunkIndex++, records);
                records = new List<TextRecord>();
                estimatedMemoryBytes = 0;
            }

            records.Add(record);
            estimatedMemoryBytes += recordMemoryBytes;

            if (lineNumber % ExternalFileSorter.ProgressReportIntervalInLines == 0)
            {
                progress?.Report(new SortingStatus(
                    SortingPhase.ReadingInput,
                    lineNumber,
                    processedBytes,
                    TemporaryFileCount: 0,
                    BytesPerSecond: 0));
            }
        }

        if (records.Count > 0)
        {
            yield return new Chunk(chunkIndex, records);
        }
    }

    public async Task<SortedTemporaryFile> SaveChunkAsync(
        Chunk chunk,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var filePath = GetChunkFilePath(workingDirectory, chunk.Index);

        await using var session = new TemporaryFileSession(filePath, _recordParser);

        foreach (var record in chunk.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await session.WriteTemporaryRecordAsync(record, cancellationToken);
        }

        await session.CompleteAsync(cancellationToken);

        var maximumRecordMemoryBytes = chunk.Records.Count == 0
            ? 0
            : chunk.Records.Max(ChunkMemoryEstimator.Estimate);

        return new SortedTemporaryFile(chunk.Index, filePath, maximumRecordMemoryBytes);
    }

    public TemporaryFileSession OpenTemporaryFileSession(string outputFilePath)
    {
        return new TemporaryFileSession(outputFilePath, _recordParser);
    }

    public string GetMergeFilePath(string workingDirectory, int passIndex, long firstInputIndex)
    {
        return Path.Combine(
            workingDirectory,
            $"merge-{passIndex:D4}-{firstInputIndex:D8}.tmp");
    }

    public string GetEmptyResultFilePath(string workingDirectory)
    {
        return Path.Combine(workingDirectory, "empty-result.tmp");
    }

    public Task DeleteFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task MoveResultAsync(
        string temporaryFilePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temporaryFilePath, outputPath);
        return Task.CompletedTask;
    }

    private static string GetChunkFilePath(string workingDirectory, long chunkIndex)
    {
        return Path.Combine(workingDirectory, $"chunk-{chunkIndex:D8}.tmp");
    }
}
