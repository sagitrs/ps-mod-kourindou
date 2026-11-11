using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sagitrs.Kourindou.Core.State;

/// <summary>
/// Versioned per-slot state envelope. Character payloads are opaque here: the
/// game-independent core does not assign gameplay defaults that belong to
/// undecided decisions (D02/D03/D06/D07/D08).
/// </summary>
public sealed class SaveEnvelope
{
    public int SchemaVersion { get; init; } = SchemaVersionPolicy.Current;

    public string SlotId { get; init; } = string.Empty;

    public Dictionary<string, JsonElement> Characters { get; init; } = new(StringComparer.Ordinal);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
