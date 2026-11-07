namespace Burl.Models;

public enum ChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Conflicted
}

// IsStaged is null when only part of the changes are staged
public record ChangedFile(string Path, ChangeType Type, bool? IsStaged);

public record CommitFile(string Path, string OldPath, ChangeType Type);
