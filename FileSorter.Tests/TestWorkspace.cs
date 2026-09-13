using System.Text;

namespace FileSorter.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "FileSorter.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string InputPath => Path.Combine(DirectoryPath, "input.txt");

    public string OutputPath => Path.Combine(DirectoryPath, "output.txt");

    public Task WriteInputAsync(IEnumerable<string> lines, bool withBom = false)
    {
        return File.WriteAllLinesAsync(
            InputPath,
            lines,
            new UTF8Encoding(withBom));
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
