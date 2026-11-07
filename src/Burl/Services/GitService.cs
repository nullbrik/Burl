using Burl.Models;
using LibGit2Sharp;

namespace Burl.Services;

public static class GitService
{
    private const FileStatus IndexChanges =
        FileStatus.NewInIndex | FileStatus.ModifiedInIndex | FileStatus.DeletedFromIndex
        | FileStatus.RenamedInIndex | FileStatus.TypeChangeInIndex;

    private const FileStatus WorkdirChanges =
        FileStatus.NewInWorkdir | FileStatus.ModifiedInWorkdir | FileStatus.DeletedFromWorkdir
        | FileStatus.RenamedInWorkdir | FileStatus.TypeChangeInWorkdir | FileStatus.Conflicted;

    public static bool IsRepository(string path) => Repository.IsValid(path);

    public static List<ChangedFile> GetChangedFiles(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);

        return repository.RetrieveStatus(new StatusOptions { IncludeUntracked = true })
            .Where(entry => entry.State != FileStatus.Ignored)
            .Select(entry => new ChangedFile(entry.FilePath, ToChangeType(entry.State), ToStagedState(entry.State)))
            .OrderBy(file => file.Path)
            .ToList();
    }

    public static void Stage(string repositoryPath, string filePath)
    {
        using var repository = new Repository(repositoryPath);
        Commands.Stage(repository, filePath);
    }

    public static void Unstage(string repositoryPath, string filePath)
    {
        using var repository = new Repository(repositoryPath);
        Commands.Unstage(repository, filePath);
    }

    private static ChangeType ToChangeType(FileStatus state)
    {
        if (state.HasFlag(FileStatus.Conflicted)) return ChangeType.Conflicted;
        if (state.HasFlag(FileStatus.NewInIndex) || state.HasFlag(FileStatus.NewInWorkdir)) return ChangeType.Added;
        if (state.HasFlag(FileStatus.DeletedFromIndex) || state.HasFlag(FileStatus.DeletedFromWorkdir)) return ChangeType.Deleted;
        if (state.HasFlag(FileStatus.RenamedInIndex) || state.HasFlag(FileStatus.RenamedInWorkdir)) return ChangeType.Renamed;
        return ChangeType.Modified;
    }

    private static bool? ToStagedState(FileStatus state)
    {
        var hasIndexChanges = (state & IndexChanges) != 0;
        var hasWorkdirChanges = (state & WorkdirChanges) != 0;

        if (hasIndexChanges && hasWorkdirChanges) return null;
        return hasIndexChanges;
    }
}
