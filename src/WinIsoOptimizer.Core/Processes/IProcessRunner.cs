namespace WinIsoOptimizer.Core.Processes;

/// <summary>
/// Abstraction over launching an external executable (dism.exe, oscdimg.exe, reg.exe, ...).
/// Exists so Core logic can be unit-tested without a real Windows host or those tools installed.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Arguments are passed as a list (not a pre-joined command line) so paths containing spaces or
/// quotes are passed through to the child process exactly, with no shell-quoting to get wrong.
/// </summary>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IProgress<string>? OutputLineProgress = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

public sealed class ExternalToolException : Exception
{
    public ProcessRequest Request { get; }
    public ProcessResult Result { get; }

    public ExternalToolException(ProcessRequest request, ProcessResult result)
        : base(BuildMessage(request, result))
    {
        Request = request;
        Result = result;
    }

    private static string BuildMessage(ProcessRequest request, ProcessResult result) =>
        $"'{request.FileName} {string.Join(' ', request.Arguments)}' exited with code {result.ExitCode}." +
        (string.IsNullOrWhiteSpace(result.StandardError) ? "" : $" Stderr: {result.StandardError.Trim()}");
}
