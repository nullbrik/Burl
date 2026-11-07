namespace Burl.Models;

public record CommitInfo(string Sha, string Message, string Author, DateTimeOffset Date)
{
    public string ShortSha => Sha[..7];
}
