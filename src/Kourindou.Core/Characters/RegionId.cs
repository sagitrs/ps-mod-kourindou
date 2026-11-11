namespace Sagitrs.Kourindou.Core.Characters;

/// <summary>
/// Stable region identity. Display names are a separate, undecided concern (D01).
/// </summary>
public readonly record struct RegionId(string Value)
{
    public static readonly RegionId Lower = new("lower");
    public static readonly RegionId Middle = new("middle");
    public static readonly RegionId Upper = new("upper");

    public override string ToString() => Value ?? string.Empty;
}
