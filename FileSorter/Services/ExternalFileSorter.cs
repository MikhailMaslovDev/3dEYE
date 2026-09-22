using System.IO;

namespace FileSorter;

internal sealed class ExternalFileSorter
{
    internal const int ProgressReportIntervalInLines = 10_000;

    private readonly FileStorageService _fileStorage;

    public ExternalFileSorter()
        : this(new FileStorageService())
    {
    }

    public ExternalFileSorter(FileStorageService fileStorage)
    {
        _fileStorage = fileStorage;
    }

    public async Task SortAsync(
        SortSettings settings,
        IProgress<SortingStatus>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validatedSettings = settings.Validate();
        
        var workingDirectory = _fileStorage.CreateWorkingDirectory(validatedSettings.OutputPath);
        var outputDirectory = Path.GetDirectoryName(validatedSettings.OutputPath)
            ?? throw new InvalidOperationException("Output directory cannot be determined.");
        
        var invalidLinesLogFileName = $"invalid-lines-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
        var invalidLinesLogPath = Path.Combine(outputDirectory, invalidLinesLogFileName);
        using var invalidLinesLogger = InvalidLinesLogger.Create(invalidLinesLogPath);

        try
        {
            var sortedFiles = await CreateSortedTemporaryFilesAsync(
                validatedSettings,
                workingDirectory,
                invalidLinesLogger,
                progress,
                cancellationToken);

            var finalTemporaryFile = MergeAndSave(
                sortedFiles,
                validatedSettings,
                workingDirectory,
                progress,
                cancellationToken);

            _fileStorage.MoveResult(
                finalTemporaryFile.Path,
                validatedSettings.OutputPath,
                cancellationToken);

            progress?.Report(new SortingStatus(
                SortingPhase.Completed,
                ProcessedLines: 0,
                ProcessedBytes: 0,
                TemporaryFileCount: 0,
                BytesPerSecond: 0));
        }
        finally
        {
            try
            {
                if (!invalidLinesLogger.HasEntries)
                {
                    File.Delete(invalidLinesLogPath);
                }
            }
            finally
            {
                _fileStorage.DeleteDirectory(
                    workingDirectory,
                    CancellationToken.None);
            }
        }
    }

    public async Task<List<SortedTemporaryFile>> CreateSortedTemporaryFilesAsync(
        ValidatedSortSettings settings,
        string workingDirectory,
        InvalidLinesLogger invalidLinesLogger,
        IProgress<SortingStatus>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var activeSorts = new List<Task<SortedTemporaryFile>>();
        var sortedFiles = new List<SortedTemporaryFile>();

        try
        {
            foreach (var chunk in _fileStorage.ReadChunks(
                         settings,
                         invalidLinesLogger,
                         progress,
                         operationCancellation.Token))
            {
                activeSorts.Add(Task.Run(
                    () => SortAndSave(chunk, workingDirectory, operationCancellation.Token),
                    operationCancellation.Token));

                if (activeSorts.Count == settings.WorkerCount)
                {
                    progress?.Report(new SortingStatus(
                        SortingPhase.SortingChunk,
                        ProcessedLines: 0,
                        ProcessedBytes: 0,
                        TemporaryFileCount: activeSorts.Count,
                        BytesPerSecond: 0));

                    await CollectCompletedSortAsync(activeSorts, sortedFiles);
                }
            }

            while (activeSorts.Count > 0)
            {
                await CollectCompletedSortAsync(activeSorts, sortedFiles);
            }

            return sortedFiles;
        }
        catch
        {
            operationCancellation.Cancel();

            try
            {
                await Task.WhenAll(activeSorts);
            }
            catch
            {
                // The original failure is more useful to the caller.
            }

            throw;
        }
    }

    public SortedTemporaryFile SortAndSave(
        Chunk chunk,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        chunk.Records.Sort(
            TextRecordComparers.CreateCancellableStableOrdinal(cancellationToken));
        return _fileStorage.SaveChunk(chunk, workingDirectory, cancellationToken);
    }

    public SortedTemporaryFile SortAndSave(
        IReadOnlyList<SortedTemporaryFile> inputFiles,
        string outputFilePath,
        CancellationToken cancellationToken = default)
    {

        if (inputFiles.Count < 2)
        {
            return inputFiles.Count == 1 ? inputFiles[0] 
                : throw new ArgumentException("At least one sorted file is required.", nameof(inputFiles));
        }

        using (var session = _fileStorage.OpenTemporaryFileSession(outputFilePath))
        {
            var queue = new PriorityQueue<SortedTemporaryFile, TextRecord>(
                TextRecordComparers.StableOrdinal);

            foreach (var inputFile in inputFiles)
            {
                var record = session.ReadNext(
                    inputFile,
                    inputFile.Index);

                if (record is not null)
                {
                    queue.Enqueue(inputFile, record.Value);
                }
            }

            while (queue.TryDequeue(out var inputFile, out var record))
            {
                cancellationToken.ThrowIfCancellationRequested();
                session.WriteTemporaryRecord(record);

                var nextRecord = session.ReadNext(
                    inputFile,
                    inputFile.Index);

                if (nextRecord is not null)
                {
                    queue.Enqueue(inputFile, nextRecord.Value);
                }
            }

            session.Complete();
        }

        _fileStorage.DeleteFiles(
            inputFiles.Select(file => file.Path),
            CancellationToken.None);

        return new SortedTemporaryFile(
            inputFiles[0].Index,
            outputFilePath,
            inputFiles.Max(file => file.MaximumRecordMemoryBytes));
    }

    private SortedTemporaryFile MergeAndSave(
        List<SortedTemporaryFile> sortedFiles,
        ValidatedSortSettings settings,
        string workingDirectory,
        IProgress<SortingStatus>? progress,
        CancellationToken cancellationToken)
    {
        if (sortedFiles.Count == 0)
        {
            var emptyResultFilePath = _fileStorage.GetEmptyResultFilePath(workingDirectory);

            using var session = _fileStorage.OpenTemporaryFileSession(emptyResultFilePath);
            session.Complete();

            return new SortedTemporaryFile(0, emptyResultFilePath, 0);
        }

        sortedFiles.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        var passIndex = 0;

        while (sortedFiles.Count > 1)
        {
            progress?.Report(new SortingStatus(
                SortingPhase.MergingFiles,
                ProcessedLines: 0,
                ProcessedBytes: 0,
                TemporaryFileCount: sortedFiles.Count,
                BytesPerSecond: 0));

            var mergedFiles = new List<SortedTemporaryFile>();
            var nextFileIndex = 0;

            while (nextFileIndex < sortedFiles.Count)
            {
                var inputFiles = SelectMergeGroup(
                    sortedFiles,
                    nextFileIndex,
                    settings.MergeMemoryBudgetInBytes);

                if (inputFiles.Count == 1)
                {
                    mergedFiles.Add(inputFiles[0]);
                    nextFileIndex++;
                    continue;
                }

                var outputFilePath = _fileStorage.GetMergeFilePath(
                    workingDirectory,
                    passIndex,
                    inputFiles[0].Index);

                mergedFiles.Add(SortAndSave(
                    inputFiles,
                    outputFilePath,
                    cancellationToken));

                nextFileIndex += inputFiles.Count;
            }

            sortedFiles.Clear();
            sortedFiles.AddRange(mergedFiles);
            passIndex++;
        }

        return sortedFiles[0];
    }

    private static IReadOnlyList<SortedTemporaryFile> SelectMergeGroup(
        IReadOnlyList<SortedTemporaryFile> availableFiles,
        int firstFileIndex,
        long memoryBudgetInBytes)
    {
        var inputFiles = new List<SortedTemporaryFile>();
        var usedMemoryBytes = FileStorageService.TemporaryStreamMemoryReservationBytes;

        for (var fileIndex = firstFileIndex; fileIndex < availableFiles.Count; fileIndex++)
        {
            var inputFile = availableFiles[fileIndex];
            var inputMemoryBytes = checked(
                FileStorageService.TemporaryStreamMemoryReservationBytes
                + inputFile.MaximumRecordMemoryBytes);

            if (WouldExceedMemoryBudget(
                    usedMemoryBytes,
                    inputMemoryBytes,
                    memoryBudgetInBytes))
            {
                if (inputFiles.Count == 1)
                {
                    throw new InvalidOperationException(
                        "The total memory budget is too small to merge two temporary files.");
                }

                if (inputFiles.Count > 1)
                {
                    break;
                }
            }

            inputFiles.Add(inputFile);
            usedMemoryBytes = checked(usedMemoryBytes + inputMemoryBytes);
        }

        return inputFiles;
    }

    private static bool WouldExceedMemoryBudget(
        long usedMemoryBytes,
        long nextInputMemoryBytes,
        long memoryBudgetInBytes)
    {
        return usedMemoryBytes > memoryBudgetInBytes - nextInputMemoryBytes;
    }

    private static async Task CollectCompletedSortAsync(
        List<Task<SortedTemporaryFile>> activeSorts,
        List<SortedTemporaryFile> sortedFiles)
    {
        var completedSort = await Task.WhenAny(activeSorts);
        activeSorts.Remove(completedSort);
        sortedFiles.Add(await completedSort);
    }
}
