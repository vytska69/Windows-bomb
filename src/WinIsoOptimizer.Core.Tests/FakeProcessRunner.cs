using WinIsoOptimizer.Core.Processes;

namespace WinIsoOptimizer.Core.Tests;

/// <summary>Records every request it's given and returns a scripted result (or a canned success),
/// so tests can assert on the exact dism/oscdimg/reg/powershell command line without a real Windows host.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<ProcessRequest> Requests { get; } = new();

    /// <summary>Keyed by FileName; if absent, defaults to a successful empty result.</summary>
    public Dictionary<string, Func<ProcessRequest, ProcessResult>> Responders { get; } = new();

    public ProcessResult DefaultResult { get; set; } = new(0, string.Empty, string.Empty);

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        var result = Responders.TryGetValue(request.FileName, out var responder) ? responder(request) : DefaultResult;
        return Task.FromResult(result);
    }
}
