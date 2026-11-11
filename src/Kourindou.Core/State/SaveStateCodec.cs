using System.Text.Json;
using System.Text.Json.Serialization;
using Sagitrs.Kourindou.Core.Characters;

namespace Sagitrs.Kourindou.Core.State;

/// <summary>
/// Game-independent state serialization and validation. It never reads game
/// assemblies or save files; rejected payloads retain the exact input text.
/// </summary>
public static class SaveStateCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize(SaveEnvelope state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, SerializerOptions);
    }

    public static StateLoadResult Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Rejected(json ?? string.Empty, StateDiagnosticCodes.EmptyPayload, "State payload is empty.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return Rejected(json, StateDiagnosticCodes.MalformedJson, $"State payload is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Rejected(json, StateDiagnosticCodes.RootNotObject, "State root must be a JSON object.");
            }

            if (!root.TryGetProperty("schemaVersion", out var schemaVersionElement))
            {
                return Rejected(json, StateDiagnosticCodes.MissingSchemaVersion, "State schemaVersion is missing.");
            }

            if (schemaVersionElement.ValueKind != JsonValueKind.Number
                || !schemaVersionElement.TryGetInt32(out var schemaVersion))
            {
                return Rejected(json, StateDiagnosticCodes.InvalidSchemaVersion, "State schemaVersion must be an integer.");
            }

            if (schemaVersion > SchemaVersionPolicy.Current)
            {
                return Rejected(
                    json,
                    StateDiagnosticCodes.UnsupportedNewerSchema,
                    $"State schemaVersion {schemaVersion} is newer than supported version {SchemaVersionPolicy.Current}; original payload preserved.");
            }

            if (schemaVersion < SchemaVersionPolicy.Current)
            {
                return Rejected(
                    json,
                    StateDiagnosticCodes.UnsupportedOlderSchema,
                    $"State schemaVersion {schemaVersion} has no registered migration; original payload preserved.");
            }

            if (!root.TryGetProperty("slotId", out var slotIdElement)
                || slotIdElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(slotIdElement.GetString()))
            {
                return Rejected(json, StateDiagnosticCodes.MissingSlotId, "State slotId is missing or empty.");
            }

            if (!root.TryGetProperty("characters", out var charactersElement)
                || charactersElement.ValueKind != JsonValueKind.Object)
            {
                return Rejected(json, StateDiagnosticCodes.InvalidCharacters, "State characters must be a JSON object.");
            }

            var duplicateIds = FindDuplicatePropertyNames(charactersElement);
            if (duplicateIds.Count > 0)
            {
                return Rejected(
                    json,
                    StateDiagnosticCodes.DuplicateCharacterId,
                    $"Duplicate character ids are not allowed: {string.Join(", ", duplicateIds)}.");
            }

            SaveEnvelope? state;
            try
            {
                state = JsonSerializer.Deserialize<SaveEnvelope>(json, SerializerOptions);
            }
            catch (JsonException exception)
            {
                return Rejected(json, StateDiagnosticCodes.MalformedJson, $"State payload has an invalid shape: {exception.Message}");
            }

            if (state is null)
            {
                return Rejected(json, StateDiagnosticCodes.MalformedJson, "State payload deserialized to null.");
            }

            var diagnostics = new List<StateDiagnostic>();
            foreach (var characterId in state.Characters.Keys)
            {
                if (!CharacterCatalog.TryGetById(characterId, out _))
                {
                    diagnostics.Add(new StateDiagnostic(
                        StateDiagnosticCodes.UnknownCharacterId,
                        $"Unknown character id '{characterId}' was preserved without interpretation.",
                        StateDiagnosticSeverity.Warning));
                }
            }

            return new StateLoadResult(true, state, diagnostics, json);
        }
    }

    private static IReadOnlyList<string> FindDuplicatePropertyNames(JsonElement element)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name) && !duplicates.Contains(property.Name, StringComparer.Ordinal))
            {
                duplicates.Add(property.Name);
            }
        }

        return duplicates;
    }

    private static StateLoadResult Rejected(string rawPayload, string code, string message)
    {
        return new StateLoadResult(
            false,
            null,
            [new StateDiagnostic(code, message, StateDiagnosticSeverity.Error)],
            rawPayload);
    }
}
