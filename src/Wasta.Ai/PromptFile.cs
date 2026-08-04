namespace Wasta.Ai;

/// <summary>
/// Resolves the on-disk prompt/knowledge files the modules load at runtime.
///
/// These are deliberately loose files rather than embedded constants so they
/// can be edited without a redeploy - but that means a relative path has to
/// resolve the same way regardless of how the app was launched. The naive
/// File.ReadAllText("Prompts/x.txt") resolves against the current working
/// directory, which is NOT the deploy directory under `dotnet run`, systemd,
/// Docker with a different WORKDIR, or IIS. The file then silently fails to
/// load and the feature degrades for a reason that looks nothing like the
/// cause.
///
/// So: absolute paths are honoured as-is; relative paths are tried against
/// the working directory first (so an operator can override by launching
/// from elsewhere), then against the application's base directory, which is
/// where content files actually ship.
/// </summary>
public static class PromptFile
{
    public static string ResolvePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new ArgumentException("Prompt path is not configured.", nameof(configuredPath));
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var fromWorkingDirectory = Path.GetFullPath(configuredPath);
        if (File.Exists(fromWorkingDirectory))
        {
            return fromWorkingDirectory;
        }

        var fromBaseDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        if (File.Exists(fromBaseDirectory))
        {
            return fromBaseDirectory;
        }

        throw new FileNotFoundException(
            $"Prompt file '{configuredPath}' was not found. Looked in '{fromWorkingDirectory}' and '{fromBaseDirectory}'.",
            configuredPath);
    }

    public static Task<string> ReadAllTextAsync(string configuredPath, CancellationToken ct)
        => File.ReadAllTextAsync(ResolvePath(configuredPath), ct);
}
