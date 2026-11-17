using Sagitrs.Kourindou.Core.Sessions;
using Sagitrs.Kourindou.Core.State;

namespace Sagitrs.Kourindou.Core.Tests;

public sealed class SlotSessionCoordinatorTests
{
    [Fact]
    public void NoSession_RejectsLoadAndSaveWithoutTouchingStore()
    {
        var store = new RecordingSlotStateStore();
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(store, events);

        Assert.Equal(SlotSessionStatus.NoSession, coordinator.Status);
        Assert.Null(coordinator.ActiveSlotId);
        Assert.False(coordinator.HasUsableState);
        Assert.Empty(events.Registrations);

        var load = coordinator.Load("slot-a", Raw("slot-a", "{}"));
        Assert.False(load.IsUsable);
        Assert.Contains(load.Diagnostics, diagnostic => diagnostic.Code == SessionCodes.SessionRequired);
        Assert.Equal(0, store.LoadCalls);

        var save = coordinator.SaveSnapshot();
        Assert.False(save.Saved);
        Assert.Contains(save.Diagnostics, diagnostic => diagnostic.Code == SessionCodes.SessionRequired);
        Assert.Equal(0, store.SaveCalls);
        Assert.False(store.TryGet("slot-a", out _));
    }

    [Fact]
    public void EnterSlot_ActivatesExplicitSessionWithoutInventingState()
    {
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store);

        var entered = coordinator.EnterSlot("slot-a");

        Assert.True(entered.Succeeded);
        Assert.Equal(SessionCodes.Entered, entered.Code);
        Assert.Equal(SlotSessionStatus.Active, coordinator.Status);
        Assert.Equal("slot-a", coordinator.ActiveSlotId);
        Assert.False(coordinator.HasUsableState);

        var save = coordinator.SaveSnapshot();
        Assert.False(save.Saved);
        Assert.Contains(save.Diagnostics, diagnostic => diagnostic.Code == SessionCodes.NoUsableState);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void EnterSlot_RejectsEmptySlotId()
    {
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore());

        var result = coordinator.EnterSlot("   ");

        Assert.False(result.Succeeded);
        Assert.Equal(SessionCodes.InvalidSlotId, result.Code);
        Assert.Equal(SlotSessionStatus.NoSession, coordinator.Status);
    }

    [Fact]
    public void DuplicateEnter_SameSlot_DoesNotResubscribeOrResetState()
    {
        var store = new RecordingSlotStateStore();
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(store, events);

        coordinator.EnterSlot("slot-a");
        var firstGeneration = coordinator.Generation;
        Assert.Equal(1, events.ActiveSubscriptionCount);
        Assert.True(coordinator.Load("slot-a", Raw("slot-a", """{"alice":{"progress":1}}""")).IsUsable);

        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handled++).Registered);
        events.Publish(new SessionEvent("tick"));
        Assert.Equal(1, handled);

        var duplicate = coordinator.EnterSlot("slot-a");

        Assert.True(duplicate.Succeeded);
        Assert.Equal(SessionCodes.AlreadyActive, duplicate.Code);
        Assert.Equal(firstGeneration, coordinator.Generation);
        Assert.Equal(1, events.ActiveSubscriptionCount);
        Assert.Equal(1, coordinator.RegisteredEventHandlerCount);
        Assert.True(coordinator.HasUsableState);

        events.Publish(new SessionEvent("tick"));
        Assert.Equal(2, handled);
    }

    [Fact]
    public void SwitchSlot_DoesNotCarryStateOrHandlersAcrossSlots()
    {
        var store = new RecordingSlotStateStore();
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(store, events);

        coordinator.EnterSlot("slot-a");
        Assert.True(coordinator.Load("slot-a", Raw("slot-a", """{"alice":{"progress":1}}""")).IsUsable);
        Assert.True(coordinator.SaveSnapshot().Saved);
        var handledA = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handledA++).Registered);
        var generationA = coordinator.Generation;

        var switched = coordinator.EnterSlot("slot-b");

        Assert.True(switched.Succeeded);
        Assert.Equal(SessionCodes.Switched, switched.Code);
        Assert.Equal("slot-b", coordinator.ActiveSlotId);
        Assert.False(coordinator.HasUsableState);
        Assert.NotEqual(generationA, coordinator.Generation);
        Assert.Equal(0, coordinator.RegisteredEventHandlerCount);
        Assert.Equal(1, events.ActiveSubscriptionCount);
        Assert.False(coordinator.SaveSnapshot().Saved);

        Assert.True(coordinator.Load("slot-b", Raw("slot-b", """{"marisa":{"progress":2}}""")).IsUsable);
        Assert.True(coordinator.SaveSnapshot().Saved);

        Assert.True(store.TryGet("slot-a", out var stateA));
        var envelopeA = Assert.IsType<SaveEnvelope>(stateA);
        Assert.Equal(1, envelopeA.Characters["alice"].GetProperty("progress").GetInt32());
        Assert.False(envelopeA.Characters.ContainsKey("marisa"));

        Assert.True(store.TryGet("slot-b", out var stateB));
        var envelopeB = Assert.IsType<SaveEnvelope>(stateB);
        Assert.Equal(2, envelopeB.Characters["marisa"].GetProperty("progress").GetInt32());
        Assert.False(envelopeB.Characters.ContainsKey("alice"));

        events.Publish(new SessionEvent("tick"));
        Assert.Equal(0, handledA);
    }

    [Fact]
    public void LateCallbackFromPreviousSession_IsDiscardedAfterSwitch()
    {
        var store = new RecordingSlotStateStore();
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(store, events);

        coordinator.EnterSlot("slot-a");
        var handledA = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handledA++).Registered);
        Assert.True(coordinator.ProcessEvent(new SessionEvent("tick")).Processed);
        Assert.Equal(1, handledA);

        coordinator.EnterSlot("slot-b");
        var handledB = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handledB++).Registered);
        Assert.Equal(2, events.Registrations.Count);

        // Registration 0 belongs to slot-a and was disposed during the switch.
        events.DeliverLate(0, new SessionEvent("tick"));

        Assert.Equal(1, handledA);
        Assert.Equal(0, handledB);

        events.Publish(new SessionEvent("tick"));
        Assert.Equal(1, handledB);
    }

    [Fact]
    public void SynchronousSubscribeCallback_EnterSlotReentrancyIsRejectedAndLeavesOneSubscription()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);

        SessionOperationResult? reentrantEnter = null;
        events.OnSubscribe = () => reentrantEnter = coordinator.EnterSlot("slot-b");

        var entered = coordinator.EnterSlot("slot-a");

        Assert.True(entered.Succeeded);
        Assert.Equal(SessionCodes.Entered, entered.Code);
        Assert.NotNull(reentrantEnter);
        Assert.False(reentrantEnter.Succeeded);
        Assert.Equal(SessionCodes.TransitionInProgress, reentrantEnter.Code);
        Assert.Equal(SlotSessionStatus.Active, coordinator.Status);
        Assert.Equal("slot-a", coordinator.ActiveSlotId);
        Assert.Equal(1, events.ActiveSubscriptionCount);
        Assert.Single(events.Registrations);

        Assert.True(coordinator.Exit().Succeeded);
        Assert.Equal(0, events.ActiveSubscriptionCount);
    }

    [Fact]
    public void SynchronousSubscribeCallback_ExitReentrancyIsRejectedAndLeavesOneSubscription()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);

        SessionOperationResult? reentrantExit = null;
        events.OnSubscribe = () => reentrantExit = coordinator.Exit();

        var entered = coordinator.EnterSlot("slot-a");

        Assert.True(entered.Succeeded);
        Assert.Equal(SessionCodes.Entered, entered.Code);
        Assert.NotNull(reentrantExit);
        Assert.False(reentrantExit.Succeeded);
        Assert.Equal(SessionCodes.TransitionInProgress, reentrantExit.Code);
        Assert.Equal(SlotSessionStatus.Active, coordinator.Status);
        Assert.Equal("slot-a", coordinator.ActiveSlotId);
        Assert.Equal(1, events.ActiveSubscriptionCount);
        Assert.Single(events.Registrations);

        Assert.True(coordinator.Exit().Succeeded);
        Assert.Equal(0, events.ActiveSubscriptionCount);
    }

    [Fact]
    public void SynchronousSubscribeCallback_DisableSupersedesStartAndReleasesReturnedHandle()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);

        SessionOperationResult? reentrantDisable = null;
        events.OnSubscribe = () => reentrantDisable = coordinator.Disable();

        var entered = coordinator.EnterSlot("slot-a");

        Assert.False(entered.Succeeded);
        Assert.Equal(SessionCodes.SessionStartSuperseded, entered.Code);
        Assert.NotNull(reentrantDisable);
        Assert.True(reentrantDisable.Succeeded);
        Assert.Equal(SessionCodes.Disabled, reentrantDisable.Code);
        Assert.Equal(SlotSessionStatus.Disabled, coordinator.Status);
        Assert.Null(coordinator.ActiveSlotId);
        Assert.Equal(0, events.ActiveSubscriptionCount);
        Assert.Single(events.Registrations);
    }

    [Fact]
    public void OldHandlerHandle_AfterSwitch_CannotRemoveNewSessionsHandler()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);

        coordinator.EnterSlot("slot-a");
        var oldRegistration = coordinator.RegisterEventHandler("tick", _ => { });
        coordinator.EnterSlot("slot-b");
        var handledB = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handledB++).Registered);

        oldRegistration.Handle!.Dispose();
        events.Publish(new SessionEvent("tick"));

        Assert.Equal(1, handledB);
        Assert.Equal(1, coordinator.RegisteredEventHandlerCount);
    }

    [Fact]
    public void Exit_CancelsSessionTokenAndReleasesRegistrations()
    {
        var store = new RecordingSlotStateStore();
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(store, events);

        coordinator.EnterSlot("slot-a");
        var token = CancellationToken.None;
        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", context =>
        {
            token = context.CancellationToken;
            handled++;
        }).Registered);
        events.Publish(new SessionEvent("tick"));
        Assert.Equal(1, handled);
        Assert.False(token.IsCancellationRequested);

        var exited = coordinator.Exit();

        Assert.True(exited.Succeeded);
        Assert.Equal(SessionCodes.Exited, exited.Code);
        Assert.Equal(SlotSessionStatus.NoSession, coordinator.Status);
        Assert.Null(coordinator.ActiveSlotId);
        Assert.False(coordinator.HasUsableState);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(0, events.ActiveSubscriptionCount);
        Assert.Equal(0, coordinator.RegisteredEventHandlerCount);

        events.DeliverLate(0, new SessionEvent("tick"));
        Assert.Equal(1, handled);

        var repeated = coordinator.Exit();
        Assert.True(repeated.Succeeded);
        Assert.Equal(SessionCodes.AlreadyExited, repeated.Code);
    }

    [Fact]
    public void CancellationCallbackDuringExit_CannotSaveEndingSession()
    {
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");
        Assert.True(coordinator.Load("slot-a", Raw("slot-a", """{"alice":{"progress":1}}""")).IsUsable);
        Assert.True(coordinator.SaveSnapshot().Saved);

        var token = CancellationToken.None;
        Assert.True(coordinator.RegisterEventHandler("tick", context => token = context.CancellationToken).Registered);
        Assert.True(coordinator.ProcessEvent(new SessionEvent("tick")).Processed);

        StateSaveResult? cancellationSave = null;
        using var cancellationRegistration = token.Register(
            () => cancellationSave = coordinator.SaveSnapshot());

        Assert.True(coordinator.Exit().Succeeded);

        Assert.NotNull(cancellationSave);
        Assert.False(cancellationSave.Saved);
        Assert.Contains(cancellationSave.Diagnostics, diagnostic => diagnostic.Code == SessionCodes.SessionRequired);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public void CancellationCallbackDuringExit_CannotStartNewSession()
    {
        var events = new RecordingSessionEventPort();
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store, events);
        coordinator.EnterSlot("slot-a");
        var token = coordinator.CurrentCancellationToken;

        SessionOperationResult? callbackEnter = null;
        using var cancellationRegistration = token.Register(
            () => callbackEnter = coordinator.EnterSlot("slot-b"));

        Assert.True(coordinator.Exit().Succeeded);

        Assert.NotNull(callbackEnter);
        Assert.False(callbackEnter.Succeeded);
        Assert.Equal(SessionCodes.TransitionInProgress, callbackEnter.Code);
        Assert.Equal(SlotSessionStatus.NoSession, coordinator.Status);
        Assert.Null(coordinator.ActiveSlotId);
        Assert.Equal(0, events.ActiveSubscriptionCount);
        Assert.Single(events.Registrations);
    }

    [Fact]
    public void Disable_IsTerminalAndReleasesEverything()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);
        coordinator.EnterSlot("slot-a");
        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handled++).Registered);
        var token = coordinator.CurrentCancellationToken;

        var disabled = coordinator.Disable();

        Assert.True(disabled.Succeeded);
        Assert.Equal(SessionCodes.Disabled, disabled.Code);
        Assert.Equal(SlotSessionStatus.Disabled, coordinator.Status);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(0, events.ActiveSubscriptionCount);

        Assert.Equal(SessionCodes.SessionDisabled, coordinator.EnterSlot("slot-b").Code);
        Assert.Contains(
            coordinator.Load("slot-a", Raw("slot-a", "{}")).Diagnostics,
            diagnostic => diagnostic.Code == SessionCodes.SessionDisabled);
        Assert.Contains(
            coordinator.SaveSnapshot().Diagnostics,
            diagnostic => diagnostic.Code == SessionCodes.SessionDisabled);
        Assert.Equal(
            SessionCodes.SessionDisabled,
            coordinator.RegisterEventHandler("other", _ => { }).Code);
        Assert.Equal(SessionCodes.SessionDisabled, coordinator.ProcessEvent(new SessionEvent("tick")).Code);
        Assert.Equal(SessionCodes.AlreadyDisabled, coordinator.Disable().Code);

        events.DeliverLate(0, new SessionEvent("tick"));
        Assert.Equal(0, handled);
    }

    [Fact]
    public void Dispose_DisablesAndIsIdempotent()
    {
        var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore());
        coordinator.EnterSlot("slot-a");

        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Equal(SlotSessionStatus.Disabled, coordinator.Status);
        Assert.Equal(SessionCodes.SessionDisabled, coordinator.EnterSlot("slot-b").Code);
    }

    [Fact]
    public void Load_UsablePayload_EnablesSaveSnapshot()
    {
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");

        var result = coordinator.Load(
            "slot-a",
            Raw("slot-a", """{"alice":{"progress":1},"legacy":{"note":"keep"}}"""));

        Assert.True(result.IsUsable);
        Assert.NotEmpty(result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.UnknownCharacterId
                && diagnostic.Severity == StateDiagnosticSeverity.Warning);
        Assert.True(coordinator.HasUsableState);

        var saved = coordinator.SaveSnapshot();

        Assert.True(saved.Saved);
        Assert.Equal(1, store.SaveCalls);
        Assert.True(store.TryGet("slot-a", out var stored));
        var envelope = Assert.IsType<SaveEnvelope>(stored);
        Assert.Equal(1, envelope.Characters["alice"].GetProperty("progress").GetInt32());
        Assert.Equal("keep", envelope.Characters["legacy"].GetProperty("note").GetString());
    }

    [Fact]
    public void FailedLoad_DoesNotCreateBlankStateOrWriteSlot()
    {
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");

        const string raw = "{not valid json";
        var result = coordinator.Load("slot-a", raw);

        Assert.False(result.IsUsable);
        Assert.Null(result.State);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == StateDiagnosticCodes.MalformedJson);
        Assert.False(coordinator.HasUsableState);

        var save = coordinator.SaveSnapshot();
        Assert.False(save.Saved);
        Assert.Contains(save.Diagnostics, diagnostic => diagnostic.Code == SessionCodes.NoUsableState);
        Assert.Equal(0, store.SaveCalls);
        Assert.False(store.TryGet("slot-a", out _));
    }

    [Fact]
    public void FailedLoad_DoesNotOverwritePreviouslyStoredSlot()
    {
        var store = new RecordingSlotStateStore();
        Assert.True(store.Seed("slot-a", TestStateFactory.Envelope("slot-a", ("alice", """{"progress":1}"""))).Saved);

        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");

        var result = coordinator.Load(
            "slot-a",
            """{"schemaVersion":1,"slotId":"slot-a","characters":[]}""");

        Assert.False(result.IsUsable);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == StateDiagnosticCodes.InvalidCharacters);
        Assert.False(coordinator.HasUsableState);
        Assert.False(coordinator.SaveSnapshot().Saved);

        Assert.True(store.TryGet("slot-a", out var stored));
        var envelope = Assert.IsType<SaveEnvelope>(stored);
        Assert.Equal(1, envelope.Characters["alice"].GetProperty("progress").GetInt32());
    }

    [Fact]
    public void FailedLoad_NewerSchema_PreservesRawPayloadAndDoesNotWrite()
    {
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");
        var raw = TestStateFactory.RawState(999, "slot-a", "{}");

        var result = coordinator.Load("slot-a", raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == StateDiagnosticCodes.UnsupportedNewerSchema);
        Assert.False(coordinator.SaveSnapshot().Saved);
        Assert.Equal(0, store.SaveCalls);
        Assert.False(store.TryGet("slot-a", out _));
    }

    [Fact]
    public void FailedLoadAfterSuccess_InvalidatesStateAndKeepsStoredPayload()
    {
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");
        Assert.True(coordinator.Load("slot-a", Raw("slot-a", """{"alice":{"progress":1}}""")).IsUsable);
        Assert.True(coordinator.SaveSnapshot().Saved);
        Assert.Equal(1, store.SaveCalls);

        var failed = coordinator.Load("slot-a", "{broken");

        Assert.False(failed.IsUsable);
        Assert.False(coordinator.HasUsableState);

        var save = coordinator.SaveSnapshot();
        Assert.False(save.Saved);
        Assert.Equal(1, store.SaveCalls);

        Assert.True(store.TryGet("slot-a", out var stored));
        var envelope = Assert.IsType<SaveEnvelope>(stored);
        Assert.Equal(1, envelope.Characters["alice"].GetProperty("progress").GetInt32());
    }

    [Fact]
    public void Load_WithDifferentSlotThanActive_IsRejectedWithoutStoreAccess()
    {
        var store = new RecordingSlotStateStore();
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");
        var raw = Raw("slot-b", "{}");

        var result = coordinator.Load("slot-b", raw);

        Assert.False(result.IsUsable);
        Assert.Equal(raw, result.RawPayload);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SessionCodes.SessionSlotMismatch);
        Assert.Equal(0, store.LoadCalls);
        Assert.False(store.TryGet("slot-b", out _));
    }

    [Fact]
    public void Load_RejectsStoreStateForAnotherSlotWithoutEnablingSave()
    {
        var wrongSlotState = TestStateFactory.Envelope("slot-b", ("alice", """{"progress":1}"""));
        var store = new StubSlotStateStore
        {
            LoadHandler = (_, rawPayload) => new StateLoadResult(
                true,
                wrongSlotState,
                Array.Empty<StateDiagnostic>(),
                rawPayload ?? string.Empty),
        };
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");

        var result = coordinator.Load("slot-a", """{"anything":true}""");

        Assert.False(result.IsUsable);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == StateDiagnosticCodes.SlotMismatch);
        Assert.False(coordinator.HasUsableState);
        Assert.False(coordinator.SaveSnapshot().Saved);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void Load_CopiesStateSoAliasesCannotMutateSavedSnapshot()
    {
        var shared = TestStateFactory.Envelope("slot-a", ("alice", """{"progress":1}"""));
        var store = new StubSlotStateStore
        {
            LoadHandler = (_, rawPayload) => new StateLoadResult(
                true,
                shared,
                Array.Empty<StateDiagnostic>(),
                rawPayload ?? string.Empty),
        };
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");

        var load = coordinator.Load("slot-a", Raw("slot-a", "{}"));
        Assert.True(load.IsUsable);

        shared.Characters["alice"] = TestStateFactory.Json("""{"progress":9}""");
        var returned = Assert.IsType<SaveEnvelope>(load.State);
        returned.Characters["alice"] = TestStateFactory.Json("""{"progress":7}""");

        Assert.True(coordinator.SaveSnapshot().Saved);
        Assert.Equal("slot-a", store.LastSaveSlot);
        var saved = Assert.IsType<SaveEnvelope>(store.LastSavedState);
        Assert.Equal(1, saved.Characters["alice"].GetProperty("progress").GetInt32());
    }

    [Fact]
    public void SaveSnapshot_PropagatesStorageFailureWithoutClearingState()
    {
        var loadState = TestStateFactory.Envelope("slot-a", ("alice", """{"progress":1}"""));
        var store = new StubSlotStateStore
        {
            LoadHandler = (_, rawPayload) => new StateLoadResult(
                true,
                loadState,
                Array.Empty<StateDiagnostic>(),
                rawPayload ?? string.Empty),
            SaveHandler = (_, _) => new StateSaveResult(
                false,
                [new StateDiagnostic("storage_unavailable", "stub save failure", StateDiagnosticSeverity.Error)]),
        };
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");
        Assert.True(coordinator.Load("slot-a", Raw("slot-a", "{}")).IsUsable);

        var save = coordinator.SaveSnapshot();

        Assert.False(save.Saved);
        Assert.Contains(save.Diagnostics, diagnostic => diagnostic.Code == "storage_unavailable");
        Assert.True(coordinator.HasUsableState);
    }

    [Fact]
    public void SaveSnapshot_RejectsReentrantSaveFromStoragePort()
    {
        var loadState = TestStateFactory.Envelope("slot-a", ("alice", """{"progress":1}"""));
        var store = new StubSlotStateStore
        {
            LoadHandler = (_, rawPayload) => new StateLoadResult(
                true,
                loadState,
                Array.Empty<StateDiagnostic>(),
                rawPayload ?? string.Empty),
        };
        using var coordinator = new SlotSessionCoordinator(store);
        coordinator.EnterSlot("slot-a");
        Assert.True(coordinator.Load("slot-a", Raw("slot-a", "{}")).IsUsable);

        StateSaveResult? reentrant = null;
        store.SaveHandler = (_, _) =>
        {
            reentrant = coordinator.SaveSnapshot();
            return new StateSaveResult(true, Array.Empty<StateDiagnostic>());
        };

        var outer = coordinator.SaveSnapshot();

        Assert.True(outer.Saved);
        Assert.NotNull(reentrant);
        Assert.False(reentrant.Saved);
        Assert.Contains(reentrant.Diagnostics, diagnostic => diagnostic.Code == SessionCodes.ReentrantSave);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public void EventHandlerRegistration_IsBoundedAndDisposable()
    {
        using var coordinator = new SlotSessionCoordinator(
            new RecordingSlotStateStore(),
            maxEventHandlers: 2);
        coordinator.EnterSlot("slot-a");

        var first = coordinator.RegisterEventHandler("first", _ => { });
        var second = coordinator.RegisterEventHandler("second", _ => { });

        Assert.True(first.Registered);
        Assert.True(second.Registered);
        Assert.Equal(2, coordinator.RegisteredEventHandlerCount);

        var third = coordinator.RegisterEventHandler("third", _ => { });
        Assert.False(third.Registered);
        Assert.Equal(SessionCodes.HandlerLimitReached, third.Code);
        Assert.Null(third.Handle);

        first.Handle!.Dispose();
        first.Handle.Dispose();
        Assert.Equal(1, coordinator.RegisteredEventHandlerCount);

        var afterRelease = coordinator.RegisterEventHandler("third", _ => { });
        Assert.True(afterRelease.Registered);
        Assert.Equal(2, coordinator.RegisteredEventHandlerCount);
    }

    [Fact]
    public void DuplicateEventHandlerName_IsRejectedAndInvokedOnce()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);
        coordinator.EnterSlot("slot-a");
        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handled++).Registered);

        var duplicate = coordinator.RegisterEventHandler("tick", _ => handled += 100);

        Assert.False(duplicate.Registered);
        Assert.Equal(SessionCodes.DuplicateHandler, duplicate.Code);
        Assert.Equal(1, coordinator.RegisteredEventHandlerCount);

        events.Publish(new SessionEvent("tick"));
        Assert.Equal(1, handled);
    }

    [Fact]
    public void DisposedHandler_IsNotInvoked()
    {
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore());
        coordinator.EnterSlot("slot-a");
        var handled = 0;
        var registration = coordinator.RegisterEventHandler("tick", _ => handled++);
        registration.Handle!.Dispose();

        var result = coordinator.ProcessEvent(new SessionEvent("tick"));

        Assert.False(result.Processed);
        Assert.Equal(SessionCodes.NoHandler, result.Code);
        Assert.Equal(0, handled);
        Assert.Equal(0, coordinator.RegisteredEventHandlerCount);
    }

    [Fact]
    public void DuplicateEventId_IsProcessedOnceWithinSession()
    {
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore());
        coordinator.EnterSlot("slot-a");
        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handled++).Registered);

        var first = coordinator.ProcessEvent(new SessionEvent("tick", "event-1"));
        var duplicate = coordinator.ProcessEvent(new SessionEvent("tick", "event-1"));
        var different = coordinator.ProcessEvent(new SessionEvent("tick", "event-2"));

        Assert.True(first.Processed);
        Assert.False(duplicate.Processed);
        Assert.Equal(SessionCodes.DuplicateEvent, duplicate.Code);
        Assert.True(different.Processed);
        Assert.Equal(2, handled);
    }

    [Fact]
    public void HandlerException_ConsumesEventIdForAtMostOnceProcessing()
    {
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore());
        coordinator.EnterSlot("slot-a");
        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("boom", _ =>
        {
            handled++;
            throw new InvalidOperationException("handler failure");
        }).Registered);

        Assert.Throws<InvalidOperationException>(
            () => coordinator.ProcessEvent(new SessionEvent("boom", "event-1")));
        var retry = coordinator.ProcessEvent(new SessionEvent("boom", "event-1"));

        Assert.False(retry.Processed);
        Assert.Equal(SessionCodes.DuplicateEvent, retry.Code);
        Assert.Equal(1, handled);
    }

    [Fact]
    public void RememberedEventIds_AreSessionScopedAndSurviveReload()
    {
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore());
        coordinator.EnterSlot("slot-a");
        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handled++).Registered);
        Assert.True(coordinator.ProcessEvent(new SessionEvent("tick", "event-1")).Processed);
        Assert.Equal(1, handled);

        Assert.True(coordinator.Load("slot-a", Raw("slot-a", """{"alice":{"progress":1}}""")).IsUsable);
        var afterLoad = coordinator.ProcessEvent(new SessionEvent("tick", "event-1"));

        Assert.False(afterLoad.Processed);
        Assert.Equal(SessionCodes.DuplicateEvent, afterLoad.Code);
        Assert.Equal(1, handled);

        Assert.True(coordinator.Exit().Succeeded);
        Assert.True(coordinator.EnterSlot("slot-a").Succeeded);
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handled++).Registered);
        Assert.True(coordinator.ProcessEvent(new SessionEvent("tick", "event-1")).Processed);
        Assert.Equal(2, handled);
    }

    [Fact]
    public void RememberedEventIds_AreBoundedAndFailClosed()
    {
        using var coordinator = new SlotSessionCoordinator(
            new RecordingSlotStateStore(),
            maxRememberedEventIds: 2);
        coordinator.EnterSlot("slot-a");
        var handled = 0;
        Assert.True(coordinator.RegisterEventHandler("tick", _ => handled++).Registered);

        Assert.True(coordinator.ProcessEvent(new SessionEvent("tick", "event-1")).Processed);
        Assert.True(coordinator.ProcessEvent(new SessionEvent("tick", "event-2")).Processed);

        var overflow = coordinator.ProcessEvent(new SessionEvent("tick", "event-3"));

        Assert.False(overflow.Processed);
        Assert.Equal(SessionCodes.EventCapacityReached, overflow.Code);
        Assert.Equal(2, handled);

        var duplicate = coordinator.ProcessEvent(new SessionEvent("tick", "event-1"));
        Assert.Equal(SessionCodes.DuplicateEvent, duplicate.Code);
    }

    [Fact]
    public void EventWithoutActiveSessionOrHandler_IsIgnored()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);

        var noSession = coordinator.ProcessEvent(new SessionEvent("tick"));
        Assert.False(noSession.Processed);
        Assert.Equal(SessionCodes.SessionRequired, noSession.Code);

        coordinator.EnterSlot("slot-a");
        var noHandler = coordinator.ProcessEvent(new SessionEvent("tick"));
        Assert.False(noHandler.Processed);
        Assert.Equal(SessionCodes.NoHandler, noHandler.Code);

        coordinator.Exit();
        var afterExit = coordinator.ProcessEvent(new SessionEvent("tick"));
        Assert.False(afterExit.Processed);
        Assert.Equal(SessionCodes.SessionRequired, afterExit.Code);
    }

    [Fact]
    public void HandlerCanExitDuringDispatchWithoutCorruptingLifecycle()
    {
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore());
        coordinator.EnterSlot("slot-a");
        var contexts = new List<SessionEventContext>();
        Assert.True(coordinator.RegisterEventHandler("stop", context =>
        {
            contexts.Add(context);
            Assert.True(coordinator.Exit().Succeeded);
        }).Registered);

        var result = coordinator.ProcessEvent(new SessionEvent("stop", "event-1"));

        Assert.True(result.Processed);
        Assert.Single(contexts);
        Assert.Equal(SlotSessionStatus.NoSession, coordinator.Status);
        Assert.True(contexts[0].CancellationToken.IsCancellationRequested);
        Assert.False(coordinator.ProcessEvent(new SessionEvent("stop", "event-2")).Processed);
    }

    [Fact]
    public void HandlerCanSwitchSlotDuringDispatchAndOldEventDoesNotReachNewSession()
    {
        var events = new RecordingSessionEventPort();
        using var coordinator = new SlotSessionCoordinator(new RecordingSlotStateStore(), events);
        coordinator.EnterSlot("slot-a");
        var oldHandled = 0;
        var newHandled = 0;
        Assert.True(coordinator.RegisterEventHandler("switch", _ =>
        {
            oldHandled++;
            Assert.Equal(SessionCodes.Switched, coordinator.EnterSlot("slot-b").Code);
            Assert.True(coordinator.RegisterEventHandler("switch", _ => newHandled++).Registered);
        }).Registered);

        var result = coordinator.ProcessEvent(new SessionEvent("switch", "event-1"));

        Assert.True(result.Processed);
        Assert.Equal(1, oldHandled);
        Assert.Equal(0, newHandled);

        events.Publish(new SessionEvent("switch", "event-2"));

        Assert.Equal(1, oldHandled);
        Assert.Equal(1, newHandled);
    }

    private static string Raw(string slotId, string charactersJson)
    {
        return TestStateFactory.RawState(SchemaVersionPolicy.Current, slotId, charactersJson);
    }
}
