namespace Arpg.Domain;

public enum MapCompletePhase
{
    RewardChoice,
    BuildManagement,
    RouteChoice,
}

/// <summary>
/// Atomic inventory/stash/crafting operations used by Build intermission.
/// The caller controls when the phase is available; this service controls the
/// item identity, currency and RNG transaction itself.
/// </summary>
public sealed class BuildcraftTransactions
{
    public bool TryMoveInventoryToStash(
        RunInventory inventory,
        Equipment equipment,
        Stash stash,
        string tabId,
        string itemId)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(stash);
        var item = inventory.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item == null || !stash.CanAccept(tabId, item))
        {
            return false;
        }

        var removed = inventory.Remove(itemId);
        if (removed == null || !stash.TryDeposit(tabId, removed))
        {
            if (removed != null)
            {
                inventory.TryAdd(removed, equipment);
            }

            return false;
        }

        return true;
    }

    public bool TryMoveStashToInventory(
        RunInventory inventory,
        Equipment equipment,
        Stash stash,
        string tabId,
        string itemId)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(stash);
        var item = stash.FindTab(tabId)?.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item == null || !inventory.CanAccept(item, equipment))
        {
            return false;
        }

        var withdrawn = stash.Withdraw(tabId, itemId);
        if (withdrawn == null || !inventory.TryAdd(withdrawn, equipment))
        {
            if (withdrawn != null)
            {
                stash.TryDeposit(tabId, withdrawn);
            }

            return false;
        }

        return true;
    }

    public bool TryReforge(
        CraftingRecipe recipe,
        string itemId,
        RunInventory inventory,
        Equipment equipment,
        Stash stash,
        RunCurrencyWallet wallet,
        RunSession session,
        out CraftingResult? result,
        out string error)
    {
        result = null;
        error = string.Empty;
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(stash);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(session);

        var inventoryItem = inventory.Items.FirstOrDefault(item => item.Id == itemId);
        var stashTab = stash.Tabs.FirstOrDefault(tab => tab.Contains(itemId));
        var input = inventoryItem ?? stashTab?.Items.FirstOrDefault(item => item.Id == itemId);
        if (input == null || inventoryItem != null && stashTab != null || equipment.ContainsItemId(itemId))
        {
            error = "crafting input must be in exactly one loose container";
            return false;
        }

        if (!recipe.CanCraft(input, wallet.ForgeFragments))
        {
            error = "recipe, item or currency is invalid";
            return false;
        }

        var previous = new SessionState(
            session.RunSeed,
            session.ItemSequence,
            session.MapLevel,
            session.LootRandom.State,
            session.CraftingRandom.State,
            session.EventRandom.State);
        var bench = new CraftingBench();
        Item? craftedItem = null;
        var inputRemoved = false;
        var currencySpent = false;
        var outputAdded = false;
        try
        {
            var crafted = bench.Craft(recipe, input, wallet.ForgeFragments, session.CreateCraftingGenerator());
            craftedItem = crafted.CraftedItem;
            if (inventoryItem != null)
            {
                inputRemoved = inventory.Remove(itemId) != null;
                currencySpent = inputRemoved && wallet.TrySpend(recipe.ForgeFragmentCost);
                outputAdded = currencySpent && inventory.TryAdd(crafted.CraftedItem, equipment);
                if (!inputRemoved || !currencySpent || !outputAdded)
                {
                    throw new InvalidOperationException("inventory crafting commit failed");
                }
            }
            else
            {
                inputRemoved = stashTab != null && stash.Withdraw(stashTab.Id, itemId) != null;
                currencySpent = inputRemoved && wallet.TrySpend(recipe.ForgeFragmentCost);
                outputAdded = currencySpent && stash.TryDeposit(stashTab!.Id, crafted.CraftedItem);
                if (!inputRemoved || !currencySpent || !outputAdded)
                {
                    throw new InvalidOperationException("stash crafting commit failed");
                }
            }

            result = crafted;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            if (outputAdded && craftedItem != null)
            {
                if (inventoryItem != null)
                {
                    inventory.Remove(craftedItem.Id);
                }
                else if (stashTab != null)
                {
                    stash.Withdraw(stashTab.Id, craftedItem.Id);
                }
            }

            if (currencySpent)
            {
                wallet.TryAdd(recipe.ForgeFragmentCost);
            }

            if (inputRemoved)
            {
                if (inventoryItem != null)
                {
                    inventory.TryAdd(input, equipment);
                }
                else if (stashTab != null)
                {
                    stash.TryDeposit(stashTab.Id, input);
                }
            }

            RestoreSession(session, previous);
            error = exception.Message;
            return false;
        }
    }

    private static void RestoreSession(RunSession session, SessionState state)
    {
        session.Restore(
            state.RunSeed,
            state.ItemSequence,
            state.MapLevel,
            state.LootRandomState,
            state.CraftingRandomState,
            state.EventRandomState);
    }

    private sealed record SessionState(
        ulong RunSeed,
        int ItemSequence,
        int MapLevel,
        ulong LootRandomState,
        ulong CraftingRandomState,
        ulong EventRandomState);
}
