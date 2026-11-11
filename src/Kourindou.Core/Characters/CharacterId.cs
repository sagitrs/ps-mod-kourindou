namespace Sagitrs.Kourindou.Core.Characters;

/// <summary>
/// Stable character identity. The value is the serialized identity and must not
/// be a display name, portrait key, or transient customer instance id.
/// </summary>
public readonly record struct CharacterId(string Value)
{
    public static readonly CharacterId Alice = new("alice");
    public static readonly CharacterId Marisa = new("marisa");
    public static readonly CharacterId Sakuya = new("sakuya");

    public bool IsKnown => CharacterCatalog.Contains(this);

    public override string ToString() => Value ?? string.Empty;

    public static CharacterId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Character id must not be empty.", nameof(value));
        }

        return new CharacterId(value);
    }

    public static bool TryParse(string? value, out CharacterId id)
    {
        if (!string.IsNullOrWhiteSpace(value) && CharacterCatalog.Contains(new CharacterId(value)))
        {
            id = new CharacterId(value);
            return true;
        }

        id = default;
        return false;
    }
}
