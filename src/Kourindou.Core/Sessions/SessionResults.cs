namespace Sagitrs.Kourindou.Core.Sessions;

/// <summary>
/// Stable result codes for session transitions and event routing. They are
/// diagnostics for callers/tests, not game event names.
/// </summary>
public static class SessionCodes
{
    public const string Entered = "entered";
    public const string AlreadyActive = "already_active";
    public const string Switched = "switched";
    public const string Exited = "exited";
    public const string AlreadyExited = "already_exited";
    public const string Disabled = "disabled";
    public const string AlreadyDisabled = "already_disabled";
    public const string InvalidSlotId = "invalid_slot_id";
    public const string TransitionInProgress = "transition_in_progress";
    public const string SessionRequired = "session_required";
    public const string SessionDisabled = "session_disabled";
    public const string SessionSlotMismatch = "session_slot_mismatch";
    public const string NoUsableState = "no_usable_state";
    public const string ReentrantSave = "reentrant_save";
    public const string HandlerRegistered = "handler_registered";
    public const string DuplicateHandler = "duplicate_handler";
    public const string HandlerLimitReached = "handler_limit_reached";
    public const string InvalidEventName = "invalid_event_name";
    public const string Handled = "handled";
    public const string NoHandler = "no_handler";
    public const string DuplicateEvent = "duplicate_event";
    public const string EventCapacityReached = "event_capacity_reached";
}

/// <summary>Outcome of an explicit session transition.</summary>
public sealed record SessionOperationResult(bool Succeeded, string Code)
{
    public static SessionOperationResult Success(string code) => new(true, code);

    public static SessionOperationResult Failure(string code) => new(false, code);
}

/// <summary>
/// Outcome of a bounded event-handler registration. A successful result carries
/// the idempotently disposable handle owned by the caller.
/// </summary>
public sealed record SessionEventRegistration(bool Registered, string Code, IDisposable? Handle)
{
    public static SessionEventRegistration Success(IDisposable handle) =>
        new(true, SessionCodes.HandlerRegistered, handle);

    public static SessionEventRegistration Failure(string code) =>
        new(false, code, null);
}

/// <summary>Outcome of routing one event to the current session.</summary>
public sealed record SessionEventDispatchResult(bool Processed, string Code);
