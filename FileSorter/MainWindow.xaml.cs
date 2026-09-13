using Microsoft.Win32;
using System.Windows;

namespace FileSorter;

public partial class MainWindow : Window
{
    private readonly ExternalFileSorter _fileSorter = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseInputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select input file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            InputPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Select sorted file location",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
        }
    }

    private async void SortButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = CreateSettings();
        _cancellationTokenSource = new CancellationTokenSource();

        SortButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        SortingProgressBar.IsIndeterminate = true;
        StatusTextBlock.Text = "Preparing...";

        try
        {
            var progress = new Progress<SortingStatus>(UpdateSortingStatus);
            var cancellationToken = _cancellationTokenSource.Token;

            await Task.Run(() => _fileSorter.SortAsync(
                settings,
                progress,
                cancellationToken));

            StatusTextBlock.Text = "Sorting completed.";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Sorting cancelled.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "Sorting failed.";

            MessageBox.Show(
                exception.Message,
                "Sorter error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            SortButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            SortingProgressBar.IsIndeterminate = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    private SortSettings CreateSettings()
    {
        return new SortSettings(
            InputPathTextBox.Text,
            OutputPathTextBox.Text,
            MemoryPerWorkerTextBox.Text,
            WorkerCountTextBox.Text);
    }

    private void UpdateSortingStatus(SortingStatus status)
    {
        StatusTextBlock.Text = status.Phase switch
        {
            SortingPhase.ReadingInput => $"Reading input: {status.ProcessedLines:N0} lines.",
            SortingPhase.MergingFiles =>
                $"Merging {status.TemporaryFileCount:N0} temporary files.",
            SortingPhase.Completed => "Sorting completed.",
            _ => "Sorting..."
        };
    }
}
