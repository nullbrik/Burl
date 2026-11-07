using System.Windows;
using Burl.Services;
using Microsoft.Win32;

namespace Burl;

public partial class MainWindow : Window
{
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
            RepositoryPathText.Text = "Not a git repository";
            ChangedFilesList.ItemsSource = null;
            return;
        }

        RepositoryPathText.Text = dialog.FolderName;
        ChangedFilesList.ItemsSource = GitService.GetChangedFiles(dialog.FolderName);
    }
}
