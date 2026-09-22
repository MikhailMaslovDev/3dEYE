using System.IO;
using System.Text;

namespace FileSorter;

internal sealed class TemporaryFileSession : IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly Dictionary<string, StreamReader> _readers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TextRecordParser _recordParser;
    private StreamWriter? _writer;

    internal TemporaryFileSession(string outputFilePath, TextRecordParser recordParser)
    {
        _recordParser = recordParser;

        var outputStream = new FileStream(
            outputFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            FileStorageService.StreamBufferSize,
            FileOptions.None);

        _writer = new StreamWriter(
            outputStream,
            Utf8WithoutBom,
            FileStorageService.StreamBufferSize)
        {
            NewLine = "\n"
        };
    }

    public TextRecord? ReadNext(
        SortedTemporaryFile temporaryFile,
        long sourceSequence)
    {
        if (!_readers.TryGetValue(temporaryFile.Path, out var reader))
        {
            reader = new StreamReader(
                new FileStream(
                    temporaryFile.Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    FileStorageService.StreamBufferSize,
                    FileOptions.SequentialScan),
                Utf8WithoutBom,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: FileStorageService.StreamBufferSize,
                leaveOpen: false);

            _readers.Add(temporaryFile.Path, reader);
        }

        // Merge reads and writes are sequential and run outside the UI thread.
        // Synchronous calls prevent one Task allocation per temporary-file row.
        var originalLine = reader.ReadLine();

        if (originalLine is null)
        {
            _readers.Remove(temporaryFile.Path);
            reader.Dispose();
            return null;
        }

        if (!_recordParser.TryParse(
                originalLine,
                sourceSequence,
                out var record,
                out var errorMessage))
        {
            throw new InvalidDataException($"Temporary sort file is invalid: {errorMessage}");
        }

        return record;
    }

    public void WriteTemporaryRecord(TextRecord record)
    {
        var writer = _writer
            ?? throw new InvalidOperationException("The temporary file has already been completed.");

        writer.WriteLine(record.OriginalLine);
    }

    public void Complete()
    {
        var writer = _writer
            ?? throw new InvalidOperationException("The temporary file has already been completed.");

        writer.Flush();
        writer.Dispose();
        _writer = null;
    }

    public void Dispose()
    {
        if (_writer is not null)
        {
            _writer.Dispose();
            _writer = null;
        }

        foreach (var reader in _readers.Values)
        {
            reader.Dispose();
        }

        _readers.Clear();
    }
}
