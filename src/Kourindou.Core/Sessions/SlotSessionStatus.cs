namespace Sagitrs.Kourindou.Core.Sessions;

/// <summary>
/// Explicit lifecycle boundary: there is no session, a slot session is active,
/// or the coordinator has been permanently disabled/disposed.
/// </summary>
public enum SlotSessionStatus
{
    NoSession,
    Active,
    Disabled,
}
