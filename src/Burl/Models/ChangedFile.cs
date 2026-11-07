namespace Burl.Models;

public enum ChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Conflicted
}

public record ChangedFile(string Path, ChangeType Type);
