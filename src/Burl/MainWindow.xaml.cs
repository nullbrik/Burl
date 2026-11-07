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
    private bool _isLoadingBranches;

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
            BranchBox.ItemsSource = null;
            HistoryList.ItemsSource = null;
            CommitFilesList.ItemsSource = null;
            CommitDiffList.ItemsSource = null;
            return;
        }

        _repositoryPath = dialog.FolderName;
        RepositoryPathText.Text = _repositoryPath;
        StatusText.Text = "";
        RefreshAll();
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

        RefreshAll();
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

        RefreshAll();
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

    private void BranchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingBranches || _repositoryPath is null || BranchBox.SelectedItem is not string branchName)
            return;

        if (branchName == GitService.GetCurrentBranch(_repositoryPath))
            return;

        try
        {
            GitService.SwitchBranch(_repositoryPath, branchName);
            StatusText.Text = $"Switched to {branchName}";
        }
        catch (CheckoutConflictException)
        {
            MessageBox.Show(
                $"Your uncommitted changes would be overwritten by {branchName}. Commit or stash them first.",
                "Burl", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (LibGit2SharpException ex)
        {
            MessageBox.Show(ex.Message, "Burl", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RefreshAll();
    }

    private void CreateBranch_Click(object sender, RoutedEventArgs e)
    {
        if (_repositoryPath is null)
            return;

        var branchName = NewBranchBox.Text.Trim();
        if (branchName.Length == 0)
        {
            StatusText.Text = "Write a branch name first";
            return;
        }

        try
        {
            GitService.CreateBranch(_repositoryPath, branchName);
            NewBranchBox.Clear();
            StatusText.Text = $"Created and switched to {branchName}";
        }
        catch (LibGit2SharpException ex)
        {
            MessageBox.Show(ex.Message, "Burl", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshBranches();
        RefreshChangedFiles();
        RefreshHistory();
    }

    private void RefreshHistory()
    {
        if (_repositoryPath is null)
            return;

        var selectedSha = (HistoryList.SelectedItem as CommitInfo)?.Sha;
        var commits = GitService.GetHistory(_repositoryPath);

        HistoryList.ItemsSource = commits;
        HistoryList.SelectedItem = commits.FirstOrDefault(commit => commit.Sha == selectedSha);
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitDiffList.ItemsSource = null;

        if (_repositoryPath is null || HistoryList.SelectedItem is not CommitInfo commit)
        {
            CommitFilesList.ItemsSource = null;
            return;
        }

        var files = GitService.GetCommitFiles(_repositoryPath, commit.Sha);
        CommitFilesList.ItemsSource = files;
        CommitFilesList.SelectedItem = files.FirstOrDefault();
    }

    private void CommitFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_repositoryPath is null
            || HistoryList.SelectedItem is not CommitInfo commit
            || CommitFilesList.SelectedItem is not CommitFile file)
        {
            CommitDiffList.ItemsSource = null;
            return;
        }

        CommitDiffList.ItemsSource = GitService.GetCommitDiff(_repositoryPath, commit.Sha, file);
    }

    private void RefreshBranches()
    {
        if (_repositoryPath is null)
            return;

        _isLoadingBranches = true;
        BranchBox.ItemsSource = GitService.GetLocalBranches(_repositoryPath);
        BranchBox.SelectedItem = GitService.GetCurrentBranch(_repositoryPath);
        _isLoadingBranches = false;
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
