using Sagitrs.Kourindou.Core.Characters;

namespace Sagitrs.Kourindou.Core.Tests;

public sealed class CharacterCatalogTests
{
    [Fact]
    public void Catalog_UsesConfirmedRegionMapping()
    {
        Assert.Equal(RegionId.Lower, CharacterCatalog.Get(CharacterId.Alice).Region);
        Assert.Equal(RegionId.Middle, CharacterCatalog.Get(CharacterId.Marisa).Region);
        Assert.Equal(RegionId.Upper, CharacterCatalog.Get(CharacterId.Sakuya).Region);
    }

    [Fact]
    public void TryParse_AcceptsOnlyStableIds_NotDisplayNames()
    {
        Assert.True(CharacterId.TryParse("alice", out var alice));
        Assert.Equal(CharacterId.Alice, alice);
        Assert.False(CharacterId.TryParse("爱丽丝", out _));
        Assert.False(CharacterId.TryParse("Alice", out _));
        Assert.False(CharacterId.TryParse("", out _));
        Assert.False(CharacterId.TryParse(null, out _));
    }

    [Fact]
    public void Catalog_AllHasThreeCharacters_WithDistinctIds()
    {
        Assert.Equal(3, CharacterCatalog.All.Count);
        Assert.Equal(
            3,
            CharacterCatalog.All
                .Select(definition => definition.Id.Value)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }
}
