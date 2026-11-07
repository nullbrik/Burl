using System.Diagnostics;

namespace Burl.Services;

public record GitCliResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

public static class GitCli
{
    public static async Task<GitCliResult> RunAsync(string repositoryPath, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = (await outputTask + await errorTask).Trim();
        return new GitCliResult(process.ExitCode, output);
    }
}
