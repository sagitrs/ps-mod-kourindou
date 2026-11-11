namespace Sagitrs.Kourindou.Core.Characters;

/// <summary>
/// A confirmed V1 character definition. DisplayNameKey is a content key, not a
/// game resource key or a stable identity.
/// </summary>
public sealed record CharacterDefinition(CharacterId Id, string DisplayNameKey, RegionId Region);
