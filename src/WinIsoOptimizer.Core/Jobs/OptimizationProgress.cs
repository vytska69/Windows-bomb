namespace WinIsoOptimizer.Core.Jobs;

/// <summary>
/// A single progress update. <see cref="Message"/> is meant to be read out loud as-is by a screen
/// reader via an accessible live region, so it is always a complete, human-readable sentence rather
/// than a fragment or raw tool output.
/// </summary>
public sealed record OptimizationProgress(string Message, int? PercentComplete = null);
