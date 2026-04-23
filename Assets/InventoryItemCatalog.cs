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
    public InventoryItemRarity Rarity;
    public int SellValueAstrons;
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
    public const string DroidScrapId = "droid_scrap";
    public const string CorsairSalvageId = "corsair_salvage";
    public const string SpaceMineWreckId = "space_mine_wreck";
    public const string PlasmaGunId = "plasma_gun";
    public const string FusionEngineId = "fusion_engine";
    public const string GadgetMineId = "gadget_mine";
    public const string BatteryId = "battery";

    static readonly Dictionary<string, InventoryItemDefinition> Definitions = BuildDefinitions();

    public static InventoryItemDefinition GetDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        Definitions.TryGetValue(itemId, out InventoryItemDefinition definition);
        return definition;
    }

    public static Sprite GetIcon(string itemId) => GetDefinition(itemId)?.GetIcon();

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
            case InventoryItemRarity.Uncommon: return new Color(0.2f, 0.62f, 0.24f, 0.98f);
            case InventoryItemRarity.Rare: return new Color(0.18f, 0.42f, 0.92f, 0.98f);
            case InventoryItemRarity.VeryRare: return new Color(0.48f, 0.22f, 0.78f, 0.98f);
            case InventoryItemRarity.Epic: return new Color(0.46f, 0.08f, 0.14f, 0.98f);
            case InventoryItemRarity.Legendary: return new Color(0.83f, 0.63f, 0.12f, 0.98f);
            default: return new Color(0.95f, 0.95f, 0.95f, 0.98f);
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
                ShortLabel = "AST",
                Description = "A common asteroid fragment.",
                ItemType = InventoryItemType.Resource,
                Rarity = InventoryItemRarity.Common,
                SellValueAstrons = 10,
                IconResourcePath = "Visuals/Treasures/asteroid_treasure_resource",
                ProjectFileName = "asteroida_treasure.png",
                SalvageOutputs = new[] { AsteroidResourceId }
            },
            [AsteroidGoldId] = new InventoryItemDefinition
            {
                Id = AsteroidGoldId,
                DisplayName = "Golden Asteroid",
                ShortLabel = "GLD",
                Description = "A richer asteroid vein with a higher resale value.",
                ItemType = InventoryItemType.Resource,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 30,
                IconResourcePath = "asteroida_zloto_clean_resource",
                ProjectFileName = "asteroida_zloto_clean.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
            },
            [AsteroidRareId] = new InventoryItemDefinition
            {
                Id = AsteroidRareId,
                DisplayName = "Rare Asteroid",
                ShortLabel = "RAR",
                Description = "A rare asteroid sample shimmering with unusual energy.",
                ItemType = InventoryItemType.Resource,
                Rarity = InventoryItemRarity.VeryRare,
                SellValueAstrons = 60,
                IconResourcePath = "asteroida_rare_clean_resource",
                ProjectFileName = "asteroida_rare_clean.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidGoldId }
            },
            [DroidScrapId] = new InventoryItemDefinition
            {
                Id = DroidScrapId,
                DisplayName = "Droid Wreck",
                ShortLabel = "BOT",
                Description = "A recoverable drone wreck fragment.",
                ItemType = InventoryItemType.Resource,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 40,
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
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 150,
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
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 55,
                IconResourcePath = "wrak_miny_resource",
                ProjectFileName = "wrak_miny.png",
                SalvageOutputs = new[] { AsteroidGoldId }
            },
            [PlasmaGunId] = new InventoryItemDefinition
            {
                Id = PlasmaGunId,
                DisplayName = "Plasma Gun",
                ShortLabel = "PLS",
                Description = "A salvaged Corsair plasma cannon adapted for player ships.",
                ItemType = InventoryItemType.Equipment,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 260,
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
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 200,
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
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 200,
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
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 100,
                IconResourcePath = "battery_charge_resource",
                ProjectFileName = "battery_charge.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
            }
        };
    }
}
