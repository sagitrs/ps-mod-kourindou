using Sagitrs.Kourindou.Core.State;

namespace Sagitrs.Kourindou.Core.Sessions;

/// <summary>
/// Game-independent per-slot session and lifecycle coordination. The caller
/// explicitly enters a slot, loads a payload through the storage port, saves
/// snapshots of the loaded state and exits or disables the session. A failed
/// load invalidates the usable state, so no blank "new game" can be written by
/// accident.
/// </summary>
/// <remarks>
/// This type is intentionally single-threaded and not thread-safe. Callers must
/// serialize access. It does not read game files, call Unity APIs or claim that
/// native hooks exist; event and storage behaviour are supplied by the
/// project-owned ports only.
/// </remarks>
public sealed class SlotSessionCoordinator : IDisposable
{
    public const int DefaultMaxEventHandlers = 8;
    public const int DefaultMaxRememberedEventIds = 64;

    private readonly ISlotStateStore _store;
    private readonly ISessionEventPort? _eventPort;
    private readonly int _maxEventHandlers;
    private readonly int _maxRememberedEventIds;
    private readonly Dictionary<string, HandlerRegistration> _handlers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rememberedEventIds = new(StringComparer.Ordinal);

    private SaveEnvelope? _state;
    private string? _slotId;
    private CancellationTokenSource? _sessionCancellation;
    private IDisposable? _eventSubscription;
    private SlotSessionStatus _status = SlotSessionStatus.NoSession;
    private long _generation;
    private bool _saving;
    private int _transitionDepth;
    private bool _disposed;

    private bool IsTransitioning => _transitionDepth > 0;

    public SlotSessionCoordinator(
        ISlotStateStore store,
        ISessionEventPort? eventPort = null,
        int maxEventHandlers = DefaultMaxEventHandlers,
        int maxRememberedEventIds = DefaultMaxRememberedEventIds)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (maxEventHandlers <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEventHandlers), maxEventHandlers, "At least one event handler must be allowed.");
        }

        if (maxRememberedEventIds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRememberedEventIds), maxRememberedEventIds, "At least one event id must be remembered.");
        }

        _store = store;
        _eventPort = eventPort;
        _maxEventHandlers = maxEventHandlers;
        _maxRememberedEventIds = maxRememberedEventIds;
    }

    public SlotSessionStatus Status => _status;

    public string? ActiveSlotId => _slotId;

    public bool HasUsableState => _status == SlotSessionStatus.Active && _state is not null;

    /// <summary>
    /// Monotonic session generation. It changes whenever a new session starts,
    /// including when switching slots or re-entering after exit.
    /// </summary>
    public long Generation => _generation;

    public int RegisteredEventHandlerCount => _handlers.Count;

    public CancellationToken CurrentCancellationToken =>
        _status == SlotSessionStatus.Active && _sessionCancellation is not null
            ? _sessionCancellation.Token
            : CancellationToken.None;

    /// <summary>
    /// Enters an explicit slot session without inventing state. Re-entering the
    /// active slot is idempotent and does not add another event subscription.
    /// Switching to a different slot releases the previous session without
    /// saving it; call <see cref="SaveSnapshot"/> first when it must persist.
    /// </summary>
    public SessionOperationResult EnterSlot(string slotId)
    {
        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return SessionOperationResult.Failure(SessionCodes.SessionDisabled);
        }

        if (IsTransitioning)
        {
            return SessionOperationResult.Failure(SessionCodes.TransitionInProgress);
        }

        if (string.IsNullOrWhiteSpace(slotId))
        {
            return SessionOperationResult.Failure(SessionCodes.InvalidSlotId);
        }

        if (_status == SlotSessionStatus.Active
            && string.Equals(_slotId, slotId, StringComparison.Ordinal))
        {
            return SessionOperationResult.Success(SessionCodes.AlreadyActive);
        }

        var switched = _status == SlotSessionStatus.Active;
        if (switched)
        {
            RunTransition(ReleaseSession);
        }

        var started = StartSession(slotId);
        return started
            ? SessionOperationResult.Success(switched ? SessionCodes.Switched : SessionCodes.Entered)
            : SessionOperationResult.Failure(SessionCodes.SessionStartSuperseded);
    }

    /// <summary>
    /// Loads a raw payload only into the active slot. A rejected payload leaves
    /// the session without usable state; any previously stored slot payload is
    /// preserved by the store and is never overwritten by this call.
    /// </summary>
    public StateLoadResult Load(string slotId, string? rawPayload)
    {
        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return RejectedLoad(SessionCodes.SessionDisabled, "Session coordinator is disabled.", rawPayload);
        }

        if (_status != SlotSessionStatus.Active)
        {
            return RejectedLoad(SessionCodes.SessionRequired, "No active slot session; enter a slot before loading.", rawPayload);
        }

        if (!string.Equals(_slotId, slotId, StringComparison.Ordinal))
        {
            return RejectedLoad(
                SessionCodes.SessionSlotMismatch,
                $"Active slot is '{_slotId}', not '{slotId}'.",
                rawPayload);
        }

        var result = _store.Load(slotId, rawPayload);
        if (!result.IsUsable || result.State is null)
        {
            _state = null;
            return result;
        }

        if (!string.Equals(result.State.SlotId, slotId, StringComparison.Ordinal))
        {
            _state = null;
            return RejectedLoad(
                StateDiagnosticCodes.SlotMismatch,
                $"Loaded state belongs to slot '{result.State.SlotId}', not '{slotId}'.",
                result.RawPayload);
        }

        _state = SaveEnvelopeSnapshot.Copy(result.State);
        return result;
    }

    /// <summary>
    /// Saves an independent snapshot of the loaded state into the active slot.
    /// Saving is rejected when there is no session, no usable state (for example
    /// after a rejected load), or when called re-entrantly from the storage port.
    /// </summary>
    public StateSaveResult SaveSnapshot()
    {
        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return RejectedSave(SessionCodes.SessionDisabled, "Session coordinator is disabled.");
        }

        if (_status != SlotSessionStatus.Active)
        {
            return RejectedSave(SessionCodes.SessionRequired, "No active slot session to save.");
        }

        if (_state is null)
        {
            return RejectedSave(
                SessionCodes.NoUsableState,
                "No usable state is loaded; a rejected load must not be saved as a blank new game.");
        }

        if (!string.Equals(_state.SlotId, _slotId, StringComparison.Ordinal))
        {
            return RejectedSave(
                StateDiagnosticCodes.SlotMismatch,
                $"Loaded state belongs to slot '{_state.SlotId}', not '{_slotId}'.");
        }

        if (_saving)
        {
            return RejectedSave(SessionCodes.ReentrantSave, "A snapshot save is already in progress.");
        }

        _saving = true;
        try
        {
            return _store.Save(_slotId!, SaveEnvelopeSnapshot.Copy(_state));
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>
    /// Registers one bounded handler for an event name in the active session.
    /// Event names are unique per session; the handle is idempotently disposable
    /// and is also released automatically on exit, switch or disable.
    /// </summary>
    public SessionEventRegistration RegisterEventHandler(
        string eventName,
        Action<SessionEventContext> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return SessionEventRegistration.Failure(SessionCodes.SessionDisabled);
        }

        if (_status != SlotSessionStatus.Active)
        {
            return SessionEventRegistration.Failure(SessionCodes.SessionRequired);
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            return SessionEventRegistration.Failure(SessionCodes.InvalidEventName);
        }

        if (_handlers.ContainsKey(eventName))
        {
            return SessionEventRegistration.Failure(SessionCodes.DuplicateHandler);
        }

        if (_handlers.Count >= _maxEventHandlers)
        {
            return SessionEventRegistration.Failure(SessionCodes.HandlerLimitReached);
        }

        var registration = new HandlerRegistration(this, eventName, handler);
        _handlers.Add(eventName, registration);
        return SessionEventRegistration.Success(registration);
    }

    /// <summary>
    /// Routes one event to the current session only. Duplicate event ids are
    /// ignored within the session and the remembered-id window is bounded; when
    /// full, further events are rejected instead of growing memory without limit.
    /// </summary>
    /// <remarks>
    /// An event id is remembered before its handler runs, so a handler exception
    /// consumes that id and a retry is reported as <c>duplicate_event</c>. This
    /// is intentional at-most-once processing within the session. Remembered ids
    /// survive a successful <see cref="Load"/> in the same session and are only
    /// cleared when the session ends (exit, slot switch or disable).
    /// </remarks>
    public SessionEventDispatchResult ProcessEvent(SessionEvent sessionEvent)
    {
        ArgumentNullException.ThrowIfNull(sessionEvent);

        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return Ignored(SessionCodes.SessionDisabled);
        }

        if (_status != SlotSessionStatus.Active)
        {
            return Ignored(SessionCodes.SessionRequired);
        }

        if (string.IsNullOrWhiteSpace(sessionEvent.Name))
        {
            return Ignored(SessionCodes.InvalidEventName);
        }

        if (!_handlers.TryGetValue(sessionEvent.Name, out var registration) || registration.IsDisposed)
        {
            return Ignored(SessionCodes.NoHandler);
        }

        var eventId = sessionEvent.EventId;
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            if (_rememberedEventIds.Contains(eventId))
            {
                return Ignored(SessionCodes.DuplicateEvent);
            }

            if (_rememberedEventIds.Count >= _maxRememberedEventIds)
            {
                return Ignored(SessionCodes.EventCapacityReached);
            }

            _rememberedEventIds.Add(eventId);
        }

        registration.Handler(new SessionEventContext(
            _slotId!,
            _generation,
            _sessionCancellation!.Token,
            sessionEvent));
        return new SessionEventDispatchResult(true, SessionCodes.Handled);
    }

    /// <summary>
    /// Leaves the active slot, cancels and releases its event registrations and
    /// token, and drops the loaded state. Repeating exit is an idempotent no-op.
    /// </summary>
    public SessionOperationResult Exit()
    {
        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return SessionOperationResult.Failure(SessionCodes.SessionDisabled);
        }

        if (IsTransitioning)
        {
            return SessionOperationResult.Failure(SessionCodes.TransitionInProgress);
        }

        if (_status != SlotSessionStatus.Active)
        {
            return SessionOperationResult.Success(SessionCodes.AlreadyExited);
        }

        RunTransition(ReleaseSession);
        return SessionOperationResult.Success(SessionCodes.Exited);
    }

    /// <summary>
    /// Permanently disables the coordinator after releasing the current session.
    /// A disabled coordinator rejects all later enter/load/save/register calls.
    /// </summary>
    public SessionOperationResult Disable()
    {
        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return SessionOperationResult.Success(SessionCodes.AlreadyDisabled);
        }

        RunTransition(() =>
        {
            ReleaseSession();
            _status = SlotSessionStatus.Disabled;
        });
        return SessionOperationResult.Success(SessionCodes.Disabled);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Disable();
        _disposed = true;
    }

    private bool StartSession(string slotId)
    {
        if (_disposed || _status == SlotSessionStatus.Disabled)
        {
            return false;
        }

        _generation++;
        var generation = _generation;
        _status = SlotSessionStatus.Active;
        _slotId = slotId;
        _state = null;
        _rememberedEventIds.Clear();
        _sessionCancellation = new CancellationTokenSource();

        if (_eventPort is null)
        {
            return true;
        }

        IDisposable? subscription = null;
        try
        {
            // A port may invoke the handler synchronously before returning its
            // handle. Keep the transition guard active so callbacks cannot
            // start/end another session and overwrite the handle being created.
            RunTransition(() =>
            {
                subscription = _eventPort.Subscribe(
                    sessionEvent => OnPortEvent(generation, sessionEvent));
            });
        }
        catch
        {
            RunTransition(ReleaseSession);
            throw;
        }

        // Defense in depth for a port that still changes lifecycle state while
        // the guard is active (for example by calling Disable). Never retain a
        // handle that does not belong to the current generation.
        if (_status != SlotSessionStatus.Active
            || _generation != generation
            || _eventSubscription is not null)
        {
            subscription?.Dispose();
            return false;
        }

        _eventSubscription = subscription;
        return true;
    }

    private void ReleaseSession()
    {
        var registrations = _handlers.Values.ToArray();
        _handlers.Clear();
        foreach (var registration in registrations)
        {
            registration.MarkDisposedByOwner();
        }

        var subscription = _eventSubscription;
        _eventSubscription = null;

        var cancellation = _sessionCancellation;
        _sessionCancellation = null;

        // Invalidate the session before releasing the subscription or cancelling
        // the token, so a late/cancellation callback cannot save, route an event
        // or start a new session through the session that is ending.
        _state = null;
        _slotId = null;
        _rememberedEventIds.Clear();
        _status = SlotSessionStatus.NoSession;

        subscription?.Dispose();

        if (cancellation is not null)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed by an owner; the session is ending anyway.
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }

    private void RunTransition(Action transition)
    {
        _transitionDepth++;
        try
        {
            transition();
        }
        finally
        {
            _transitionDepth--;
        }
    }

    private void OnPortEvent(long generation, SessionEvent sessionEvent)
    {
        if (_disposed || _status != SlotSessionStatus.Active || generation != _generation)
        {
            return;
        }

        ProcessEvent(sessionEvent);
    }

    private void RemoveHandler(string eventName, HandlerRegistration registration)
    {
        if (_handlers.TryGetValue(eventName, out var current) && ReferenceEquals(current, registration))
        {
            _handlers.Remove(eventName);
        }
    }

    private static SessionEventDispatchResult Ignored(string code) => new(false, code);

    private static StateLoadResult RejectedLoad(string code, string message, string? rawPayload)
    {
        return new StateLoadResult(
            false,
            null,
            [new StateDiagnostic(code, message, StateDiagnosticSeverity.Error)],
            rawPayload ?? string.Empty);
    }

    private static StateSaveResult RejectedSave(string code, string message)
    {
        return new StateSaveResult(
            false,
            [new StateDiagnostic(code, message, StateDiagnosticSeverity.Error)]);
    }

    private sealed class HandlerRegistration : IDisposable
    {
        private readonly SlotSessionCoordinator _owner;

        public HandlerRegistration(
            SlotSessionCoordinator owner,
            string eventName,
            Action<SessionEventContext> handler)
        {
            _owner = owner;
            EventName = eventName;
            Handler = handler;
        }

        public string EventName { get; }

        public Action<SessionEventContext> Handler { get; }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _owner.RemoveHandler(EventName, this);
        }

        public void MarkDisposedByOwner()
        {
            IsDisposed = true;
        }
    }
}
