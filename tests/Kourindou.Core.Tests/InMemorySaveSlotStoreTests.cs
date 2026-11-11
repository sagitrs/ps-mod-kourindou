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
