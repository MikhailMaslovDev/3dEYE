namespace FileGenerator.Tests;

public sealed class FileGeneratorServiceTests
{
    [Fact]
    public async Task GenerateAsync_CreatesUtf8FileAtLeastRequestedSize()
    {
        using var workspace = new TestWorkspace();
        var settings = new GenerationSettings(workspace.OutputPath, "1");

        await new FileGeneratorService().GenerateAsync(settings);

        var bytes = await File.ReadAllBytesAsync(workspace.OutputPath);
        var lines = await File.ReadAllLinesAsync(workspace.OutputPath);

        Assert.True(bytes.Length >= 1_048_576);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.NotEmpty(lines);
        Assert.All(lines, AssertValidLine);
        Assert.Empty(Directory.GetFiles(workspace.DirectoryPath, "*.partial"));
    }

    [Fact]
    public async Task GenerateAsync_PreCancelledToken_CreatesNoOutputOrTemporaryFile()
    {
        using var workspace = new TestWorkspace();
        var settings = new GenerationSettings(workspace.OutputPath, "1");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FileGeneratorService().GenerateAsync(settings, cancellationToken: cancellation.Token));

        Assert.False(File.Exists(workspace.OutputPath));
        Assert.Empty(Directory.GetFiles(workspace.DirectoryPath, "*.partial"));
    }

    private static void AssertValidLine(string line)
    {
        var separatorIndex = line.IndexOf('.');

        Assert.True(separatorIndex > 0);
        Assert.True(int.TryParse(line[..separatorIndex], out _));
        Assert.NotEmpty(line[(separatorIndex + 1)..]);
    }
}
