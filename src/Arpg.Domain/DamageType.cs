namespace Arpg.Domain;

public enum DamageType
{
    Physical,
    Fire,
    Cold,
    Lightning,
    Poison,
}

public static class DamageTypeExtensions
{
    public static string DisplayName(this DamageType type)
    {
        return type switch
        {
            DamageType.Physical => "Physical",
            DamageType.Fire => "Fire",
            DamageType.Cold => "Cold",
            DamageType.Lightning => "Lightning",
            DamageType.Poison => "Poison",
            _ => "Unknown",
        };
    }
}
