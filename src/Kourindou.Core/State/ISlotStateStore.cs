namespace Sagitrs.Kourindou.Core.State;

/// <summary>
/// Project-owned per-slot storage port for the session lifecycle core. This is
/// not a Unity, game-save or filesystem API: implementations in this repository
/// are in-memory only, and a future adapter must stay behind this contract.
/// </summary>
/// <remarks>
/// A failed <see cref="Load"/> must leave any previously stored payload for the
/// slot untouched. Save receives an envelope owned by the coordinator and must
/// keep its own independent copy.
/// </remarks>
public interface ISlotStateStore
{
    StateLoadResult Load(string slotId, string? rawPayload);

    StateSaveResult Save(string slotId, SaveEnvelope state);
}
