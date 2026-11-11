namespace Sagitrs.Kourindou.Core.State;

public sealed record StateSaveResult(bool Saved, IReadOnlyList<StateDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == StateDiagnosticSeverity.Error);
}
