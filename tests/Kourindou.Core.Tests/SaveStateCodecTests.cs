using System.Text.Json;
using Sagitrs.Kourindou.Core.State;

namespace Sagitrs.Kourindou.Core.Tests;

public sealed class SaveStateCodecTests
{
    [Fact]
    public void Serialize_ThenDeserialize_PreservesKnownAndUnknownFields()
    {
        var state = new SaveEnvelope
        {
            SlotId = "slot-a",
            Characters = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["alice"] = TestStateFactory.Json("""{"fixtureValue":1,"nested":{"enabled":true}}"""),
            },
            ExtensionData = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["futureTopLevel"] = TestStateFactory.Json("""{"note":"preserve"}"""),
            },
        };

        var json = SaveStateCodec.Serialize(state);
        var result = SaveStateCodec.Deserialize(json);

        Assert.True(result.IsUsable);
        var loaded = Assert.IsType<SaveEnvelope>(result.State);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("alice", Assert.Single(loaded.Characters.Keys));
        Assert.Equal(1, loaded.Characters["alice"].GetProperty("fixtureValue").GetInt32());
        Assert.True(loaded.Characters["alice"].GetProperty("nested").GetProperty("enabled").GetBoolean());
        Assert.NotNull(loaded.ExtensionData);
        Assert.True(loaded.ExtensionData!.ContainsKey("futureTopLevel"));
        Assert.Equal(json, result.RawPayload);
    }

    [Fact]
    public void Serialize_UsesStableId_NotDisplayName()
    {
        var state = TestStateFactory.Envelope("slot-a", ("alice", """{"fixtureValue":1}"""));
        var json = SaveStateCodec.Serialize(state);

        Assert.Contains("\"alice\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("爱丽丝", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_RejectsNewerSchema_AndPreservesRawPayload()
    {
        var raw = TestStateFactory.RawState(999, "slot-a", "{}");
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Null(result.State);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.UnsupportedNewerSchema);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson_AndPreservesRawPayload()
    {
        const string raw = "{not valid json";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.MalformedJson);
    }

    [Fact]
    public void Deserialize_RejectsMalformedSlotIdString_AndPreservesRawPayload()
    {
        const string raw = """{"schemaVersion":1,"slotId":"\uD800","characters":{}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.MalformedString);
    }

    [Fact]
    public void Deserialize_RejectsMalformedCharacterKeyString_AndPreservesRawPayload()
    {
        const string raw = """{"schemaVersion":1,"slotId":"slot-a","characters":{"\uD800":{}}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.MalformedString);
    }

    [Fact]
    public void Deserialize_RejectsMalformedTopLevelKeyString_AndPreservesRawPayload()
    {
        const string raw = """{"schemaVersion":1,"slotId":"slot-a","characters":{},"\uD800":1}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.MalformedString);
    }

    [Fact]
    public void Deserialize_RejectsMalformedCharacterValueString_AndPreservesRawPayload()
    {
        const string raw = """{"schemaVersion":1,"slotId":"slot-a","characters":{"alice":{"note":"\uD800"}}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.MalformedString);
    }

    [Fact]
    public void Deserialize_RejectsMissingSlotId()
    {
        const string raw = """{"schemaVersion":1,"characters":{}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.MissingSlotId);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"schemaVersion":2,"slotId":"slot-a","characters":{}}""")]
    [InlineData("""{"schemaVersion":1,"slotId":"slot-a","slotId":"slot-b","characters":{}}""")]
    [InlineData("""{"schemaVersion":1,"slotId":"slot-a","characters":{},"characters":{}}""")]
    [InlineData("""{"schemaVersion":1,"slotId":"slot-a","characters":{},"future":1,"future":2}""")]
    public void Deserialize_RejectsDuplicateTopLevelProperties(string raw)
    {
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.DuplicateTopLevelProperty);
    }

    [Fact]
    public void Deserialize_RejectsDuplicateCharacterIds()
    {
        const string raw = """{"schemaVersion":1,"slotId":"slot-a","characters":{"alice":{},"alice":{}}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.DuplicateCharacterId);
    }

    [Fact]
    public void Deserialize_WarnsUnknownCharacter_AndPreservesIt()
    {
        const string raw = """{"schemaVersion":1,"slotId":"slot-a","characters":{"alice":{"fixtureValue":1},"legacy_character":{"fixtureValue":2}}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.True(result.IsUsable);
        var loaded = Assert.IsType<SaveEnvelope>(result.State);
        Assert.True(loaded.Characters.ContainsKey("legacy_character"));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.UnknownCharacterId
                && diagnostic.Severity == StateDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Deserialize_RejectsOlderSchema()
    {
        var raw = TestStateFactory.RawState(0, "slot-a", "{}");
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.UnsupportedOlderSchema);
    }

    [Fact]
    public void Deserialize_RejectsNonObjectCharacters()
    {
        const string raw = """{"schemaVersion":1,"slotId":"slot-a","characters":[]}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.InvalidCharacters);
    }

    [Fact]
    public void Deserialize_RejectsMissingSchemaVersion()
    {
        const string raw = """{"slotId":"slot-a","characters":{}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.MissingSchemaVersion);
    }

    [Fact]
    public void Deserialize_RejectsInvalidSchemaVersion()
    {
        const string raw = """{"schemaVersion":"one","slotId":"slot-a","characters":{}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.InvalidSchemaVersion);
    }

    [Fact]
    public void Deserialize_RejectsNonObjectRoot()
    {
        const string raw = "[]";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.RootNotObject);
    }

    [Fact]
    public void Deserialize_EmptyPayloadIsRejected()
    {
        var result = SaveStateCodec.Deserialize("   ");

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.EmptyPayload);
    }

    [Fact]
    public void Deserialize_EmptyCharactersIsUsable()
    {
        const string raw = """{"schemaVersion":1,"slotId":"slot-a","characters":{}}""";
        var result = SaveStateCodec.Deserialize(raw);

        Assert.True(result.IsUsable);
        var loaded = Assert.IsType<SaveEnvelope>(result.State);
        Assert.Empty(loaded.Characters);
        Assert.Empty(result.Diagnostics);
    }
}
