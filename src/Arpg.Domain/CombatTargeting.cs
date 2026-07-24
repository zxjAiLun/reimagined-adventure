namespace Arpg.Domain;

public static class CombatTargeting
{
    public static bool CanHit(DamageRequest request, ICombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);

        if (request.CanHitSameFaction
            || request.SourceFaction == CombatFaction.Neutral
            || target.Faction == CombatFaction.Neutral)
        {
            return true;
        }

        return request.SourceFaction != target.Faction;
    }
}
