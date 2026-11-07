namespace Burl.Models;

public enum DiffLineType
{
    Header,
    Hunk,
    Context,
    Added,
    Removed
}

public record DiffLine(string Text, DiffLineType Type);
