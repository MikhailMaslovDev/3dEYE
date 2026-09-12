using Microsoft.Win32;
using System.Windows;

namespace FileGenerator;

public partial class MainWindow : Window
{
    private readonly FileGeneratorService _generatorService = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Select generated file location",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = CreateSettings();

        _cancellationTokenSource = new CancellationTokenSource();

        GenerateButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        GenerationProgressBar.Minimum = 0;
        GenerationProgressBar.Value = 0;
        GenerationProgressBar.IsIndeterminate = true;
        StatusTextBlock.Text = "Generating...";

        var progress = new Progress<GenerationStatus>(status =>
        {
            GenerationProgressBar.IsIndeterminate = false;
            GenerationProgressBar.Maximum = status.TargetBytes;
            GenerationProgressBar.Value = Math.Min(
                status.WrittenBytes,
                GenerationProgressBar.Maximum);

            StatusTextBlock.Text =
                $"Written: {FormatBytes(status.WrittenBytes)} | " +
                $"Lines: {status.WrittenLines:N0} | " +
                $"Speed: {FormatBytes((long)status.BytesPerSecond)}/s";
        });

        try
        {
            await _generatorService.GenerateAsync(
                settings,
                progress,
                _cancellationTokenSource.Token);

            StatusTextBlock.Text = "Generation completed.";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Generation cancelled.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "Generation failed.";

            MessageBox.Show(
                exception.Message,
                "Generator error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            GenerateButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            GenerationProgressBar.Value = 0;
            GenerationProgressBar.IsIndeterminate = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    private GenerationSettings CreateSettings()
    {
        return new GenerationSettings(
            OutputPathTextBox.Text,
            SizeInMegabytesTextBox.Text,
            SeedTextBox.Text);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];

        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
