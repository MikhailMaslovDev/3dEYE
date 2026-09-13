using System.IO;
using System.Text;

namespace FileSorter;

internal sealed class InvalidLinesLogger : IAsyncDisposable
{
    private const int BufferSize = 64 * 1024;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly string _logPath;
    private StreamWriter? _writer;

    private InvalidLinesLogger(string logPath)
    {
        _logPath = logPath;
    }

    public bool HasEntries { get; private set; }

    public static InvalidLinesLogger Create(string logPath)
    {
        return new InvalidLinesLogger(Path.GetFullPath(logPath));
    }

    public async Task WriteAsync(
        long lineNumber,
        string reason,
        string originalLine,
        CancellationToken cancellationToken)
    {
        var entry = $"Line {lineNumber}: {reason} | {originalLine}";
        var writer = _writer ??= CreateWriter();

        HasEntries = true;
        await writer.WriteLineAsync(entry.AsMemory(), cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _writer?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private StreamWriter CreateWriter()
    {
        var logDirectory = Path.GetDirectoryName(_logPath)
            ?? throw new InvalidOperationException("Log directory cannot be determined.");

        Directory.CreateDirectory(logDirectory);

        var stream = new FileStream(
            _logPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous);

        return new StreamWriter(stream, Utf8WithoutBom, BufferSize);
    }
}
