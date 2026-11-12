using System.Collections.ObjectModel;

namespace Sagitrs.Kourindou.Core.Characters;

/// <summary>
/// Confirmed V1 mapping: Alice/lower, Marisa/middle, Sakuya/upper.
/// This catalog is game-independent and does not claim any game API.
/// </summary>
public static class CharacterCatalog
{
    private static readonly CharacterDefinition[] Definitions =
    [
        new(CharacterId.Alice, "character.alice.name", RegionId.Lower),
        new(CharacterId.Marisa, "character.marisa.name", RegionId.Middle),
        new(CharacterId.Sakuya, "character.sakuya.name", RegionId.Upper),
    ];

    private static readonly ReadOnlyCollection<CharacterDefinition> AllDefinitions =
        Array.AsReadOnly(Definitions);

    private static readonly Dictionary<string, CharacterDefinition> ById =
        Definitions.ToDictionary(definition => definition.Id.Value, StringComparer.Ordinal);

    public static IReadOnlyList<CharacterDefinition> All => AllDefinitions;

    public static bool Contains(CharacterId id)
    {
        return id.Value is not null && ById.ContainsKey(id.Value);
    }

    public static bool TryGet(CharacterId id, out CharacterDefinition definition)
    {
        if (id.Value is not null && ById.TryGetValue(id.Value, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public static CharacterDefinition Get(CharacterId id)
    {
        return TryGet(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown character id '{id}'.");
    }

    public static bool TryGetById(string? id, out CharacterDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(id) && ById.TryGetValue(id, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }
}
