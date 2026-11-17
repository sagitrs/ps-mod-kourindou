using Sagitrs.Kourindou.Core.Sessions;
using Sagitrs.Kourindou.Core.State;

namespace Sagitrs.Kourindou.Core.Tests;

internal sealed class RecordingSlotStateStore : ISlotStateStore
{
    private readonly InMemorySaveSlotStore _inner = new();

    public int LoadCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public StateLoadResult Load(string slotId, string? rawPayload)
    {
        LoadCalls++;
        return _inner.Load(slotId, rawPayload);
    }

    public StateSaveResult Save(string slotId, SaveEnvelope state)
    {
        SaveCalls++;
        return _inner.Save(slotId, state);
    }

    public StateSaveResult Seed(string slotId, SaveEnvelope state)
    {
        return _inner.Save(slotId, state);
    }

    public bool TryGet(string slotId, out SaveEnvelope? state)
    {
        return _inner.TryGet(slotId, out state);
    }
}

internal sealed class RecordingSessionEventPort : ISessionEventPort
{
    public List<Registration> Registrations { get; } = [];

    /// <summary>
    /// One-shot action invoked synchronously from <see cref="Subscribe"/> before
    /// the handle is returned. Used to exercise reentrant synchronous delivery.
    /// </summary>
    public Action? OnSubscribe { get; set; }

    public int ActiveSubscriptionCount => Registrations.Count(registration => !registration.IsDisposed);

    public IDisposable Subscribe(Action<SessionEvent> handler)
    {
        var onSubscribe = OnSubscribe;
        OnSubscribe = null;
        onSubscribe?.Invoke();

        var registration = new Registration(handler);
        Registrations.Add(registration);
        return registration;
    }

    public void Publish(SessionEvent sessionEvent)
    {
        foreach (var registration in Registrations.Where(registration => !registration.IsDisposed).ToArray())
        {
            registration.Handler(sessionEvent);
        }
    }

    /// <summary>
    /// Delivers the callback even though its handle may be disposed. This models
    /// a source that already queued or raced a callback during teardown.
    /// </summary>
    public void DeliverLate(int registrationIndex, SessionEvent sessionEvent)
    {
        Registrations[registrationIndex].Handler(sessionEvent);
    }

    internal sealed class Registration : IDisposable
    {
        public Registration(Action<SessionEvent> handler)
        {
            Handler = handler;
        }

        public Action<SessionEvent> Handler { get; }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}

internal sealed class StubSlotStateStore : ISlotStateStore
{
    private static readonly StateSaveResult SaveSucceeded = new(true, Array.Empty<StateDiagnostic>());

    public Func<string, string?, StateLoadResult> LoadHandler { get; set; } =
        (_, rawPayload) => new StateLoadResult(
            false,
            null,
            [new StateDiagnostic(StateDiagnosticCodes.EmptyPayload, "stub load", StateDiagnosticSeverity.Error)],
            rawPayload ?? string.Empty);

    public Func<string, SaveEnvelope, StateSaveResult> SaveHandler { get; set; } = (_, _) => SaveSucceeded;

    public int LoadCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public string? LastSaveSlot { get; private set; }

    public SaveEnvelope? LastSavedState { get; private set; }

    public StateLoadResult Load(string slotId, string? rawPayload)
    {
        LoadCalls++;
        return LoadHandler(slotId, rawPayload);
    }

    public StateSaveResult Save(string slotId, SaveEnvelope state)
    {
        SaveCalls++;
        LastSaveSlot = slotId;
        LastSavedState = state;
        return SaveHandler(slotId, state);
    }
}
