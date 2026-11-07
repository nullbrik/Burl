using Burl.Models;
using LibGit2Sharp;

namespace Burl.Services;

public static class GitService
{
    public static bool IsRepository(string path) => Repository.IsValid(path);

    public static List<ChangedFile> GetChangedFiles(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);

        return repository.RetrieveStatus(new StatusOptions { IncludeUntracked = true })
            .Where(entry => entry.State != FileStatus.Ignored)
            .Select(entry => new ChangedFile(entry.FilePath, ToChangeType(entry.State)))
            .OrderBy(file => file.Path)
            .ToList();
    }

    private static ChangeType ToChangeType(FileStatus state)
    {
        if (state.HasFlag(FileStatus.Conflicted)) return ChangeType.Conflicted;
        if (state.HasFlag(FileStatus.NewInIndex) || state.HasFlag(FileStatus.NewInWorkdir)) return ChangeType.Added;
        if (state.HasFlag(FileStatus.DeletedFromIndex) || state.HasFlag(FileStatus.DeletedFromWorkdir)) return ChangeType.Deleted;
        if (state.HasFlag(FileStatus.RenamedInIndex) || state.HasFlag(FileStatus.RenamedInWorkdir)) return ChangeType.Renamed;
        return ChangeType.Modified;
    }
}
