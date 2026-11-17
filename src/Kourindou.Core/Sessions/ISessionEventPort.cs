namespace Sagitrs.Kourindou.Core.Sessions;

/// <summary>
/// Project-owned event registration port for the session lifecycle core. This
/// is a dependency contract only; it is not a Unity/game hook, does not claim
/// that native hooks are available, and is not a general-purpose plugin or
/// event platform.
/// </summary>
/// <remarks>
/// <see cref="Subscribe"/> returns an idempotently disposable handle. The
/// coordinator discards callbacks delivered for an ended session, so sources
/// are allowed to deliver a late callback after the handle was disposed.
/// </remarks>
public interface ISessionEventPort
{
    IDisposable Subscribe(Action<SessionEvent> handler);
}
