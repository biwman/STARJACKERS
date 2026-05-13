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
            OutputItemId = InventoryItemCatalog.AsteroidEpicId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "epic_to_legendary_asteroid",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidEpicId,
                InventoryItemCatalog.AsteroidEpicId,
                InventoryItemCatalog.AsteroidEpicId,
                InventoryItemCatalog.AsteroidEpicId
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
            Id = "triple_gun_from_common_asteroids",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId
            },
            OutputItemId = InventoryItemCatalog.TripleGunId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "artillery_gun_from_mid_asteroids",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidRareId
            },
            OutputItemId = InventoryItemCatalog.ArtilleryGunId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "rail_gun_from_high_grade_salvage",
            Inputs = new[]
            {
                InventoryItemCatalog.CorsairSalvageId,
                InventoryItemCatalog.NeutralFighterSalvageId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.RichAsteroidId
            },
            OutputItemId = InventoryItemCatalog.RailGunId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "double_ionizer_from_fighter_salvage",
            Inputs = new[]
            {
                InventoryItemCatalog.NeutralFighterSalvageId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidRareId
            },
            OutputItemId = InventoryItemCatalog.DoubleIonizerId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "astro_cutter_from_radar_salvage",
            Inputs = new[]
            {
                InventoryItemCatalog.RadarShipSalvageId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.SpaceJunkStandardId
            },
            OutputItemId = InventoryItemCatalog.AstroCutterId,
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
            Id = "fuel_tank_from_truck_wreck",
            Inputs = new[]
            {
                InventoryItemCatalog.SpaceTruckWreckId,
                InventoryItemCatalog.SpaceJunkStandardId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidResourceId
            },
            OutputItemId = InventoryItemCatalog.FuelTankId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "super_booster_from_fusion_engine",
            Inputs = new[]
            {
                InventoryItemCatalog.FusionEngineId,
                InventoryItemCatalog.SpaceTruckWreckId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.SuperBoosterId,
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
            Id = "lure_beacon_from_asteroids",
            Inputs = new[]
            {
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidResourceId,
                InventoryItemCatalog.AsteroidGoldId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.LureBeaconId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "auto_turret_from_fighter_salvage",
            Inputs = new[]
            {
                InventoryItemCatalog.NeutralFighterSalvageId,
                InventoryItemCatalog.SpaceMineWreckId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.AutoTurretId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "guidance_system_from_radar_salvage",
            Inputs = new[]
            {
                InventoryItemCatalog.RadarShipSalvageId,
                InventoryItemCatalog.SpaceJunkStandardId,
                InventoryItemCatalog.AsteroidRareId,
                InventoryItemCatalog.DroidScrapId
            },
            OutputItemId = InventoryItemCatalog.GuidanceSystemId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "looting_friend_from_tractor_beam",
            Inputs = new[]
            {
                InventoryItemCatalog.TractorBeamId,
                InventoryItemCatalog.DroidScrapId,
                InventoryItemCatalog.SpaceJunkStandardId,
                InventoryItemCatalog.AsteroidRareId
            },
            OutputItemId = InventoryItemCatalog.LootingFriendId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "space_drill_from_magnetic_beam",
            Inputs = new[]
            {
                InventoryItemCatalog.MagneticBeamId,
                InventoryItemCatalog.DroidScrapId,
                InventoryItemCatalog.SpaceMineWreckId,
                InventoryItemCatalog.AsteroidRareId
            },
            OutputItemId = InventoryItemCatalog.SpaceDrillId,
            OutputCount = 1
        },
        new PlayerProfileCraftingRecipe
        {
            Id = "space_trap_from_mine_wreck",
            Inputs = new[]
            {
                InventoryItemCatalog.SpaceMineWreckId,
                InventoryItemCatalog.SpaceMineWreckId,
                InventoryItemCatalog.SpaceJunkStandardId,
                InventoryItemCatalog.AsteroidGoldId
            },
            OutputItemId = InventoryItemCatalog.SpaceTrapId,
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

    public static int GetCraftingInputSellValueForOutput(string outputItemId)
    {
        PlayerProfileCraftingRecipe recipe = GetRecipeForOutput(outputItemId);
        if (recipe == null || recipe.Inputs == null)
            return 0;

        int total = 0;
        for (int i = 0; i < recipe.Inputs.Length; i++)
            total += InventoryItemCatalog.GetSellValueAstrons(recipe.Inputs[i]);

        return total;
    }

    public static PlayerProfileCraftingRecipe GetRecipeForOutput(string outputItemId)
    {
        if (string.IsNullOrWhiteSpace(outputItemId))
            return null;

        for (int i = 0; i < Recipes.Length; i++)
        {
            PlayerProfileCraftingRecipe recipe = Recipes[i];
            if (recipe != null && string.Equals(recipe.OutputItemId, outputItemId, StringComparison.Ordinal))
                return recipe;
        }

        return null;
    }

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
