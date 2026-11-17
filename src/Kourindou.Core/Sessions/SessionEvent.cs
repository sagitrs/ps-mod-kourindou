using System.Text.Json;

namespace Sagitrs.Kourindou.Core.Sessions;

/// <summary>
/// Opaque event delivered through <see cref="ISessionEventPort"/>. The core
/// routes it to a handler registered for the same session; it does not
/// interpret the name, id or payload as game semantics.
/// </summary>
public sealed record SessionEvent(string Name, string? EventId = null, JsonElement? Payload = null);
