using System.Text;

namespace FileSorter.Tests;

public sealed class ExternalFileSorterTests
{
    [Fact]
    public async Task SortAsync_SortsValidUtf8InputWithoutCreatingLog()
    {
        using var workspace = new TestWorkspace();
        var inputLines = new[]
        {
            "2.beta",
            "10.alpha",
            "1.alpha",
            "7. alpha",
            "1.a.b",
            "1.Z"
        };

        await workspace.WriteInputAsync(inputLines);

        await SortAsync(workspace);

        var outputLines = await File.ReadAllLinesAsync(workspace.OutputPath);

        Assert.Equal(
            new[] { "7. alpha", "1.Z", "1.a.b", "1.alpha", "10.alpha", "2.beta" },
            outputLines);
        Assert.Empty(Directory.GetFiles(workspace.DirectoryPath, "invalid-lines-*.log"));
        Assert.Empty(Directory.GetDirectories(workspace.DirectoryPath, ".output.txt.sorting-*"));
    }

    [Fact]
    public async Task SortAsync_SkipsInvalidLinesAndWritesLog()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteInputAsync(new[]
        {
            "2.beta",
            "missing separator",
            "1.",
            "1.alpha",
            "-1.negative"
        });

        await SortAsync(workspace);

        Assert.Equal(
            new[] { "1.alpha", "2.beta" },
            await File.ReadAllLinesAsync(workspace.OutputPath));

        var logPath = Assert.Single(
            Directory.GetFiles(workspace.DirectoryPath, "invalid-lines-*.log"));
        var logLines = await File.ReadAllLinesAsync(logPath);

        Assert.Equal(3, logLines.Length);
        Assert.Contains("Line 2:", logLines[0]);
        Assert.Contains("missing separator", logLines[0]);
        Assert.Contains("Line 3:", logLines[1]);
        Assert.Contains("Line 5:", logLines[2]);
    }

    [Fact]
    public async Task SortAsync_AllInvalidLines_CreatesEmptyOutputAndLog()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteInputAsync(new[] { "missing separator", "1.", "-1.negative" });

        await SortAsync(workspace);

        Assert.Empty(await File.ReadAllLinesAsync(workspace.OutputPath));
        var logPath = Assert.Single(
            Directory.GetFiles(workspace.DirectoryPath, "invalid-lines-*.log"));
        Assert.Equal(3, (await File.ReadAllLinesAsync(logPath)).Length);
    }

    [Fact]
    public async Task SortAsync_ProcessesUtf8Bom()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteInputAsync(new[] { "2.beta", "1.alpha" }, withBom: true);

        await SortAsync(workspace);

        Assert.Equal(
            new[] { "1.alpha", "2.beta" },
            await File.ReadAllLinesAsync(workspace.OutputPath));
        Assert.Empty(Directory.GetFiles(workspace.DirectoryPath, "invalid-lines-*.log"));
    }

    [Fact]
    public async Task SortAsync_TreatsSingleOversizedLineAsSoftMemoryLimit()
    {
        using var workspace = new TestWorkspace();
        var line = $"1.{new string('x', 600_000)}";
        await workspace.WriteInputAsync(new[] { line });

        await SortAsync(workspace, memoryPerWorkerInMegabytes: 1, workerCount: 2);

        Assert.Equal(new[] { line }, await File.ReadAllLinesAsync(workspace.OutputPath));
    }

    [Fact]
    public async Task SortAsync_ThrowsWhenTwoRunsDoNotFitMergeBudget()
    {
        using var workspace = new TestWorkspace();
        var text = new string('x', 600_000);
        await workspace.WriteInputAsync(new[] { $"1.{text}", $"2.{text}" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SortAsync(workspace, memoryPerWorkerInMegabytes: 1, workerCount: 1));

        Assert.False(File.Exists(workspace.OutputPath));
        Assert.Empty(Directory.GetDirectories(workspace.DirectoryPath, ".output.txt.sorting-*"));
    }

    [Fact]
    public async Task SortAsync_PreservesEqualKeyOrderAcrossMultipleMergePasses()
    {
        using var workspace = new TestWorkspace();
        var text = new string('x', 600_000);
        var inputLines = new[]
        {
            $"001.{text}",
            $"1.{text}",
            $"00001.{text}",
            $"01.{text}",
            $"0001.{text}"
        };

        await workspace.WriteInputAsync(inputLines);

        await SortAsync(workspace, memoryPerWorkerInMegabytes: 1, workerCount: 4);

        Assert.Equal(inputLines, await File.ReadAllLinesAsync(workspace.OutputPath));
    }

    [Fact]
    public async Task SortAsync_DoesNotOverwriteExistingOutput()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteInputAsync(new[] { "1.alpha" });
        await File.WriteAllTextAsync(workspace.OutputPath, "sentinel", new UTF8Encoding(false));

        await Assert.ThrowsAsync<IOException>(() => SortAsync(workspace));

        Assert.Equal("sentinel", await File.ReadAllTextAsync(workspace.OutputPath));
    }

    [Fact]
    public async Task SortAsync_CancellationRemovesTemporaryFiles()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteInputAsync(
            Enumerable.Range(0, 10_001).Select(index => $"{index}.item-{index:D5}"));
        using var cancellation = new CancellationTokenSource();
        var progress = new CallbackProgress(status =>
        {
            if (status.Phase == SortingPhase.ReadingInput)
            {
                cancellation.Cancel();
            }
        });
        var settings = new SortSettings(
            workspace.InputPath,
            workspace.OutputPath,
            "1",
            "1");

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ExternalFileSorter().SortAsync(settings, progress, cancellation.Token));

        Assert.False(File.Exists(workspace.OutputPath));
        Assert.Empty(Directory.GetDirectories(workspace.DirectoryPath, ".output.txt.sorting-*"));
    }

    private static Task SortAsync(
        TestWorkspace workspace,
        int memoryPerWorkerInMegabytes = 1,
        int workerCount = 2)
    {
        var settings = new SortSettings(
            workspace.InputPath,
            workspace.OutputPath,
            memoryPerWorkerInMegabytes.ToString(),
            workerCount.ToString());

        return new ExternalFileSorter().SortAsync(settings);
    }

    private sealed class CallbackProgress(Action<SortingStatus> report) : IProgress<SortingStatus>
    {
        public void Report(SortingStatus value)
        {
            report(value);
        }
    }
}
