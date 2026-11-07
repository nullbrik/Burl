using System.IO;
using System.Windows;
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

        RepositoryPathText.Text = IsGitRepository(dialog.FolderName)
            ? dialog.FolderName
            : "Not a git repository";
    }

    private static bool IsGitRepository(string path) =>
        Directory.Exists(Path.Combine(path, ".git"));
}
