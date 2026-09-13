using System.IO;

namespace FileSorter;

internal sealed partial class FileStorageService
{
    internal sealed class TemporaryFileSession : IAsyncDisposable
    {
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
                StreamBufferSize,
                FileOptions.Asynchronous);

            _writer = new StreamWriter(outputStream, Utf8WithoutBom, StreamBufferSize)
            {
                NewLine = "\n"
            };
        }

        public async Task<TextRecord?> ReadNextAsync(
            SortedTemporaryFile temporaryFile,
            long sourceSequence,
            CancellationToken cancellationToken)
        {
            if (!_readers.TryGetValue(temporaryFile.Path, out var reader))
            {
                reader = new StreamReader(
                    new FileStream(
                        temporaryFile.Path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        StreamBufferSize,
                        FileOptions.Asynchronous | FileOptions.SequentialScan),
                    Utf8WithoutBom,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: StreamBufferSize,
                    leaveOpen: false);

                _readers.Add(temporaryFile.Path, reader);
            }

            var originalLine = await reader.ReadLineAsync(cancellationToken);

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

        public Task WriteTemporaryRecordAsync(
            TextRecord record,
            CancellationToken cancellationToken)
        {
            var writer = _writer
                ?? throw new InvalidOperationException("The temporary file has already been completed.");

            return writer.WriteLineAsync(record.OriginalLine.AsMemory(), cancellationToken);
        }

        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            var writer = _writer
                ?? throw new InvalidOperationException("The temporary file has already been completed.");

            await writer.FlushAsync(cancellationToken);
            await writer.DisposeAsync();
            _writer = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_writer is not null)
            {
                await _writer.DisposeAsync();
                _writer = null;
            }

            foreach (var reader in _readers.Values)
            {
                reader.Dispose();
            }

            _readers.Clear();
        }
    }
}
