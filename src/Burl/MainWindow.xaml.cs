using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Burl.Models;
using Burl.Services;
using LibGit2Sharp;
using Microsoft.Win32;

namespace Burl;

public partial class MainWindow : Window
{
    private string? _repositoryPath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenRepository_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select a Git repository" };

        if (dialog.ShowDialog() != true)
            return;

        if (!GitService.IsRepository(dialog.FolderName))
        {
            _repositoryPath = null;
            RepositoryPathText.Text = "Not a git repository";
            ChangedFilesList.ItemsSource = null;
            DiffList.ItemsSource = null;
            return;
        }

        _repositoryPath = dialog.FolderName;
        RepositoryPathText.Text = _repositoryPath;
        StatusText.Text = "";
        RefreshChangedFiles();
    }

    private void StageCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_repositoryPath is null || sender is not CheckBox { DataContext: ChangedFile file })
            return;

        try
        {
            if (file.IsStaged == true)
                GitService.Unstage(_repositoryPath, file.Path);
            else
                GitService.Stage(_repositoryPath, file.Path);
        }
        catch (LibGit2SharpException ex)
        {
            MessageBox.Show(ex.Message, "Burl", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RefreshChangedFiles();
    }

    private void Commit_Click(object sender, RoutedEventArgs e)
    {
        if (_repositoryPath is null)
            return;

        var message = CommitMessageBox.Text.Trim();
        if (message.Length == 0)
        {
            StatusText.Text = "Write a commit message first";
            return;
        }

        try
        {
            var sha = GitService.Commit(_repositoryPath, message);
            CommitMessageBox.Clear();
            StatusText.Text = $"Committed {sha}";
        }
        catch (EmptyCommitException)
        {
            StatusText.Text = "Nothing staged to commit";
        }
        catch (LibGit2SharpException ex)
        {
            MessageBox.Show(ex.Message, "Burl", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RefreshChangedFiles();
    }

    private async void Pull_Click(object sender, RoutedEventArgs e) =>
        await RunRemoteOperationAsync("Pulling...", "Pulled", GitService.PullAsync);

    private async void Push_Click(object sender, RoutedEventArgs e) =>
        await RunRemoteOperationAsync("Pushing...", "Pushed", GitService.PushAsync);

    private async Task RunRemoteOperationAsync(
        string runningText, string successText, Func<string, Task<GitCliResult>> operation)
    {
        if (_repositoryPath is null)
            return;

        PullButton.IsEnabled = false;
        PushButton.IsEnabled = false;
        StatusText.Text = runningText;

        try
        {
            var result = await operation(_repositoryPath);

            if (result.Succeeded)
            {
                StatusText.Text = successText;
            }
            else
            {
                StatusText.Text = "";
                MessageBox.Show(result.Output, "Burl", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or LibGit2SharpException)
        {
            StatusText.Text = "";
            MessageBox.Show(ex.Message, "Burl", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PullButton.IsEnabled = true;
            PushButton.IsEnabled = true;
        }

        RefreshChangedFiles();
    }

    private void ChangedFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_repositoryPath is null || ChangedFilesList.SelectedItem is not ChangedFile file)
        {
            DiffList.ItemsSource = null;
            return;
        }

        DiffList.ItemsSource = GitService.GetDiff(_repositoryPath, file.Path);
    }

    private void RefreshChangedFiles()
    {
        if (_repositoryPath is null)
            return;

        var selectedPath = (ChangedFilesList.SelectedItem as ChangedFile)?.Path;
        var files = GitService.GetChangedFiles(_repositoryPath);

        ChangedFilesList.ItemsSource = files;
        ChangedFilesList.SelectedItem = files.FirstOrDefault(file => file.Path == selectedPath);
    }
}
