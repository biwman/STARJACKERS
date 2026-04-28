using System;
using System.Collections.Generic;

public sealed class PlayerProfileCraftingRecipe
{
    public string Id;
    public string[] Inputs;
    public string OutputItemId;
    public int OutputCount;
}

public readonly struct PlayerProfileCraftingResult
{
    public readonly PlayerProfileCraftingRecipe Recipe;

    public PlayerProfileCraftingResult(PlayerProfileCraftingRecipe recipe)
    {
        Recipe = recipe;
    }
}

public static class PlayerProfileCraftingCatalog
{
    static readonly PlayerProfileCraftingRecipe[] Recipes =
    {
        new PlayerProfileCraftingRecipe
        {
            Id = "common_to_uncommon_asteroid",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId
            },
            OutputItemId = InventoryItemCatalog.AsteroidGoldId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "uncommon_to_rare_asteroid",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.AsteroidRareId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "rare_to_very_rare_asteroid",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidRareId
            },
            OutputItemId = InventoryItemCatalog.RichAsteroidId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "very_rare_to_epic_asteroid",
            Inputs = new[]
            {
                InventoryItemCatalog.RichAsteroidId,
                InventoryItemCatalog.RichAsteroidId,
                InventoryItemCatalog.RichAsteroidId,
                InventoryItemCatalog.RichAsteroidId
            },
            OutputItemId = InventoryItemCatalog.SpaceJunkId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "epic_to_legendary_asteroid",
            Inputs = new[]
            {
                InventoryItemCatalog.SpaceJunkId,
                InventoryItemCatalog.SpaceJunkId,
                InventoryItemCatalog.SpaceJunkId,
                InventoryItemCatalog.SpaceJunkId
            },
            OutputItemId = InventoryItemCatalog.AsteroidLegendaryId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "plasma_gun_from_corsair_salvage",
            Inputs = new[]
            {
                InventoryItemCatalog.CorsairSalvageId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.PlasmaGunId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "fusion_engine_from_droid_wreck",
            Inputs = new[]
            {
                InventoryItemCatalog.DroidScrapId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId
            },
            OutputItemId = InventoryItemCatalog.FusionEngineId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "gadget_mine_from_mine_wreck",
            Inputs = new[]
            {
                InventoryItemCatalog.SpaceMineWreckId,
                InventoryItemCatalog.SpaceMineWreckId,
                InventoryItemCatalog.SpaceMineWreckId,
                InventoryItemCatalog.SpaceMineWreckId
            },
            OutputItemId = InventoryItemCatalog.GadgetMineId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "battery_from_asteroids",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.BatteryId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "magnetic_beam_from_asteroids",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.MagneticBeamId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "tractor_beam_from_asteroids",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidRareId
            },
            OutputItemId = InventoryItemCatalog.TractorBeamId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "shield_reactor_from_truck_wreck",
            Inputs = new[]
            {
                InventoryItemCatalog.SpaceTruckWreckId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.ShieldReactorId,
            OutputCount = 1
        }
    };

    public static IReadOnlyList<PlayerProfileCraftingRecipe> GetAllRecipes() => Recipes;

    public static bool TryCraft(string[] craftingSlots, out PlayerProfileCraftingResult result)
    {
        result = default;

        if (craftingSlots == null)
            return false;

        List<string> occupiedItems = new List<string>(craftingSlots.Length);
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            string itemId = craftingSlots[i];
            if (!string.IsNullOrWhiteSpace(itemId))
                occupiedItems.Add(itemId);
        }

        if (occupiedItems.Count == 0)
            return false;

        for (int i = 0; i < Recipes.Length; i++)
        {
            PlayerProfileCraftingRecipe recipe = Recipes[i];
            if (recipe == null || recipe.Inputs == null || recipe.Inputs.Length != occupiedItems.Count)
                continue;

            if (Matches(recipe.Inputs, occupiedItems))
            {
                result = new PlayerProfileCraftingResult(recipe);
                return true;
            }
        }

        return false;
    }

    static bool Matches(string[] recipeInputs, List<string> occupiedItems)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < recipeInputs.Length; i++)
        {
            string itemId = recipeInputs[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int currentCount);
            counts[itemId] = currentCount + 1;
        }

        for (int i = 0; i < occupiedItems.Count; i++)
        {
            string itemId = occupiedItems[i];
            if (!counts.TryGetValue(itemId, out int currentCount) || currentCount <= 0)
                return false;

            if (currentCount == 1)
                counts.Remove(itemId);
            else
                counts[itemId] = currentCount - 1;
        }

        return counts.Count == 0;
    }
}
