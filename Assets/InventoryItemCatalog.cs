using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum InventoryItemType
{
    Resource,
    Equipment,
    Consumable,
    Quest,
    Misc
}

public enum InventoryItemCategory
{
    Treasure,
    SpaceJunk,
    Wreck,
    Weapon,
    Shield,
    Engine,
    Gadget,
    QuestItem,
    Resource,
    Misc
}

public enum InventoryItemRarity
{
    Common,
    Uncommon,
    Rare,
    VeryRare,
    Epic,
    Legendary
}

[Serializable]
public class InventoryItemDefinition
{
    public string Id;
    public string DisplayName;
    public string ShortLabel;
    public string Description;
    public InventoryItemType ItemType;
    public InventoryItemCategory Category;
    public InventoryItemRarity Rarity;
    public int SellValueAstrons;
    public int ShopBuyValueAstronsOverride = -1;
    public string IconResourcePath;
    public string IconSpriteName;
    public string ProjectFileName;
    public bool CanEnterSafePocket = true;
    public string[] SalvageOutputs;

    Sprite cachedIcon;

    public Sprite GetIcon()
    {
        if (cachedIcon != null)
            return cachedIcon;

        if (!string.IsNullOrWhiteSpace(IconResourcePath))
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(IconResourcePath);
            cachedIcon = GetNamedSprite(sprites, IconSpriteName);
            if (cachedIcon != null)
            {
                return cachedIcon;
            }

            cachedIcon = Resources.Load<Sprite>(IconResourcePath);
            if (cachedIcon != null)
                return cachedIcon;

            cachedIcon = GetLargestSprite(sprites);
            if (cachedIcon != null)
            {
                return cachedIcon;
            }

            Texture2D texture = Resources.Load<Texture2D>(IconResourcePath);
            if (texture != null)
            {
                float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
                cachedIcon = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                return cachedIcon;
            }
        }

#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(ProjectFileName))
            return null;

        string assetPath = "Assets/" + ProjectFileName;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (!string.IsNullOrWhiteSpace(IconSpriteName))
        {
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && string.Equals(sprite.name, IconSpriteName, StringComparison.Ordinal))
                {
                    cachedIcon = sprite;
                    return cachedIcon;
                }
            }
        }

        cachedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (cachedIcon != null)
            return cachedIcon;

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                cachedIcon = sprite;
                return cachedIcon;
            }
        }
#endif

        return null;
    }

    static Sprite GetNamedSprite(Sprite[] sprites, string spriteName)
    {
        if (sprites == null || sprites.Length == 0 || string.IsNullOrWhiteSpace(spriteName))
            return null;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate != null && string.Equals(candidate.name, spriteName, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            Rect rect = candidate.rect;
            float area = rect.width * rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }
}

public static class InventoryItemCatalog
{
    public const string AsteroidResourceId = "asteroid_resource";
    public const string AsteroidGoldId = "asteroid_gold_resource";
    public const string AsteroidRareId = "asteroid_rare_resource";
    public const string RichAsteroidId = "rich_asteroid_resource";
    public const string AsteroidEpicId = "asteroid_epic_resource";
    public const string AsteroidLegendaryId = "asteroid_legendary_resource";
    public const string AsteroidCommonId = AsteroidResourceId;
    public const string AsteroidUncommonId = AsteroidGoldId;
    public const string AsteroidVeryRareId = RichAsteroidId;
    public const string LegacySpaceJunkId = "space_junk";
    public const string SpaceJunkTrashId = "space_junk_trash";
    public const string SpaceJunkStandardId = "space_junk_standard";
    public const string SpaceJunkAsteroidId = "space_junk_asteroid";
    public const string SpaceJunkId = SpaceJunkStandardId;
    public const string ContainerIdPrefix = "container_";
    public const int ContainerVariantCount = 9;
    public const string RandomLootWreckIdPrefix = "random_loot_wreck_";
    public const int RandomLootWreckVariantCount = 9;
    public const string CashSuitcaseId = "cash_suitcase";
    public const string DroidScrapId = "droid_scrap";
    public const string CorsairSalvageId = "corsair_salvage";
    public const string SpaceMineWreckId = "space_mine_wreck";
    public const string SpaceTruckWreckId = "space_truck_wreck";
    public const string MothershipCoreId = "mothership_core";
    public const string NeutralFighterSalvageId = "neutral_fighter_salvage";
    public const string RadarShipSalvageId = "radar_ship_salvage";
    public const string RescueShipSalvageId = "rescue_ship_salvage";
    public const string PirateFighterSalvageId = "pirate_fighter_salvage";
    public const string PirateBaseCoreId = "pirate_base_core";
    public const string PlasmaGunId = "plasma_gun";
    public const string TripleGunId = "triple_gun";
    public const string ArtilleryGunId = "artillery_gun";
    public const string RailGunId = "rail_gun";
    public const string DoubleIonizerId = "double_ionizer";
    public const string AstroCutterId = "astro_cutter";
    public const string FusionEngineId = "fusion_engine";
    public const string FuelTankId = "fuel_tank";
    public const string SuperBoosterId = "super_booster";
    public const string GadgetMineId = "gadget_mine";
    public const string BatteryId = "battery";
    public const string MagneticBeamId = "magnetic_beam";
    public const string TractorBeamId = "tractor_beam";
    public const string LureBeaconId = "lure_beacon";
    public const string AutoTurretId = "auto_turret";
    public const string GuidanceSystemId = "guidance_system";
    public const string LootingFriendId = "looting_friend";
    public const string SpaceDrillId = "space_drill";
    public const string SpaceTrapId = "space_trap";
    public const string ShieldReactorId = "shield_reactor";
    public const string AlienTransmitterId = "alien_transmitter";

    static readonly Dictionary<string, InventoryItemDefinition> Definitions = BuildDefinitions();

    public static InventoryItemDefinition GetDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        Definitions.TryGetValue(itemId, out InventoryItemDefinition definition);
        return definition;
    }

    public static Sprite GetIcon(string itemId) => GetDefinition(itemId)?.GetIcon();

    public static bool CanEnterSafePocket(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition == null || definition.CanEnterSafePocket;
    }

    public static string GetContainerItemId(int variantIndex)
    {
        int clampedIndex = Mathf.Clamp(variantIndex, 0, ContainerVariantCount - 1);
        return ContainerIdPrefix + (clampedIndex + 1).ToString("00");
    }

    public static bool IsContainerItem(string itemId)
    {
        return GetContainerVariantIndex(itemId) >= 0;
    }

    public static int GetContainerVariantIndex(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            !itemId.StartsWith(ContainerIdPrefix, StringComparison.Ordinal) ||
            itemId.Length <= ContainerIdPrefix.Length)
        {
            return -1;
        }

        string suffix = itemId.Substring(ContainerIdPrefix.Length);
        if (!int.TryParse(suffix, out int oneBasedIndex))
            return -1;

        int zeroBasedIndex = oneBasedIndex - 1;
        return zeroBasedIndex >= 0 && zeroBasedIndex < ContainerVariantCount ? zeroBasedIndex : -1;
    }

    public static string[] GetContainerItemIds()
    {
        string[] itemIds = new string[ContainerVariantCount];
        for (int i = 0; i < itemIds.Length; i++)
            itemIds[i] = GetContainerItemId(i);

        return itemIds;
    }

    public static string GetRandomLootWreckItemId(int variantIndex)
    {
        int clampedIndex = Mathf.Clamp(variantIndex, 0, RandomLootWreckVariantCount - 1);
        return RandomLootWreckIdPrefix + (clampedIndex + 1).ToString("00");
    }

    public static bool IsRandomLootWreckItem(string itemId)
    {
        return GetRandomLootWreckVariantIndex(itemId) >= 0;
    }

    public static int GetRandomLootWreckVariantIndex(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            !itemId.StartsWith(RandomLootWreckIdPrefix, StringComparison.Ordinal) ||
            itemId.Length <= RandomLootWreckIdPrefix.Length)
        {
            return -1;
        }

        string suffix = itemId.Substring(RandomLootWreckIdPrefix.Length);
        if (!int.TryParse(suffix, out int oneBasedIndex))
            return -1;

        int zeroBasedIndex = oneBasedIndex - 1;
        return zeroBasedIndex >= 0 && zeroBasedIndex < RandomLootWreckVariantCount ? zeroBasedIndex : -1;
    }

    public static string[] GetEquipmentItemIdsByCategory(InventoryItemCategory category)
    {
        List<string> itemIds = new List<string>();
        foreach (InventoryItemDefinition definition in Definitions.Values)
        {
            if (definition == null ||
                definition.ItemType != InventoryItemType.Equipment ||
                definition.Category != category ||
                string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            itemIds.Add(definition.Id);
        }

        itemIds.Sort(StringComparer.Ordinal);
        return itemIds.ToArray();
    }

    public static IReadOnlyList<InventoryItemDefinition> GetAllDefinitions()
    {
        List<InventoryItemDefinition> definitions = new List<InventoryItemDefinition>(Definitions.Values);
        definitions.Sort((a, b) => string.CompareOrdinal(a?.DisplayName ?? string.Empty, b?.DisplayName ?? string.Empty));
        return definitions;
    }

    public static string GetShortLabel(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.ShortLabel))
            return definition.ShortLabel;

        if (string.IsNullOrWhiteSpace(itemId))
            return string.Empty;

        return itemId.Length <= 3 ? itemId.ToUpperInvariant() : itemId.Substring(0, 3).ToUpperInvariant();
    }

    public static string GetDisplayName(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            return definition.DisplayName;

        return itemId ?? string.Empty;
    }

    public static string GetDescription(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null ? definition.Description : string.Empty;
    }

    public static InventoryItemType GetItemType(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null ? definition.ItemType : InventoryItemType.Misc;
    }

    public static InventoryItemCategory GetCategory(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null ? definition.Category : InventoryItemCategory.Misc;
    }

    public static string GetCategoryLabel(string itemId)
    {
        return GetCategory(itemId) switch
        {
            InventoryItemCategory.Treasure => "Treasure",
            InventoryItemCategory.SpaceJunk => "Space Junk",
            InventoryItemCategory.Wreck => "Wreck",
            InventoryItemCategory.Weapon => "Weapon",
            InventoryItemCategory.Shield => "Shield",
            InventoryItemCategory.Engine => "Engine",
            InventoryItemCategory.Gadget => "Gadget",
            InventoryItemCategory.QuestItem => "Quest Items",
            InventoryItemCategory.Resource => "Resource",
            _ => "Misc"
        };
    }

    public static bool IsCompatibleWithEquipmentSlot(string itemId, int equipmentSlotIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        InventoryItemDefinition definition = GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        InventoryItemCategory expectedCategory = GetEquipmentSlotCategory(equipmentSlotIndex);
        return expectedCategory != InventoryItemCategory.Misc && definition.Category == expectedCategory;
    }

    public static InventoryItemCategory GetEquipmentSlotCategory(int equipmentSlotIndex)
    {
        if (equipmentSlotIndex == 0 || equipmentSlotIndex == 1)
            return InventoryItemCategory.Weapon;
        if (equipmentSlotIndex == 2 || equipmentSlotIndex == 3)
            return InventoryItemCategory.Shield;
        if (equipmentSlotIndex == 4 || equipmentSlotIndex == 5)
            return InventoryItemCategory.Engine;
        if (equipmentSlotIndex == 6 || equipmentSlotIndex == 7)
            return InventoryItemCategory.Gadget;

        return InventoryItemCategory.Misc;
    }

    public static InventoryItemRarity GetRarity(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null ? definition.Rarity : InventoryItemRarity.Common;
    }

    public static int GetSellValueAstrons(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null ? Mathf.Max(0, definition.SellValueAstrons) : 0;
    }

    public static int GetShopBuyValueAstrons(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return 0;

        if (definition.ShopBuyValueAstronsOverride > 0)
            return definition.ShopBuyValueAstronsOverride;

        int craftingCost = PlayerProfileCraftingCatalog.GetCraftingInputSellValueForOutput(itemId);
        if (craftingCost > 0)
            return craftingCost * 4;

        return Mathf.Max(0, definition.SellValueAstrons * 4);
    }

    public static string[] GetSalvageOutputs(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        if (definition?.SalvageOutputs == null || definition.SalvageOutputs.Length == 0)
            return Array.Empty<string>();

        string[] outputs = new string[definition.SalvageOutputs.Length];
        Array.Copy(definition.SalvageOutputs, outputs, outputs.Length);
        return outputs;
    }

    public static Color GetRarityColor(string itemId)
    {
        return GetRarityColor(GetRarity(itemId));
    }

    public static Color GetRarityColor(InventoryItemRarity rarity)
    {
        switch (rarity)
        {
            case InventoryItemRarity.Uncommon: return new Color(0.18f, 0.64f, 0.24f, 0.98f);
            case InventoryItemRarity.Rare: return new Color(0.16f, 0.42f, 0.94f, 0.98f);
            case InventoryItemRarity.VeryRare: return new Color(0.48f, 0.2f, 0.8f, 0.98f);
            case InventoryItemRarity.Epic: return new Color(0.5f, 0.05f, 0.16f, 0.98f);
            case InventoryItemRarity.Legendary: return new Color(0.92f, 0.68f, 0.08f, 0.98f);
            default: return new Color(1f, 1f, 1f, 0.98f);
        }
    }

    static Dictionary<string, InventoryItemDefinition> BuildDefinitions()
    {
        Dictionary<string, InventoryItemDefinition> definitions = new Dictionary<string, InventoryItemDefinition>(StringComparer.Ordinal)
        {
            [AsteroidResourceId] = new InventoryItemDefinition
            {
                Id = AsteroidResourceId,
                DisplayName = "Common Asteroid",
                ShortLabel = "COM",
                Description = "A common collectible asteroid resource.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Common,
                SellValueAstrons = 100,
                IconResourcePath = "treasure_asteroid_white_common_resource",
                ProjectFileName = "treasure_asteroid_white_common.png",
                SalvageOutputs = new[] { AsteroidResourceId }
            },
            [AsteroidGoldId] = new InventoryItemDefinition
            {
                Id = AsteroidGoldId,
                DisplayName = "Uncommon Asteroid",
                ShortLabel = "UNC",
                Description = "An uncommon collectible asteroid resource.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 200,
                IconResourcePath = "treasure_asteroid_green_uncommon_resource",
                ProjectFileName = "treasure_asteroid_green_uncommon.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
            },
            [AsteroidRareId] = new InventoryItemDefinition
            {
                Id = AsteroidRareId,
                DisplayName = "Rare Asteroid",
                ShortLabel = "RAR",
                Description = "A rare collectible asteroid resource.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 400,
                IconResourcePath = "treasure_asteroid_blue_rare_resource",
                ProjectFileName = "treasure_asteroid_blue_rare.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidGoldId }
            },
            [RichAsteroidId] = new InventoryItemDefinition
            {
                Id = RichAsteroidId,
                DisplayName = "Very Rare Asteroid",
                ShortLabel = "VRA",
                Description = "A very rare collectible asteroid resource.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.VeryRare,
                SellValueAstrons = 800,
                IconResourcePath = "treasure_asteroid_violet_very_rare_resource",
                ProjectFileName = "treasure_asteroid_violet_very_rare.png",
                SalvageOutputs = new[] { AsteroidRareId, AsteroidRareId }
            },
            [AsteroidEpicId] = new InventoryItemDefinition
            {
                Id = AsteroidEpicId,
                DisplayName = "Epic Asteroid",
                ShortLabel = "EPI",
                Description = "An epic collectible asteroid resource.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 1600,
                IconResourcePath = "treasure_asteroid_burgundy_epic_resource",
                ProjectFileName = "treasure_asteroid_burgundy_epic.png",
                SalvageOutputs = new[] { RichAsteroidId, RichAsteroidId }
            },
            [LegacySpaceJunkId] = CreateSpaceJunkDefinition(
                LegacySpaceJunkId,
                "Space Junk",
                "JNK",
                "A recoverable piece of drifting space junk. Useful as a future crafting component.",
                "space_junk_standard",
                "space_junk_standard.png"),
            [SpaceJunkTrashId] = CreateSpaceJunkDefinition(
                SpaceJunkTrashId,
                "Space Junk Trash",
                "JNT",
                "A battered cloud of recoverable cosmic scrap.",
                "space_junk_trash",
                "space_junk_trash.png"),
            [SpaceJunkStandardId] = CreateSpaceJunkDefinition(
                SpaceJunkStandardId,
                "Space Junk",
                "JNK",
                "A recoverable piece of drifting space junk. Useful as a future crafting component.",
                "space_junk_standard",
                "space_junk_standard.png"),
            [SpaceJunkAsteroidId] = CreateSpaceJunkDefinition(
                SpaceJunkAsteroidId,
                "Space Junk Asteroid",
                "JNA",
                "A resource-rich asteroid fragment tangled with abandoned space debris.",
                "space_junk_asteroid",
                "space_junk_asteroid.png"),
            [AsteroidLegendaryId] = new InventoryItemDefinition
            {
                Id = AsteroidLegendaryId,
                DisplayName = "Legendary Asteroid",
                ShortLabel = "LEG",
                Description = "A legendary collectible asteroid resource.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 3200,
                IconResourcePath = "treasure_asteroid_gold_legendary_resource",
                ProjectFileName = "treasure_asteroid_gold_legendary.png",
                SalvageOutputs = new[] { AsteroidEpicId, AsteroidEpicId }
            },
            [CashSuitcaseId] = new InventoryItemDefinition
            {
                Id = CashSuitcaseId,
                DisplayName = "Cash Suitcase",
                ShortLabel = "CSH",
                Description = "A sealed suitcase packed with high-value factory payout credits.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 5000,
                IconResourcePath = "Cash_Suitcase",
                ProjectFileName = "Resources/Cash_Suitcase.png",
                CanEnterSafePocket = false,
                SalvageOutputs = new[] { AsteroidCommonId }
            },
            [DroidScrapId] = new InventoryItemDefinition
            {
                Id = DroidScrapId,
                DisplayName = "Droid Wreck",
                ShortLabel = "BOT",
                Description = "A recoverable drone wreck fragment.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 300,
                IconResourcePath = "droid1_resource",
                ProjectFileName = "droid1.png",
                SalvageOutputs = new[] { AsteroidGoldId }
            },
            [CorsairSalvageId] = new InventoryItemDefinition
            {
                Id = CorsairSalvageId,
                DisplayName = "Corsair Salvage",
                ShortLabel = "CRS",
                Description = "Rare salvage recovered from a destroyed Corsair.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1200,
                IconResourcePath = "statek_duzy_resource",
                ProjectFileName = "statek_duzy.png",
                SalvageOutputs = new[] { AsteroidRareId, AsteroidRareId }
            },
            [SpaceMineWreckId] = new InventoryItemDefinition
            {
                Id = SpaceMineWreckId,
                DisplayName = "Mine Wreck",
                ShortLabel = "MIN",
                Description = "A disabled space mine hull recovered before detonation.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 300,
                IconResourcePath = "wrak_miny_resource",
                ProjectFileName = "wrak_miny.png",
                SalvageOutputs = new[] { AsteroidGoldId }
            },
            [SpaceTruckWreckId] = new InventoryItemDefinition
            {
                Id = SpaceTruckWreckId,
                DisplayName = "Space Truck Wreck",
                ShortLabel = "TRK",
                Description = "Heavy salvage recovered from a destroyed Space Truck.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 800,
                IconResourcePath = "space_truck_wrak_resource",
                ProjectFileName = "space_truck_wrak.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidGoldId }
            },
            [MothershipCoreId] = new InventoryItemDefinition
            {
                Id = MothershipCoreId,
                DisplayName = "Mothership Core",
                ShortLabel = "MTH",
                Description = "A legendary command core recovered from a destroyed Mothership.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 4000,
                IconResourcePath = "mother_ship_wrak_resource",
                ProjectFileName = "mother_ship_wrak.png",
                SalvageOutputs = new[] { AsteroidRareId, AsteroidRareId }
            },
            [NeutralFighterSalvageId] = new InventoryItemDefinition
            {
                Id = NeutralFighterSalvageId,
                DisplayName = "Neutral Fighter Salvage",
                ShortLabel = "NFS",
                Description = "Uncommon salvage recovered from a destroyed Neutral Fighter.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 500,
                IconResourcePath = "neutral_fighter_wreck_resource",
                ProjectFileName = "neutral_fighter_wreck.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
            },
            [RadarShipSalvageId] = new InventoryItemDefinition
            {
                Id = RadarShipSalvageId,
                DisplayName = "Radar Ship Salvage",
                ShortLabel = "RDS",
                Description = "Heavy salvage recovered from a destroyed Radar Ship spotter platform.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1800,
                IconResourcePath = "radar_ship_wreck_resource",
                ProjectFileName = "radar_ship_wreck.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidRareId }
            },
            [RescueShipSalvageId] = new InventoryItemDefinition
            {
                Id = RescueShipSalvageId,
                DisplayName = "Rescue Ship Salvage",
                ShortLabel = "RSC",
                Description = "Support-platform salvage recovered from a destroyed Rescue Ship.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1400,
                IconResourcePath = "rescue_ship_wreck_resource",
                ProjectFileName = "rescue_ship_wreck.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidGoldId, AsteroidResourceId }
            },
            [PirateFighterSalvageId] = new InventoryItemDefinition
            {
                Id = PirateFighterSalvageId,
                DisplayName = "Pirate Fighter Salvage",
                ShortLabel = "PFS",
                Description = "Rare combat salvage recovered from a destroyed Pirate Fighter.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 900,
                IconResourcePath = "pirate_fighter_wreck_resource",
                ProjectFileName = "pirate_fighter_wreck.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidResourceId }
            },
            [PirateBaseCoreId] = new InventoryItemDefinition
            {
                Id = PirateBaseCoreId,
                DisplayName = "Pirate Base Core",
                ShortLabel = "PBC",
                Description = "A legendary command core recovered from a destroyed Pirate Base.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 5000,
                IconResourcePath = "pirate_base_wreck_resource",
                ProjectFileName = "pirate_base_wreck.png",
                SalvageOutputs = new[] { AsteroidEpicId, AsteroidRareId }
            },
            [PlasmaGunId] = new InventoryItemDefinition
            {
                Id = PlasmaGunId,
                DisplayName = "Plasma Gun",
                ShortLabel = "PLS",
                Description = "A salvaged Corsair plasma cannon adapted for player ships.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 1800,
                ShopBuyValueAstronsOverride = 6500,
                IconResourcePath = "plasma_gun_resource",
                ProjectFileName = "plasma_gun.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidGoldId }
            },
            [TripleGunId] = new InventoryItemDefinition
            {
                Id = TripleGunId,
                DisplayName = "Triple Gun",
                ShortLabel = "TRI",
                Description = "A simple spread weapon based on the free starter gun. Fires a light three-shot burst.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Common,
                SellValueAstrons = 250,
                ShopBuyValueAstronsOverride = 900,
                IconResourcePath = "triple_gun_resource",
                ProjectFileName = "triple_gun.png",
                SalvageOutputs = new[] { AsteroidResourceId }
            },
            [ArtilleryGunId] = new InventoryItemDefinition
            {
                Id = ArtilleryGunId,
                DisplayName = "Artillery Gun",
                ShortLabel = "ART",
                Description = "An indirect-fire weapon that launches a hot shell in an arc and damages only the landing area.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 900,
                ShopBuyValueAstronsOverride = 3600,
                IconResourcePath = "artillery_gun_resource",
                ProjectFileName = "artillery_gun.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidResourceId }
            },
            [RailGunId] = new InventoryItemDefinition
            {
                Id = RailGunId,
                DisplayName = "Rail Gun",
                ShortLabel = "RLG",
                Description = "A long-range piercing rail weapon that fires short red-orange energy slugs at extreme speed.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 2900,
                ShopBuyValueAstronsOverride = 12000,
                IconResourcePath = "rail_gun_resource",
                ProjectFileName = "rail_gun.png",
                SalvageOutputs = new[] { CorsairSalvageId, AsteroidRareId }
            },
            [DoubleIonizerId] = new InventoryItemDefinition
            {
                Id = DoubleIonizerId,
                DisplayName = "Double Ionizer",
                ShortLabel = "ION",
                Description = "A dual ion weapon that fires paired blue charges tuned to overload shields.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1300,
                ShopBuyValueAstronsOverride = 5500,
                IconResourcePath = "double_ionizer_resource",
                ProjectFileName = "double_ionizer.png",
                SalvageOutputs = new[] { NeutralFighterSalvageId, AsteroidGoldId }
            },
            [AstroCutterId] = new InventoryItemDefinition
            {
                Id = AstroCutterId,
                DisplayName = "Astro Cutter",
                ShortLabel = "CUT",
                Description = "A medium-range continuous beam weapon tuned to carve through obstacles and meteor rock.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2900,
                IconResourcePath = "astro_cutter_resource",
                ProjectFileName = "astro_cutter.png",
                SalvageOutputs = new[] { RadarShipSalvageId, AsteroidRareId }
            },
            [FusionEngineId] = new InventoryItemDefinition
            {
                Id = FusionEngineId,
                DisplayName = "Fusion Engine",
                ShortLabel = "FUS",
                Description = "A high-output engine core that boosts speed, trail output, and engine recovery.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 900,
                IconResourcePath = "fusion_engine_icon_resource",
                ProjectFileName = "fusion_ engine.png",
                SalvageOutputs = new[] { AsteroidRareId }
            },
            [FuelTankId] = new InventoryItemDefinition
            {
                Id = FuelTankId,
                DisplayName = "Fuel Tank",
                ShortLabel = "FUE",
                Description = "An auxiliary engine-slot tank that increases maximum booster duration for the whole round.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1600,
                IconResourcePath = "fuel_tank_resource",
                ProjectFileName = "fuel_tank.png",
                SalvageOutputs = new[] { SpaceTruckWreckId, AsteroidResourceId }
            },
            [SuperBoosterId] = new InventoryItemDefinition
            {
                Id = SuperBoosterId,
                DisplayName = "Super Booster",
                ShortLabel = "SBO",
                Description = "An engine-slot burst system that launches the ship forward at extreme speed without consuming normal booster charge.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2300,
                IconResourcePath = "super_booster_resource",
                ProjectFileName = "super_booster.png",
                SalvageOutputs = new[] { SpaceTruckWreckId, AsteroidRareId, AsteroidGoldId }
            },
            [GadgetMineId] = new InventoryItemDefinition
            {
                Id = GadgetMineId,
                DisplayName = "Gadget Mine",
                ShortLabel = "GMI",
                Description = "A deployable proximity mine adapted for player gadget slots.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1200,
                IconResourcePath = "space_mine_resource",
                ProjectFileName = "space mine.png",
                SalvageOutputs = new[] { SpaceMineWreckId, SpaceMineWreckId }
            },
            [BatteryId] = new InventoryItemDefinition
            {
                Id = BatteryId,
                DisplayName = "Battery",
                ShortLabel = "BAT",
                Description = "A reusable shield charge pack that restores shields over time when triggered from a gadget slot.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 500,
                IconResourcePath = "battery_charge_resource",
                ProjectFileName = "battery_charge.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
            },
            [MagneticBeamId] = new InventoryItemDefinition
            {
                Id = MagneticBeamId,
                DisplayName = "Magnetic Beam",
                ShortLabel = "MAG",
                Description = "A ship-mounted magnetic projector that pulls nearby asteroids and resources toward the ship for a short burst.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 600,
                IconResourcePath = "magnetic_beam_resource",
                ProjectFileName = "magnetic_beam.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidGoldId }
            },
            [TractorBeamId] = new InventoryItemDefinition
            {
                Id = TractorBeamId,
                DisplayName = "Tractor Beam",
                ShortLabel = "TRC",
                Description = "A focused yellow tow beam that locks onto the nearest collectible object and pulls it behind the ship while held.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1200,
                IconResourcePath = "tractor_beam_resource",
                ProjectFileName = "tractor_beam.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidRareId }
            },
            [LureBeaconId] = new InventoryItemDefinition
            {
                Id = LureBeaconId,
                DisplayName = "Lure Beacon",
                ShortLabel = "LUR",
                Description = "A drifting decoy beacon that mimics a player target and pulls hostile attention away from the ship.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 700,
                IconResourcePath = "lure_beacon_gadget_resource",
                ProjectFileName = "lure_beacon_gadget.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidGoldId }
            },
            [AutoTurretId] = new InventoryItemDefinition
            {
                Id = AutoTurretId,
                DisplayName = "Auto Turret",
                ShortLabel = "TUR",
                Description = "A single-use deployable turret that anchors behind the ship and fires paired neutral-fighter shots at the nearest enemy.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1400,
                IconResourcePath = "auto_turret_top_down_resource",
                ProjectFileName = "auto_turret_top_down.png",
                SalvageOutputs = new[] { NeutralFighterSalvageId, SpaceMineWreckId }
            },
            [GuidanceSystemId] = new InventoryItemDefinition
            {
                Id = GuidanceSystemId,
                DisplayName = "Guidance System",
                ShortLabel = "GUI",
                Description = "A short-lived tactical navigator that points toward extraction, valuable loot, and the nearest hostile contact.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 3000,
                IconResourcePath = "guidance_system_resource",
                ProjectFileName = "guidance_system.png",
                SalvageOutputs = new[] { RadarShipSalvageId, SpaceJunkStandardId }
            },
            [LootingFriendId] = new InventoryItemDefinition
            {
                Id = LootingFriendId,
                DisplayName = "Looting Friend",
                ShortLabel = "LFR",
                Description = "A passive companion drone that follows the ship and automatically collects nearby loot while the pilot keeps flying.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2400,
                IconResourcePath = "looting_friend_top_down_resource",
                ProjectFileName = "looting_friend_top_down.png",
                SalvageOutputs = new[] { DroidScrapId, SpaceJunkStandardId, AsteroidRareId }
            },
            [SpaceDrillId] = new InventoryItemDefinition
            {
                Id = SpaceDrillId,
                DisplayName = "Space Drill",
                ShortLabel = "DRL",
                Description = "A fragile autonomous mining drone that extracts the nearest lootable asteroid and returns the loot to the ship.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1600,
                IconResourcePath = "space_drill_top_down_resource",
                ProjectFileName = "space_drill_top_down.png",
                SalvageOutputs = new[] { DroidScrapId, AsteroidRareId }
            },
            [SpaceTrapId] = new InventoryItemDefinition
            {
                Id = SpaceTrapId,
                DisplayName = "Space Trap",
                ShortLabel = "TRP",
                Description = "A single-use trap kit that arms a nearby loot object with a boosted mine explosion when looted.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1300,
                IconResourcePath = "space_trap_top_down_resource",
                ProjectFileName = "space_trap_top_down.png",
                SalvageOutputs = new[] { SpaceMineWreckId, AsteroidGoldId }
            },
            [ShieldReactorId] = new InventoryItemDefinition
            {
                Id = ShieldReactorId,
                DisplayName = "Shield Reactor",
                ShortLabel = "SHR",
                Description = "A defensive reactor that increases maximum shield capacity when installed in a shield slot.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1400,
                IconResourcePath = "shield_reactor_resource",
                ProjectFileName = "shield_reactor.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidGoldId }
            },
            [AlienTransmitterId] = new InventoryItemDefinition
            {
                Id = AlienTransmitterId,
                DisplayName = "Alien Transmitter",
                ShortLabel = "ALT",
                Description = "A unique alien signal device recovered from Space Mayhem. It is a quest item for future projects and special events.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "alien_transmitter_resource",
                ProjectFileName = "alien_transmitter.png",
                SalvageOutputs = Array.Empty<string>()
            }
        };

        AddContainerDefinitions(definitions);
        return definitions;
    }

    static void AddContainerDefinitions(Dictionary<string, InventoryItemDefinition> definitions)
    {
        if (definitions == null)
            return;

        for (int i = 0; i < ContainerVariantCount; i++)
        {
            string itemId = GetContainerItemId(i);
            definitions[itemId] = new InventoryItemDefinition
            {
                Id = itemId,
                DisplayName = "Container",
                ShortLabel = "CNT",
                Description = "A small drifting cargo container recovered from deep space.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Common,
                SellValueAstrons = 100,
                IconResourcePath = "kontenery_9",
                IconSpriteName = "kontenery_9_" + i,
                ProjectFileName = "Resources/kontenery_9.png",
                SalvageOutputs = new[] { AsteroidUncommonId }
            };
        }
    }

    static InventoryItemDefinition CreateSpaceJunkDefinition(
        string id,
        string displayName,
        string shortLabel,
        string description,
        string iconResourcePath,
        string projectFileName)
    {
        return new InventoryItemDefinition
        {
            Id = id,
            DisplayName = displayName,
            ShortLabel = shortLabel,
            Description = description,
            ItemType = InventoryItemType.Resource,
            Category = InventoryItemCategory.SpaceJunk,
            Rarity = InventoryItemRarity.Rare,
            SellValueAstrons = 500,
            IconResourcePath = iconResourcePath,
            ProjectFileName = projectFileName,
            SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
        };
    }
}
