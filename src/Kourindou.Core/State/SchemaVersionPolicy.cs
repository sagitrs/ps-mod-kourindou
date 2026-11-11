namespace Sagitrs.Kourindou.Core.State;

/// <summary>
/// Version boundary for the game-independent state envelope. No migration is
/// registered for older versions in this slice; unknown versions are rejected
/// with the original payload preserved.
/// </summary>
public static class SchemaVersionPolicy
{
    public const int Current = 1;

    public static bool IsCurrent(int version) => version == Current;
}
