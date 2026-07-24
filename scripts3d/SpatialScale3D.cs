using System;

/// <summary>
/// The only boundary between legacy 2D gameplay distances and 3D metres.
/// Keeping this conversion in one place prevents each skill from inventing a
/// slightly different magic multiplier.
/// </summary>
public static class SpatialScale3D
{
    public const float LegacyUnitsToMeters = 0.025f;

    public static float Distance(double legacyUnits)
    {
        if (double.IsNaN(legacyUnits)
            || double.IsInfinity(legacyUnits)
            || legacyUnits < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(legacyUnits));
        }

        return (float)legacyUnits * LegacyUnitsToMeters;
    }
}
