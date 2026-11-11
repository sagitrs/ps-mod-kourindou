namespace Sagitrs.Kourindou.Core.State;

/// <summary>
/// Result of loading a state payload. A rejected result still carries the raw
/// payload so callers can preserve it instead of silently clearing the slot.
/// </summary>
public sealed record StateLoadResult(
    bool IsUsable,
    SaveEnvelope? State,
    IReadOnlyList<StateDiagnostic> Diagnostics,
    string RawPayload)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == StateDiagnosticSeverity.Error);

    public bool HasWarnings => Diagnostics.Any(diagnostic => diagnostic.Severity == StateDiagnosticSeverity.Warning);
}
