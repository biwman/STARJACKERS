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
    Wreck,
    Weapon,
    Shield,
    Engine,
    Gadget,
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
    public string ProjectFileName;
    public string[] SalvageOutputs;

    Sprite cachedIcon;

    public Sprite GetIcon()
    {
        if (cachedIcon != null)
            return cachedIcon;

        if (!string.IsNullOrWhiteSpace(IconResourcePath))
        {
            cachedIcon = Resources.Load<Sprite>(IconResourcePath);
            if (cachedIcon != null)
                return cachedIcon;

            Sprite[] sprites = Resources.LoadAll<Sprite>(IconResourcePath);
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
        cachedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (cachedIcon != null)
            return cachedIcon;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
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
    public const string SpaceJunkId = "space_junk";
    public const string AsteroidLegendaryId = "asteroid_legendary_resource";
    public const string AsteroidCommonId = AsteroidResourceId;
    public const string AsteroidUncommonId = AsteroidGoldId;
    public const string AsteroidVeryRareId = RichAsteroidId;
    public const string AsteroidEpicId = SpaceJunkId;
    public const string DroidScrapId = "droid_scrap";
    public const string CorsairSalvageId = "corsair_salvage";
    public const string SpaceMineWreckId = "space_mine_wreck";
    public const string SpaceTruckWreckId = "space_truck_wreck";
    public const string MothershipCoreId = "mothership_core";
    public const string NeutralFighterSalvageId = "neutral_fighter_salvage";
    public const string PlasmaGunId = "plasma_gun";
    public const string FusionEngineId = "fusion_engine";
    public const string GadgetMineId = "gadget_mine";
    public const string BatteryId = "battery";
    public const string MagneticBeamId = "magnetic_beam";
    public const string TractorBeamId = "tractor_beam";
    public const string ShieldReactorId = "shield_reactor";

    static readonly Dictionary<string, InventoryItemDefinition> Definitions = BuildDefinitions();

    public static InventoryItemDefinition GetDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        Definitions.TryGetValue(itemId, out InventoryItemDefinition definition);
        return definition;
    }

    public static Sprite GetIcon(string itemId) => GetDefinition(itemId)?.GetIcon();

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
            InventoryItemCategory.Wreck => "Wreck",
            InventoryItemCategory.Weapon => "Weapon",
            InventoryItemCategory.Shield => "Shield",
            InventoryItemCategory.Engine => "Engine",
            InventoryItemCategory.Gadget => "Gadget",
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
        return new Dictionary<string, InventoryItemDefinition>(StringComparer.Ordinal)
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
                IconResourcePath = "treasure_asteroid_violet_rare_resource",
                ProjectFileName = "treasure_asteroid_violet_rare.png",
                SalvageOutputs = new[] { AsteroidRareId, AsteroidRareId }
            },
            [SpaceJunkId] = new InventoryItemDefinition
            {
                Id = SpaceJunkId,
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
                SalvageOutputs = new[] { SpaceJunkId, SpaceJunkId }
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
            }
        };
    }
}
