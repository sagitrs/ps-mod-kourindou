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
        catch (InvalidOperationException exception)
        {
            return Rejected(json, StateDiagnosticCodes.MalformedString, $"State payload contains malformed UTF-16: {exception.Message}");
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

            // JsonDocument accepts unpaired UTF-16 surrogates; decoding them later
            // throws InvalidOperationException. Validate every string up front so
            // malformed input stays a recoverable rejection with RawPayload intact.
            if (!TryValidateStrings(root, out var malformedStringError))
            {
                return Rejected(
                    json,
                    StateDiagnosticCodes.MalformedString,
                    $"State payload contains malformed UTF-16: {malformedStringError}");
            }

            // JsonDocument accepts duplicate object keys. Reject duplicate top-level
            // properties explicitly instead of relying on last-wins deserialization.
            if (!TryFindDuplicatePropertyNames(root, out var duplicateTopLevelNames, out var duplicateTopLevelError))
            {
                return Rejected(
                    json,
                    StateDiagnosticCodes.MalformedString,
                    $"State top-level properties contain malformed UTF-16: {duplicateTopLevelError}");
            }

            if (duplicateTopLevelNames.Count > 0)
            {
                return Rejected(
                    json,
                    StateDiagnosticCodes.DuplicateTopLevelProperty,
                    $"Duplicate top-level properties are not allowed: {string.Join(", ", duplicateTopLevelNames)}.");
            }

            if (!TryGetPropertySafe(root, "schemaVersion", out var schemaVersionElement, out var schemaVersionError))
            {
                if (schemaVersionError is not null)
                {
                    return Rejected(json, StateDiagnosticCodes.MalformedString, $"State schemaVersion lookup failed: {schemaVersionError}");
                }

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

            if (!TryGetPropertySafe(root, "slotId", out var slotIdElement, out var slotIdLookupError))
            {
                if (slotIdLookupError is not null)
                {
                    return Rejected(json, StateDiagnosticCodes.MalformedString, $"State slotId lookup failed: {slotIdLookupError}");
                }

                return Rejected(json, StateDiagnosticCodes.MissingSlotId, "State slotId is missing or empty.");
            }

            if (slotIdElement.ValueKind != JsonValueKind.String)
            {
                return Rejected(json, StateDiagnosticCodes.MissingSlotId, "State slotId is missing or empty.");
            }

            string slotId;
            try
            {
                slotId = slotIdElement.GetString() ?? string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                return Rejected(json, StateDiagnosticCodes.MalformedString, $"State slotId contains malformed UTF-16: {exception.Message}");
            }

            if (string.IsNullOrWhiteSpace(slotId))
            {
                return Rejected(json, StateDiagnosticCodes.MissingSlotId, "State slotId is missing or empty.");
            }

            if (!TryGetPropertySafe(root, "characters", out var charactersElement, out var charactersLookupError))
            {
                if (charactersLookupError is not null)
                {
                    return Rejected(json, StateDiagnosticCodes.MalformedString, $"State characters lookup failed: {charactersLookupError}");
                }

                return Rejected(json, StateDiagnosticCodes.InvalidCharacters, "State characters must be a JSON object.");
            }

            if (charactersElement.ValueKind != JsonValueKind.Object)
            {
                return Rejected(json, StateDiagnosticCodes.InvalidCharacters, "State characters must be a JSON object.");
            }

            if (!TryFindDuplicatePropertyNames(charactersElement, out var duplicateIds, out var duplicateError))
            {
                return Rejected(json, StateDiagnosticCodes.MalformedString, $"State characters contain malformed UTF-16: {duplicateError}");
            }

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
            catch (InvalidOperationException exception)
            {
                return Rejected(json, StateDiagnosticCodes.MalformedString, $"State payload contains malformed UTF-16: {exception.Message}");
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

    private static bool TryValidateStrings(JsonElement element, out string? malformedError)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    _ = element.GetString();
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        _ = property.Name;
                        if (!TryValidateStrings(property.Value, out malformedError))
                        {
                            return false;
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (!TryValidateStrings(item, out malformedError))
                        {
                            return false;
                        }
                    }

                    break;
            }

            malformedError = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            malformedError = exception.Message;
            return false;
        }
    }

    private static bool TryGetPropertySafe(
        JsonElement root,
        string propertyName,
        out JsonElement value,
        out string? malformedError)
    {
        try
        {
            var found = root.TryGetProperty(propertyName, out value);
            malformedError = null;
            return found;
        }
        catch (InvalidOperationException exception)
        {
            value = default;
            malformedError = exception.Message;
            return false;
        }
    }

    private static bool TryFindDuplicatePropertyNames(
        JsonElement element,
        out IReadOnlyList<string> duplicates,
        out string? malformedError)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var found = new List<string>();

        try
        {
            foreach (var property in element.EnumerateObject())
            {
                var name = property.Name;
                if (!seen.Add(name) && !found.Contains(name, StringComparer.Ordinal))
                {
                    found.Add(name);
                }
            }

            duplicates = found;
            malformedError = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            duplicates = Array.Empty<string>();
            malformedError = exception.Message;
            return false;
        }
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
