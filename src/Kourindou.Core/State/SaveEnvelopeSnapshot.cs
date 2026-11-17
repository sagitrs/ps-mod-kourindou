using System.Text.Json;

namespace Sagitrs.Kourindou.Core.State;

/// <summary>
/// Creates an independent deep copy of a save envelope. Character payloads and
/// extension data are cloned, so mutating either envelope cannot change the
/// other. This is a plain data copy; it performs no game save or file access.
/// </summary>
internal static class SaveEnvelopeSnapshot
{
    public static SaveEnvelope Copy(SaveEnvelope state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(state.Characters);

        var characters = new Dictionary<string, JsonElement>(state.Characters.Count, StringComparer.Ordinal);
        foreach (var pair in state.Characters)
        {
            characters[pair.Key] = pair.Value.Clone();
        }

        Dictionary<string, JsonElement>? extensionData = null;
        if (state.ExtensionData is not null)
        {
            extensionData = new Dictionary<string, JsonElement>(state.ExtensionData.Count, StringComparer.Ordinal);
            foreach (var pair in state.ExtensionData)
            {
                extensionData[pair.Key] = pair.Value.Clone();
            }
        }

        return new SaveEnvelope
        {
            SchemaVersion = state.SchemaVersion,
            SlotId = state.SlotId,
            Characters = characters,
            ExtensionData = extensionData,
        };
    }
}
