namespace Sagitrs.Kourindou.Core.State;

public enum StateDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record StateDiagnostic(string Code, string Message, StateDiagnosticSeverity Severity);

public static class StateDiagnosticCodes
{
    public const string EmptyPayload = "empty_payload";
    public const string MalformedJson = "malformed_json";
    public const string RootNotObject = "root_not_object";
    public const string MissingSchemaVersion = "missing_schema_version";
    public const string InvalidSchemaVersion = "invalid_schema_version";
    public const string UnsupportedNewerSchema = "unsupported_newer_schema";
    public const string UnsupportedOlderSchema = "unsupported_older_schema";
    public const string MissingSlotId = "missing_slot_id";
    public const string InvalidCharacters = "invalid_characters";
    public const string DuplicateCharacterId = "duplicate_character_id";
    public const string UnknownCharacterId = "unknown_character_id";
    public const string SlotMismatch = "slot_mismatch";
}
