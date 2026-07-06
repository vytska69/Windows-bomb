using System.Diagnostics;
using System.Text;

namespace WinIsoOptimizer.Core.Processes;

/// <summary>
/// Real <see cref="IProcessRunner"/> backed by <see cref="Process"/>. Streams stdout/stderr lines to
/// <see cref="ProcessRequest.OutputLineProgress"/> as they arrive so a GUI can show live progress and
/// a screen reader can announce it, instead of only seeing output after the tool exits.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = request.FileName,
                WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        foreach (var arg in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdout.AppendLine(e.Data);
            request.OutputLineProgress?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderr.AppendLine(e.Data);
            request.OutputLineProgress?.Report(e.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{request.FileName}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(() => TryKill(process)))
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort: the process may have already exited between the check and the kill.
        }
    }
}
