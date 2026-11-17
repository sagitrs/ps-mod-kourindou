namespace Sagitrs.Kourindou.Core.Sessions;

/// <summary>
/// Session-bound event context. The cancellation token is cancelled when the
/// session exits, switches slots or is disabled; a handler must not treat a
/// callback as belonging to a newer session.
/// </summary>
public sealed record SessionEventContext(
    string SlotId,
    long Generation,
    CancellationToken CancellationToken,
    SessionEvent Event);
