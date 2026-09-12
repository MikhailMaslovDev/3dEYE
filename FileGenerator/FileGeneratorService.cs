using System.Diagnostics;
using System.IO;
using System.Text;

namespace FileGenerator;

internal sealed class FileGeneratorService
{
    private const int BufferSize = 64 * 1024;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public async Task GenerateAsync(
        GenerationSettings settings,
        IProgress<GenerationStatus>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var validatedSettings = settings.Validate();
        var outputPath = Path.GetFullPath(validatedSettings.OutputPath);
        var outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("Output directory cannot be determined.");

        Directory.CreateDirectory(outputDirectory);

        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.partial");

        var lineFactory = new RandomLineFactory(validatedSettings.Seed);
        var stopwatch = Stopwatch.StartNew();

        long writtenBytes = 0;
        long writtenLines = 0;

        try
        {
            progress?.Report(CreateStatus(
                writtenBytes,
                writtenLines,
                stopwatch.Elapsed,
                validatedSettings.MinimumFileSizeBytes));

            await using (var fileStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(
                fileStream,
                Utf8WithoutBom,
                BufferSize))
            {
                while (writtenBytes < validatedSettings.MinimumFileSizeBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = lineFactory.CreateLine();

                    await writer.WriteAsync(line.AsMemory(), cancellationToken);
                    await writer.WriteAsync("\n".AsMemory(), cancellationToken);

                    writtenBytes += Utf8WithoutBom.GetByteCount(line) + 1;
                    writtenLines++;

                    if (writtenLines % GenerationSettings.ProgressReportIntervalInLines == 0)
                    {
                        progress?.Report(CreateStatus(
                            writtenBytes,
                            writtenLines,
                            stopwatch.Elapsed,
                            validatedSettings.MinimumFileSizeBytes));
                    }
                }

                await writer.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, outputPath);

            progress?.Report(CreateStatus(
                writtenBytes,
                writtenLines,
                stopwatch.Elapsed,
                validatedSettings.MinimumFileSizeBytes));
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static GenerationStatus CreateStatus(
        long writtenBytes,
        long writtenLines,
        TimeSpan elapsed,
        long targetBytes)
    {
        var bytesPerSecond = elapsed.TotalSeconds > 0
            ? writtenBytes / elapsed.TotalSeconds
            : 0;

        return new GenerationStatus(
            writtenBytes,
            writtenLines,
            bytesPerSecond,
            targetBytes);
    }
}
