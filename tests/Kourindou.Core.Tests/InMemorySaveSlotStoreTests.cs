using System.Text.Json;
using Sagitrs.Kourindou.Core.State;

namespace Sagitrs.Kourindou.Core.Tests;

public sealed class InMemorySaveSlotStoreTests
{
    [Fact]
    public void Save_AndLoad_KeepSlotsIsolated()
    {
        var store = new InMemorySaveSlotStore();
        var slotA = TestStateFactory.Envelope("slot-a", ("alice", """{"fixtureValue":1}"""));
        var slotB = TestStateFactory.Envelope("slot-b", ("marisa", """{"fixtureValue":2}"""));

        Assert.True(store.Save("slot-a", slotA).Saved);
        Assert.True(store.Save("slot-b", slotB).Saved);
        Assert.True(store.TryGet("slot-a", out var loadedA));
        Assert.True(store.TryGet("slot-b", out var loadedB));

        var stateA = Assert.IsType<SaveEnvelope>(loadedA);
        var stateB = Assert.IsType<SaveEnvelope>(loadedB);
        Assert.True(stateA.Characters.ContainsKey("alice"));
        Assert.False(stateA.Characters.ContainsKey("marisa"));
        Assert.True(stateB.Characters.ContainsKey("marisa"));
        Assert.False(stateB.Characters.ContainsKey("alice"));
    }

    [Fact]
    public void Load_FromJson_StoresOnlyMatchingSlot()
    {
        var store = new InMemorySaveSlotStore();
        var raw = TestStateFactory.RawState(1, "slot-b", """{"marisa":{"fixtureValue":2}}""");

        var result = store.Load("slot-a", raw);

        Assert.False(result.IsUsable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.SlotMismatch);
        Assert.False(store.TryGet("slot-a", out _));
    }

    [Fact]
    public void Save_RejectsSlotMismatch()
    {
        var store = new InMemorySaveSlotStore();
        var state = TestStateFactory.Envelope("slot-b", ("alice", """{"fixtureValue":1}"""));

        var result = store.Save("slot-a", state);

        Assert.False(result.Saved);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.SlotMismatch);
        Assert.False(store.TryGet("slot-a", out _));
    }

    [Fact]
    public void Save_SnapshotsSharedInput_SoSlotMutationDoesNotLeak()
    {
        var store = new InMemorySaveSlotStore();
        var sharedCharacters = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["alice"] = TestStateFactory.Json("""{"progress":1}"""),
        };

        Assert.True(store.Save("slot-a", new SaveEnvelope { SlotId = "slot-a", Characters = sharedCharacters }).Saved);
        Assert.True(store.Save("slot-b", new SaveEnvelope { SlotId = "slot-b", Characters = sharedCharacters }).Saved);

        Assert.True(store.TryGet("slot-a", out var loadedA));
        var stateA = Assert.IsType<SaveEnvelope>(loadedA);
        stateA.Characters["alice"] = TestStateFactory.Json("""{"progress":9}""");

        Assert.True(store.TryGet("slot-b", out var loadedB));
        var stateB = Assert.IsType<SaveEnvelope>(loadedB);
        Assert.Equal(1, stateB.Characters["alice"].GetProperty("progress").GetInt32());
        Assert.NotSame(stateA.Characters, stateB.Characters);

        sharedCharacters["alice"] = TestStateFactory.Json("""{"progress":42}""");

        Assert.True(store.TryGet("slot-a", out var reloadedA));
        var reloadedStateA = Assert.IsType<SaveEnvelope>(reloadedA);
        Assert.Equal(1, reloadedStateA.Characters["alice"].GetProperty("progress").GetInt32());
        Assert.NotSame(sharedCharacters, reloadedStateA.Characters);
    }

    [Fact]
    public void TryGet_ReturnsIndependentSnapshot_EachCall()
    {
        var store = new InMemorySaveSlotStore();
        var state = TestStateFactory.Envelope("slot-a", ("alice", """{"progress":1}"""));
        Assert.True(store.Save("slot-a", state).Saved);

        Assert.True(store.TryGet("slot-a", out var first));
        Assert.True(store.TryGet("slot-a", out var second));
        var firstState = Assert.IsType<SaveEnvelope>(first);
        var secondState = Assert.IsType<SaveEnvelope>(second);

        firstState.Characters["alice"] = TestStateFactory.Json("""{"progress":9}""");

        Assert.NotSame(firstState.Characters, secondState.Characters);
        Assert.Equal(1, secondState.Characters["alice"].GetProperty("progress").GetInt32());

        Assert.True(store.TryGet("slot-a", out var third));
        var thirdState = Assert.IsType<SaveEnvelope>(third);
        Assert.Equal(1, thirdState.Characters["alice"].GetProperty("progress").GetInt32());
    }

    [Fact]
    public void Load_ReturnsStateIndependentOfStoredSnapshot()
    {
        var store = new InMemorySaveSlotStore();
        var raw = TestStateFactory.RawState(1, "slot-a", """{"alice":{"progress":1}}""");

        var result = store.Load("slot-a", raw);
        Assert.True(result.IsUsable);
        var loaded = Assert.IsType<SaveEnvelope>(result.State);
        loaded.Characters["alice"] = TestStateFactory.Json("""{"progress":9}""");

        Assert.True(store.TryGet("slot-a", out var stored));
        var storedState = Assert.IsType<SaveEnvelope>(stored);
        Assert.Equal(1, storedState.Characters["alice"].GetProperty("progress").GetInt32());
    }

    [Fact]
    public void Load_IntoMatchingSlot_IsUsable()
    {
        var store = new InMemorySaveSlotStore();
        var raw = TestStateFactory.RawState(1, "slot-a", """{"alice":{"fixtureValue":1}}""");

        var result = store.Load("slot-a", raw);

        Assert.True(result.IsUsable);
        Assert.True(store.TryGet("slot-a", out var loaded));
        var state = Assert.IsType<SaveEnvelope>(loaded);
        Assert.True(state.Characters.ContainsKey("alice"));
    }
}
