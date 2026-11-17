namespace Sagitrs.Kourindou.Core.State;

/// <summary>
/// In-memory per-slot store used to prove slot isolation without touching any
/// game save API or filesystem save file. Save snapshots the caller's envelope;
/// TryGet returns an independent snapshot. Mutating either side does not change
/// the store or another slot. It implements the project-owned
/// <see cref="ISlotStateStore"/> port for the session lifecycle core.
/// </summary>
/// <remarks>
/// This store is intentionally single-threaded and not thread-safe. Callers must
/// serialize access; the class makes no concurrent read/write safety claim.
/// </remarks>
public sealed class InMemorySaveSlotStore : ISlotStateStore
{
    private readonly Dictionary<string, SaveEnvelope> _slots = new(StringComparer.Ordinal);

    public StateLoadResult Load(string slotId, string? json)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return Rejected(StateDiagnosticCodes.SlotMismatch, "Target slot id is required.", json ?? string.Empty);
        }

        var result = SaveStateCodec.Deserialize(json);
        if (!result.IsUsable || result.State is null)
        {
            return result;
        }

        if (!string.Equals(result.State.SlotId, slotId, StringComparison.Ordinal))
        {
            return Rejected(
                StateDiagnosticCodes.SlotMismatch,
                $"State belongs to slot '{result.State.SlotId}', not '{slotId}'.",
                result.RawPayload);
        }

        _slots[slotId] = SaveEnvelopeSnapshot.Copy(result.State);
        return result;
    }

    public StateSaveResult Save(string slotId, SaveEnvelope state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(slotId))
        {
            return new StateSaveResult(false, [Error(StateDiagnosticCodes.SlotMismatch, "Target slot id is required.")]);
        }

        if (!string.Equals(state.SlotId, slotId, StringComparison.Ordinal))
        {
            return new StateSaveResult(false, [Error(StateDiagnosticCodes.SlotMismatch,
                $"State belongs to slot '{state.SlotId}', not '{slotId}'.")]);
        }

        _slots[slotId] = SaveEnvelopeSnapshot.Copy(state);
        return new StateSaveResult(true, Array.Empty<StateDiagnostic>());
    }

    public bool TryGet(string slotId, out SaveEnvelope? state)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            state = null;
            return false;
        }

        if (_slots.TryGetValue(slotId, out var found))
        {
            state = SaveEnvelopeSnapshot.Copy(found);
            return true;
        }

        state = null;
        return false;
    }

    private static StateLoadResult Rejected(string code, string message, string rawPayload)
    {
        return new StateLoadResult(false, null, [Error(code, message)], rawPayload);
    }

    private static StateDiagnostic Error(string code, string message)
    {
        return new StateDiagnostic(code, message, StateDiagnosticSeverity.Error);
    }
}
