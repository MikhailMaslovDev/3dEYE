namespace FileGenerator.Tests;

public sealed class GenerationSettingsTests
{
    [Fact]
    public void Validate_ValidSettings_ReturnsExpectedValues()
    {
        using var workspace = new TestWorkspace();
        var settings = new GenerationSettings(workspace.OutputPath, "1");

        var validated = settings.Validate();

        Assert.Equal(workspace.OutputPath, validated.OutputPath);
        Assert.Equal(1_048_576, validated.MinimumFileSizeBytes);
    }

    [Theory]
    [InlineData("", "1")]
    [InlineData("output.txt", "0")]
    [InlineData("output.txt", "-1")]
    [InlineData("output.txt", "1.5")]
    public void Validate_InvalidValues_Throws(string outputPath, string size)
    {
        var settings = new GenerationSettings(outputPath, size);

        Assert.Throws<ArgumentException>(() => settings.Validate());
    }

    [Fact]
    public async Task Validate_ExistingOutput_Throws()
    {
        using var workspace = new TestWorkspace();
        await File.WriteAllTextAsync(workspace.OutputPath, "existing");
        var settings = new GenerationSettings(workspace.OutputPath, "1");

        Assert.Throws<IOException>(() => settings.Validate());
    }
}
