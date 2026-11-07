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

    public static string Commit(string repositoryPath, string message)
    {
        using var repository = new Repository(repositoryPath);

        var signature = repository.Config.BuildSignature(DateTimeOffset.Now)
            ?? throw new LibGit2SharpException("Set user.name and user.email in your git config first.");

        if (!repository.RetrieveStatus().Any(entry => (entry.State & IndexChanges) != 0))
            throw new EmptyCommitException("Nothing staged to commit.");

        var commit = repository.Commit(message, signature, signature, new CommitOptions());
        return commit.Sha[..7];
    }

    public static List<CommitInfo> GetHistory(string repositoryPath, int maxCount = 500)
    {
        using var repository = new Repository(repositoryPath);

        var filter = new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time };

        return repository.Commits.QueryBy(filter)
            .Take(maxCount)
            .Select(commit => new CommitInfo(commit.Sha, commit.MessageShort, commit.Author.Name, commit.Author.When))
            .ToList();
    }

    public static string GetCurrentBranch(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        return repository.Head.FriendlyName;
    }

    public static List<string> GetLocalBranches(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);

        var names = repository.Branches
            .Where(branch => !branch.IsRemote)
            .Select(branch => branch.FriendlyName)
            .ToList();

        // A new repository has no branch until its first commit
        if (!names.Contains(repository.Head.FriendlyName))
            names.Add(repository.Head.FriendlyName);

        return names.Order().ToList();
    }

    public static void SwitchBranch(string repositoryPath, string branchName)
    {
        using var repository = new Repository(repositoryPath);
        Commands.Checkout(repository, repository.Branches[branchName]);
    }

    public static void CreateBranch(string repositoryPath, string branchName)
    {
        using var repository = new Repository(repositoryPath);

        if (repository.Head.Tip is null)
            throw new LibGit2SharpException("Make a first commit before creating a branch.");

        if (!Reference.IsValidName($"refs/heads/{branchName}"))
            throw new LibGit2SharpException($"'{branchName}' is not a valid branch name.");

        Commands.Checkout(repository, repository.CreateBranch(branchName));
    }

    public static Task<GitCliResult> PushAsync(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);

        return repository.Head.TrackedBranch is null
            ? GitCli.RunAsync(repositoryPath, "push", "-u", "origin", "HEAD")
            : GitCli.RunAsync(repositoryPath, "push");
    }

    public static Task<GitCliResult> PullAsync(string repositoryPath) =>
        GitCli.RunAsync(repositoryPath, "pull", "--ff-only");

    public static List<DiffLine> GetDiff(string repositoryPath, string filePath)
    {
        using var repository = new Repository(repositoryPath);

        var patch = repository.Diff.Compare<Patch>(
            repository.Head.Tip?.Tree,
            DiffTargets.Index | DiffTargets.WorkingDirectory,
            [filePath]);

        return ToDiffLines(patch);
    }

    public static List<CommitFile> GetCommitFiles(string repositoryPath, string sha)
    {
        using var repository = new Repository(repositoryPath);
        var commit = repository.Lookup<Commit>(sha);

        return repository.Diff.Compare<TreeChanges>(commit.Parents.FirstOrDefault()?.Tree, commit.Tree)
            .Select(change => new CommitFile(change.Path, change.OldPath, ToChangeType(change.Status)))
            .OrderBy(file => file.Path)
            .ToList();
    }

    public static List<DiffLine> GetCommitDiff(string repositoryPath, string sha, CommitFile file)
    {
        using var repository = new Repository(repositoryPath);
        var commit = repository.Lookup<Commit>(sha);

        var patch = repository.Diff.Compare<Patch>(commit.Parents.FirstOrDefault()?.Tree, commit.Tree, [file.OldPath, file.Path]);
        return ToDiffLines(patch);
    }

    private static List<DiffLine> ToDiffLines(Patch patch) =>
        patch.Content
            .TrimEnd('\n')
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Select(line => new DiffLine(line, ToDiffLineType(line)))
            .ToList();

    private static ChangeType ToChangeType(LibGit2Sharp.ChangeKind kind) => kind switch
    {
        LibGit2Sharp.ChangeKind.Added or LibGit2Sharp.ChangeKind.Copied => ChangeType.Added,
        LibGit2Sharp.ChangeKind.Deleted => ChangeType.Deleted,
        LibGit2Sharp.ChangeKind.Renamed => ChangeType.Renamed,
        LibGit2Sharp.ChangeKind.Conflicted => ChangeType.Conflicted,
        _ => ChangeType.Modified
    };

    private static DiffLineType ToDiffLineType(string line)
    {
        if (line.StartsWith("+++") || line.StartsWith("---")) return DiffLineType.Header;
        if (line.StartsWith("@@")) return DiffLineType.Hunk;
        if (line.StartsWith('+')) return DiffLineType.Added;
        if (line.StartsWith('-')) return DiffLineType.Removed;
        if (line.StartsWith(' ') || line.Length == 0) return DiffLineType.Context;
        return DiffLineType.Header;
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
