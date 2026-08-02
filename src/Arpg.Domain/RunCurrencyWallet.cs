namespace Arpg.Domain;

/// <summary>
/// Run-scoped forge currency. Callers cannot mutate the integer directly;
/// spending and restoring are validated transactions.
/// </summary>
public sealed class RunCurrencyWallet
{
    public RunCurrencyWallet(int forgeFragments = 0)
    {
        if (forgeFragments < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(forgeFragments));
        }

        ForgeFragments = forgeFragments;
    }

    public int ForgeFragments { get; private set; }

    public bool TryAdd(int amount)
    {
        if (amount < 0 || ForgeFragments > int.MaxValue - amount)
        {
            return false;
        }

        ForgeFragments += amount;
        return true;
    }

    public bool CanSpend(int amount) => amount >= 0 && ForgeFragments >= amount;

    public bool TrySpend(int amount)
    {
        if (!CanSpend(amount))
        {
            return false;
        }

        ForgeFragments -= amount;
        return true;
    }

    public bool Restore(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        ForgeFragments = amount;
        return true;
    }
}
