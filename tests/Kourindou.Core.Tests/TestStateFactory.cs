using System.Text.Json;
using Sagitrs.Kourindou.Core.State;

namespace Sagitrs.Kourindou.Core.Tests;

internal static class TestStateFactory
{
    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public static SaveEnvelope Envelope(string slotId, params (string CharacterId, string PayloadJson)[] characters)
    {
        var payloads = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (characterId, payloadJson) in characters)
        {
            payloads[characterId] = Json(payloadJson);
        }

        return new SaveEnvelope
        {
            SchemaVersion = SchemaVersionPolicy.Current,
            SlotId = slotId,
            Characters = payloads,
        };
    }

    public static string RawState(
        int schemaVersion,
        string slotId,
        string charactersJson,
        string? extraTopLevelJson = null)
    {
        var extra = string.IsNullOrWhiteSpace(extraTopLevelJson)
            ? string.Empty
            : $",{extraTopLevelJson.TrimStart(',')}";

        return $$"""{"schemaVersion":{{schemaVersion}},"slotId":{{JsonSerializer.Serialize(slotId)}},"characters":{{charactersJson}}{{extra}}}""";
    }
}
