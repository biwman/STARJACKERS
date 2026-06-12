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
    Blueprint,
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
    Support,
    Rescue,
    Blueprint,
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
    public bool RequiresSafePocket = false;
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
    const int DefaultShopBuyValueMultiplier = 3;

    public const int RadioactiveTreasureShieldDamagePerSecond = 1;
    public const int RadioactiveTreasureHpDamagePerSecond = 1;
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
    public const string BlueprintScrapContainerIdPrefix = "blueprint_scrap_container_";
    public const int BlueprintScrapContainerVariantCount = 9;
    public const string RandomLootWreckIdPrefix = "random_loot_wreck_";
    public const int RandomLootWreckVariantCount = 9;
    public const string BlueprintIdPrefix = "blueprint_";
    public const int BlueprintSellValueAstrons = 4000;
    public const string BlueprintScrapId = "blueprint_scrap";
    public const int BlueprintScrapSellValueAstrons = 200;
    public const string CashSuitcaseId = "cash_suitcase";
    public const string PirateCaseId = "pirate_case";
    public const string PlatinumChunkId = "platinum_chunk";
    public const string RadioactiveTreasureId = "radioactive_treasure";
    public const string DroidScrapId = "droid_scrap";
    public const string CorsairSalvageId = "corsair_salvage";
    public const string SpaceMineWreckId = "space_mine_wreck";
    public const string SpaceTruckWreckId = "space_truck_wreck";
    public const string ContainerShipWreckId = "container_ship_wreck";
    public const string MothershipCoreId = "mothership_core";
    public const string VoidMawCoreId = "void_maw_core";
    public const string NeutralFighterSalvageId = "neutral_fighter_salvage";
    public const string RadarShipSalvageId = "radar_ship_salvage";
    public const string HunterLanceCoreId = "hunter_lance_core";
    public const string RescueShipSalvageId = "rescue_ship_salvage";
    public const string PirateFighterSalvageId = "pirate_fighter_salvage";
    public const string PirateBaseCoreId = "pirate_base_core";
    public const string SpaceAnimalRemainsId = "space_animal_remains";
    public const string PlasmaGunId = "plasma_gun";
    public const string TripleGunId = "triple_gun";
    public const string GatlingGunId = "gatling_gun";
    public const string ArtilleryGunId = "artillery_gun";
    public const string RocketLauncherId = "rocket_launcher";
    public const string DoubleRocketLauncherId = "double_rocket_launcher";
    public const string RailGunId = "rail_gun";
    public const string DoubleIonizerId = "double_ionizer";
    public const string AstroCutterId = "astro_cutter";
    public const string PulseDisruptorId = "pulse_disruptor";
    public const string PowerEngineId = "power_engine";
    public const string IonEngineId = "ion_engine";
    public const string FusionEngineId = "fusion_engine";
    public const string HybridEngineId = "hybrid_engine";
    public const string DoubleEngineId = "double_engine";
    public const string FuelTankId = "fuel_tank";
    public const string SuperBoosterId = "super_booster";
    public const string AfterburnerStabilizerId = "afterburner_stabilizer";
    public const string GadgetMineId = "gadget_mine";
    public const string SpaceBombId = "space_bomb";
    public const string DropbotId = "dropbot";
    public const string BatteryId = "battery";
    public const string MagneticBeamId = "magnetic_beam";
    public const string TractorBeamId = "tractor_beam";
    public const string LureBeaconId = "lure_beacon";
    public const string AutoTurretId = "auto_turret";
    public const string RocketAutoTurretId = "rocket_auto_turret";
    public const string GuidanceSystemId = "guidance_system";
    public const string TreasureScannerId = "treasure_scanner";
    public const string ShortScannerId = "short_scanner";
    public const string CloakDeviceId = "cloak_device";
    public const string LootingFriendId = "looting_friend";
    public const string FiringFriendId = "firing_friend";
    public const string SpaceDrillId = "space_drill";
    public const string SpaceTrapId = "space_trap";
    public const string EmergencySuitBeaconId = "emergency_suit_beacon";
    public const string EscapePodId = "escape_pod";
    public const string SalvageMagnetArrayId = "salvage_magnet_array";
    public const string ShieldReactorId = "shield_reactor";
    public const string KineticDampenerId = "kinetic_dampener";
    public const string PhaseShieldId = "phase_shield";
    public const string CargoBayExtensionId = "cargo_bay_extension";
    public const string StrongPlatingId = "strong_plating";
    public const string ShieldCapacitorId = "shield_capacitor";
    public const string AegisBatteryId = "aegis_battery";
    public const string RegenerativeShieldMatrixId = "regenerative_shield_matrix";
    public const string BulwarkProjectorId = "bulwark_projector";
    public const string AlienAegisCoreId = "alien_aegis_core";
    public const string AlienTransmitterId = "alien_transmitter";
    public const string PirateSymbolId = "pirate_symbol";
    public const string AvengerStartingCodesId = "avenger_starting_codes";
    public const string ArrowRaceTokenMinefieldId = "arrow_race_token_minefield";
    public const string ArrowRaceTokenSnowFieldId = "arrow_race_token_snowfield";
    public const string ArrowRaceTokenDeepSpaceId = "arrow_race_token_deep_space";
    public const string ArrowRaceTokenPirateBayId = "arrow_race_token_pirate_bay";

    static readonly Dictionary<string, InventoryItemDefinition> Definitions = BuildDefinitions();
    static bool iconsPrewarmed;

    public static InventoryItemDefinition GetDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        Definitions.TryGetValue(itemId, out InventoryItemDefinition definition);
        return definition;
    }

    public static Sprite GetIcon(string itemId) => GetDefinition(itemId)?.GetIcon();

    public static void PrewarmIcons()
    {
        if (iconsPrewarmed)
            return;

        iconsPrewarmed = true;
        foreach (InventoryItemDefinition definition in Definitions.Values)
            definition?.GetIcon();
    }

    public static string GetBlueprintItemId(string equipmentItemId)
    {
        if (string.IsNullOrWhiteSpace(equipmentItemId))
            return string.Empty;

        return BlueprintIdPrefix + equipmentItemId;
    }

    public static bool IsBlueprintItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(GetBlueprintTargetItemId(itemId));
    }

    public static string GetBlueprintTargetItemId(string blueprintItemId)
    {
        if (string.IsNullOrWhiteSpace(blueprintItemId) ||
            !blueprintItemId.StartsWith(BlueprintIdPrefix, StringComparison.Ordinal) ||
            blueprintItemId.Length <= BlueprintIdPrefix.Length)
        {
            return string.Empty;
        }

        string targetItemId = blueprintItemId.Substring(BlueprintIdPrefix.Length);
        InventoryItemDefinition target = GetDefinition(targetItemId);
        return target != null && target.ItemType == InventoryItemType.Equipment ? targetItemId : string.Empty;
    }

    public static string[] GetAllBlueprintItemIds()
    {
        List<string> itemIds = new List<string>();
        foreach (InventoryItemDefinition definition in Definitions.Values)
        {
            if (definition == null ||
                definition.ItemType != InventoryItemType.Blueprint ||
                string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            itemIds.Add(definition.Id);
        }

        itemIds.Sort(StringComparer.Ordinal);
        return itemIds.ToArray();
    }

    public static bool CanEnterSafePocket(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition == null || definition.CanEnterSafePocket;
    }

    public static bool RequiresSafePocket(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null && definition.RequiresSafePocket;
    }

    public static bool IsArrowRaceToken(string itemId)
    {
        return string.Equals(itemId, ArrowRaceTokenMinefieldId, StringComparison.Ordinal) ||
               string.Equals(itemId, ArrowRaceTokenSnowFieldId, StringComparison.Ordinal) ||
               string.Equals(itemId, ArrowRaceTokenDeepSpaceId, StringComparison.Ordinal) ||
               string.Equals(itemId, ArrowRaceTokenPirateBayId, StringComparison.Ordinal);
    }

    public static bool TryGetArrowRaceTokenForMap(string mapId, out string itemId)
    {
        switch (mapId)
        {
            case LobbyMapCatalog.MinefieldMapId:
                itemId = ArrowRaceTokenMinefieldId;
                return true;
            case LobbyMapCatalog.SnowFieldMapId:
                itemId = ArrowRaceTokenSnowFieldId;
                return true;
            case LobbyMapCatalog.DeepSpaceMapId:
                itemId = ArrowRaceTokenDeepSpaceId;
                return true;
            case LobbyMapCatalog.PirateBayMapId:
                itemId = ArrowRaceTokenPirateBayId;
                return true;
            default:
                itemId = string.Empty;
                return false;
        }
    }

    public static bool TryGetArrowRaceTokenMapId(string itemId, out string mapId)
    {
        switch (itemId)
        {
            case ArrowRaceTokenMinefieldId:
                mapId = LobbyMapCatalog.MinefieldMapId;
                return true;
            case ArrowRaceTokenSnowFieldId:
                mapId = LobbyMapCatalog.SnowFieldMapId;
                return true;
            case ArrowRaceTokenDeepSpaceId:
                mapId = LobbyMapCatalog.DeepSpaceMapId;
                return true;
            case ArrowRaceTokenPirateBayId:
                mapId = LobbyMapCatalog.PirateBayMapId;
                return true;
            default:
                mapId = string.Empty;
                return false;
        }
    }

    public static string[] GetArrowRaceTokenIds()
    {
        return new[]
        {
            ArrowRaceTokenMinefieldId,
            ArrowRaceTokenSnowFieldId,
            ArrowRaceTokenDeepSpaceId,
            ArrowRaceTokenPirateBayId
        };
    }

    public static bool IsTrackedValuableCargo(string itemId)
    {
        return string.Equals(itemId, CashSuitcaseId, StringComparison.Ordinal) ||
               string.Equals(itemId, PirateCaseId, StringComparison.Ordinal);
    }

    public static bool IsRadioactiveTreasure(string itemId)
    {
        return string.Equals(itemId, RadioactiveTreasureId, StringComparison.Ordinal);
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

    public static string GetBlueprintScrapContainerItemId(int variantIndex)
    {
        int clampedIndex = Mathf.Clamp(variantIndex, 0, BlueprintScrapContainerVariantCount - 1);
        return BlueprintScrapContainerIdPrefix + (clampedIndex + 1).ToString("00");
    }

    public static bool IsBlueprintScrapContainerItem(string itemId)
    {
        return GetBlueprintScrapContainerVariantIndex(itemId) >= 0;
    }

    public static int GetBlueprintScrapContainerVariantIndex(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            !itemId.StartsWith(BlueprintScrapContainerIdPrefix, StringComparison.Ordinal) ||
            itemId.Length <= BlueprintScrapContainerIdPrefix.Length)
        {
            return -1;
        }

        string suffix = itemId.Substring(BlueprintScrapContainerIdPrefix.Length);
        if (!int.TryParse(suffix, out int oneBasedIndex))
            return -1;

        int zeroBasedIndex = oneBasedIndex - 1;
        return zeroBasedIndex >= 0 && zeroBasedIndex < BlueprintScrapContainerVariantCount ? zeroBasedIndex : -1;
    }

    public static string[] GetBlueprintScrapContainerItemIds()
    {
        string[] itemIds = new string[BlueprintScrapContainerVariantCount];
        for (int i = 0; i < itemIds.Length; i++)
            itemIds[i] = GetBlueprintScrapContainerItemId(i);

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
        string description = definition != null ? definition.Description : string.Empty;
        string classification = WeaponAttackCatalog.BuildEquipmentClassificationSummary(itemId);
        if (string.IsNullOrWhiteSpace(classification))
            return description;

        if (string.IsNullOrWhiteSpace(description))
            return classification;

        return description + "\n\n" + classification;
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
            InventoryItemCategory.Support => "Support",
            InventoryItemCategory.Rescue => "Rescue",
            InventoryItemCategory.Blueprint => "Blueprint",
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
        if (equipmentSlotIndex == 8 || equipmentSlotIndex == 9)
            return InventoryItemCategory.Support;
        if (equipmentSlotIndex == 10 || equipmentSlotIndex == 11)
            return InventoryItemCategory.Rescue;

        return InventoryItemCategory.Misc;
    }

    public static int CountEquippedItem(string[] equipmentSlots, int shipSkinIndex, string itemId)
    {
        if (equipmentSlots == null || string.IsNullOrWhiteSpace(itemId))
            return 0;

        int count = 0;
        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            if (string.Equals(equipmentSlots[i], itemId, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    public static bool HasEquippedItem(string[] equipmentSlots, int shipSkinIndex, string itemId)
    {
        return CountEquippedItem(equipmentSlots, shipSkinIndex, itemId) > 0;
    }

    public static int GetShieldCapacityBonus(string itemId)
    {
        switch (itemId)
        {
            case ShieldReactorId:
                return 40;
            case KineticDampenerId:
                return 30;
            case PhaseShieldId:
                return 30;
            case CargoBayExtensionId:
                return 20;
            case ShieldCapacitorId:
                return 60;
            case AegisBatteryId:
                return 50;
            case RegenerativeShieldMatrixId:
                return 60;
            case BulwarkProjectorId:
                return 75;
            case AlienAegisCoreId:
                return 110;
            default:
                return 0;
        }
    }

    public static int GetHpCapacityBonus(string itemId)
    {
        switch (itemId)
        {
            case StrongPlatingId:
                return 40;
            default:
                return 0;
        }
    }

    public static int GetEquippedHpCapacityBonus(string[] equipmentSlots, int shipSkinIndex)
    {
        if (equipmentSlots == null)
            return 0;

        int bonus = 0;
        for (int i = 2; i <= 3; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            string itemId = i >= 0 && i < equipmentSlots.Length ? equipmentSlots[i] : null;
            bonus += GetHpCapacityBonus(itemId);
        }

        return bonus;
    }

    public static int GetEquippedShieldCapacityBonus(string[] equipmentSlots, int shipSkinIndex)
    {
        if (equipmentSlots == null)
            return 0;

        int bonus = 0;
        for (int i = 2; i <= 3; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            string itemId = i >= 0 && i < equipmentSlots.Length ? equipmentSlots[i] : null;
            bonus += GetShieldCapacityBonus(itemId);
        }

        return bonus;
    }

    public static int GetShieldSpeedPenaltyPercent(string itemId)
    {
        switch (itemId)
        {
            case StrongPlatingId:
                return 5;
            case RegenerativeShieldMatrixId:
                return 10;
            case BulwarkProjectorId:
                return 10;
            case AlienAegisCoreId:
                return 15;
            default:
                return 0;
        }
    }

    public static int GetEquippedShieldSpeedPenaltyPercent(string[] equipmentSlots, int shipSkinIndex)
    {
        if (equipmentSlots == null)
            return 0;

        int penalty = 0;
        for (int i = 2; i <= 3; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            string itemId = i >= 0 && i < equipmentSlots.Length ? equipmentSlots[i] : null;
            penalty += GetShieldSpeedPenaltyPercent(itemId);
        }

        return Mathf.Clamp(penalty, 0, 75);
    }

    public static InventoryItemRarity GetRarity(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null ? definition.Rarity : InventoryItemRarity.Common;
    }

    public static bool IsAsteroidResource(string itemId)
    {
        return string.Equals(itemId, AsteroidResourceId, StringComparison.Ordinal) ||
               string.Equals(itemId, AsteroidGoldId, StringComparison.Ordinal) ||
               string.Equals(itemId, AsteroidRareId, StringComparison.Ordinal) ||
               string.Equals(itemId, RichAsteroidId, StringComparison.Ordinal) ||
               string.Equals(itemId, AsteroidEpicId, StringComparison.Ordinal) ||
               string.Equals(itemId, AsteroidLegendaryId, StringComparison.Ordinal);
    }

    public static bool TryGetNextAsteroidRarityId(string itemId, out string upgradedItemId)
    {
        upgradedItemId = null;
        if (string.Equals(itemId, AsteroidResourceId, StringComparison.Ordinal))
            upgradedItemId = AsteroidGoldId;
        else if (string.Equals(itemId, AsteroidGoldId, StringComparison.Ordinal))
            upgradedItemId = AsteroidRareId;
        else if (string.Equals(itemId, AsteroidRareId, StringComparison.Ordinal))
            upgradedItemId = RichAsteroidId;
        else if (string.Equals(itemId, RichAsteroidId, StringComparison.Ordinal))
            upgradedItemId = AsteroidEpicId;
        else if (string.Equals(itemId, AsteroidEpicId, StringComparison.Ordinal))
            upgradedItemId = AsteroidLegendaryId;

        return !string.IsNullOrWhiteSpace(upgradedItemId);
    }

    public static int GetSellValueAstrons(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        return definition != null ? Mathf.Max(0, definition.SellValueAstrons) : 0;
    }

    public static int GetShopBuyValueAstrons(string itemId)
    {
        InventoryItemDefinition definition = GetDefinition(itemId);
        if (definition == null)
            return 0;

        if (definition.ShopBuyValueAstronsOverride > 0)
            return definition.ShopBuyValueAstronsOverride;

        if (definition.ItemType != InventoryItemType.Equipment)
            return 0;

        int craftingCost = PlayerProfileCraftingCatalog.GetCraftingInputSellValueForOutput(itemId);
        if (craftingCost > 0)
            return craftingCost * DefaultShopBuyValueMultiplier;

        return Mathf.Max(0, definition.SellValueAstrons * DefaultShopBuyValueMultiplier);
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

    public static string[] RollSalvageOutputs(string itemId)
    {
        if (IsRadioactiveTreasure(itemId))
            return new[] { RollRadioactiveTreasureSalvageOutput() };

        return GetSalvageOutputs(itemId);
    }

    public static bool HasRandomSalvageOutputs(string itemId)
    {
        return IsRadioactiveTreasure(itemId);
    }

    public static string GetRandomSalvageDescription(string itemId)
    {
        if (!IsRadioactiveTreasure(itemId))
            return string.Empty;

        return "50% " + GetDisplayName(RichAsteroidId) + "\n" +
               "30% " + GetDisplayName(AsteroidEpicId) + "\n" +
               "20% " + GetDisplayName(AsteroidLegendaryId);
    }

    static string RollRadioactiveTreasureSalvageOutput()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.5f)
            return RichAsteroidId;

        if (roll < 0.8f)
            return AsteroidEpicId;

        return AsteroidLegendaryId;
    }

    public static Color GetRarityColor(string itemId)
    {
        return GetRarityColor(GetRarity(itemId));
    }

    public static Color GetRarityColor(InventoryItemRarity rarity)
    {
        switch (rarity)
        {
            case InventoryItemRarity.Common: return new Color(0.58f, 0.61f, 0.65f, 0.98f);
            case InventoryItemRarity.Uncommon: return new Color(0.18f, 0.68f, 0.32f, 0.98f);
            case InventoryItemRarity.Rare: return new Color(0.16f, 0.42f, 0.94f, 0.98f);
            case InventoryItemRarity.VeryRare: return new Color(0.48f, 0.2f, 0.8f, 0.98f);
            case InventoryItemRarity.Epic: return new Color(0.5f, 0.05f, 0.16f, 0.98f);
            case InventoryItemRarity.Legendary: return new Color(0.92f, 0.68f, 0.08f, 0.98f);
            default: return new Color(0.58f, 0.61f, 0.65f, 0.98f);
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
                "space_junk_trash.png",
                350),
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
                "space_junk_asteroid.png",
                650),
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
            [PirateCaseId] = new InventoryItemDefinition
            {
                Id = PirateCaseId,
                DisplayName = "Pirate Case",
                ShortLabel = "PIR",
                Description = "A locked pirate payoff case loaded with stolen Astrons. Carrying it makes you very interesting.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 6000,
                IconResourcePath = "Items/pirate_case",
                ProjectFileName = "Resources/Items/pirate_case.png",
                CanEnterSafePocket = false,
                SalvageOutputs = new[] { AsteroidGoldId }
            },
            [PlatinumChunkId] = new InventoryItemDefinition
            {
                Id = PlatinumChunkId,
                DisplayName = "Platinum Chunk",
                ShortLabel = "PLT",
                Description = "A dense platinum-rich core hidden inside a fractured obstacle.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 9000,
                IconResourcePath = "Items/platinum_chunk",
                ProjectFileName = "Resources/Items/platinum_chunk.png",
                CanEnterSafePocket = false,
                SalvageOutputs = new[] { AsteroidLegendaryId, AsteroidEpicId, RichAsteroidId }
            },
            [RadioactiveTreasureId] = new InventoryItemDefinition
            {
                Id = RadioactiveTreasureId,
                DisplayName = "Radioactive Treasure",
                ShortLabel = "RAD",
                Description = "An unstable irradiated treasure. Each one carried in cargo deals 1 shield damage per second, then 1 HP damage per second after shields fail. Salvage yields a random treasure: Very Rare 50%, Epic 30%, Legendary 20%.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Treasure,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 250,
                IconResourcePath = "radioactive_treasure",
                ProjectFileName = "Resources/radioactive_treasure.png",
                CanEnterSafePocket = false,
                SalvageOutputs = new[] { RichAsteroidId, AsteroidEpicId, AsteroidLegendaryId }
            },
            [BlueprintScrapId] = new InventoryItemDefinition
            {
                Id = BlueprintScrapId,
                DisplayName = "Blueprint Scrap",
                ShortLabel = "BSC",
                Description = "Recovered fragments of technical schematics. Miss Enigma accepts them in trade for selected blueprints.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Blueprint,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = BlueprintScrapSellValueAstrons,
                IconResourcePath = "Items/blueprint_scrap",
                IconSpriteName = "blueprint_scrap_0",
                ProjectFileName = "Resources/Items/blueprint_scrap.png",
                SalvageOutputs = Array.Empty<string>()
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
                SellValueAstrons = 1100,
                IconResourcePath = "space_truck_wrak_resource",
                ProjectFileName = "space_truck_wrak.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidGoldId }
            },
            [ContainerShipWreckId] = new InventoryItemDefinition
            {
                Id = ContainerShipWreckId,
                DisplayName = "Container Hauler Wreck",
                ShortLabel = "HUL",
                Description = "Heavy salvage recovered from a destroyed container hauler.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1050,
                IconResourcePath = "Enemies/ContainerShip/container_ship_wreck",
                ProjectFileName = "Resources/Enemies/ContainerShip/container_ship_wreck.png",
                SalvageOutputs = new[] { AsteroidGoldId, SpaceJunkStandardId }
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
                SellValueAstrons = 5500,
                IconResourcePath = "mother_ship_wrak_resource",
                ProjectFileName = "mother_ship_wrak.png",
                SalvageOutputs = new[] { AsteroidRareId, AsteroidRareId }
            },
            [VoidMawCoreId] = new InventoryItemDefinition
            {
                Id = VoidMawCoreId,
                DisplayName = "Void Maw Core",
                ShortLabel = "MAW",
                Description = "A living stellar organ from the Cosmic Worm. Miss Enigma will want to see it.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 12000,
                IconResourcePath = "Enemies/CosmicWorm/cosmic_worm_resource",
                ProjectFileName = "Resources/Enemies/CosmicWorm/cosmic_worm_resource.png",
                CanEnterSafePocket = false,
                SalvageOutputs = Array.Empty<string>()
            },
            [NeutralFighterSalvageId] = new InventoryItemDefinition
            {
                Id = NeutralFighterSalvageId,
                DisplayName = "Neutral Fighter Salvage",
                ShortLabel = "NFS",
                Description = "Common salvage recovered from a destroyed Neutral Fighter.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Common,
                SellValueAstrons = 600,
                IconResourcePath = "neutral_fighter_wreck_resource",
                ProjectFileName = "neutral_fighter_wreck.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
            },
            [RadarShipSalvageId] = new InventoryItemDefinition
            {
                Id = RadarShipSalvageId,
                DisplayName = "Radar Ship Salvage",
                ShortLabel = "RDS",
                Description = "Very rare heavy salvage recovered from a destroyed Radar Ship spotter platform.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.VeryRare,
                SellValueAstrons = 1800,
                IconResourcePath = "radar_ship_wreck_resource",
                ProjectFileName = "radar_ship_wreck.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidRareId }
            },
            [HunterLanceCoreId] = new InventoryItemDefinition
            {
                Id = HunterLanceCoreId,
                DisplayName = "Hunter Lance Core",
                ShortLabel = "HLC",
                Description = "A cracked rail-lance focusing core recovered from a destroyed Hunter Lance.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1700,
                IconResourcePath = "Enemies/HunterLance/hunter_lance_core_resource",
                ProjectFileName = "Resources/Enemies/HunterLance/hunter_lance_core_resource.png",
                SalvageOutputs = new[] { AsteroidRareId, SpaceJunkStandardId }
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
                SellValueAstrons = 1100,
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
                SellValueAstrons = 7000,
                IconResourcePath = "pirate_base_wreck_resource",
                ProjectFileName = "pirate_base_wreck.png",
                SalvageOutputs = new[] { AsteroidEpicId, AsteroidRareId }
            },
            [SpaceAnimalRemainsId] = new InventoryItemDefinition
            {
                Id = SpaceAnimalRemainsId,
                DisplayName = "Space Animal Remains",
                ShortLabel = "SAR",
                Description = "Organic remains recovered from a defeated cosmic creature.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Wreck,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 900,
                IconResourcePath = "space_animal_remains_resource",
                ProjectFileName = "Resources/space_animal_remains_resource.png",
                SalvageOutputs = new[] { AsteroidRareId, AsteroidGoldId }
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
                ShopBuyValueAstronsOverride = 6000,
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
                SellValueAstrons = 200,
                ShopBuyValueAstronsOverride = 700,
                IconResourcePath = "triple_gun_resource",
                ProjectFileName = "triple_gun.png",
                SalvageOutputs = new[] { AsteroidResourceId }
            },
            [GatlingGunId] = new InventoryItemDefinition
            {
                Id = GatlingGunId,
                DisplayName = "Gatling Gun",
                ShortLabel = "GAT",
                Description = "A compact rotary weapon that pours out a short burst of tiny kinetic rounds.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1500,
                ShopBuyValueAstronsOverride = 5600,
                IconResourcePath = "gatling_gun",
                ProjectFileName = "Resources/gatling_gun.png",
                SalvageOutputs = new[] { NeutralFighterSalvageId, AsteroidRareId }
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
                SellValueAstrons = 800,
                ShopBuyValueAstronsOverride = 3000,
                IconResourcePath = "artillery_gun_resource",
                ProjectFileName = "artillery_gun.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidResourceId }
            },
            [RocketLauncherId] = new InventoryItemDefinition
            {
                Id = RocketLauncherId,
                DisplayName = "Rocket Launcher",
                ShortLabel = "RKT",
                Description = "A compact launcher that fires explosive rockets. Holding aim on a target locks the rocket for homing flight.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1700,
                ShopBuyValueAstronsOverride = 6200,
                IconResourcePath = "Items/rocket_launcher",
                ProjectFileName = "Resources/Items/rocket_launcher.png",
                SalvageOutputs = new[] { NeutralFighterSalvageId, AsteroidGoldId }
            },
            [DoubleRocketLauncherId] = new InventoryItemDefinition
            {
                Id = DoubleRocketLauncherId,
                DisplayName = "Double Rocket Launcher",
                ShortLabel = "DRK",
                Description = "A twin launcher that fires paired homing-capable rockets with a dangerous explosive blast.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2900,
                ShopBuyValueAstronsOverride = 10500,
                IconResourcePath = "Items/double_launcher",
                ProjectFileName = "Resources/Items/double_launcher.png",
                SalvageOutputs = new[] { RocketLauncherId, AsteroidRareId }
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
                ShopBuyValueAstronsOverride = 4800,
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
            [PulseDisruptorId] = new InventoryItemDefinition
            {
                Id = PulseDisruptorId,
                DisplayName = "Pulse Disruptor",
                ShortLabel = "PUL",
                Description = "A slow heavy pulse weapon that arms over a short distance and can emit a shield-breaking EMP wave.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Weapon,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2100,
                ShopBuyValueAstronsOverride = 7500,
                IconResourcePath = "Items/pulse_disruptor",
                ProjectFileName = "Resources/Items/pulse_disruptor.png",
                SalvageOutputs = new[] { DoubleIonizerId, AsteroidRareId }
            },
            [PowerEngineId] = new InventoryItemDefinition
            {
                Id = PowerEngineId,
                DisplayName = "Power Engine",
                ShortLabel = "PWR",
                Description = "A rugged engine upgrade that raises cruise speed and maximum boost output.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 900,
                ShopBuyValueAstronsOverride = 3400,
                IconResourcePath = "Items/power_engine",
                ProjectFileName = "Resources/Items/power_engine.png",
                SalvageOutputs = new[] { DroidScrapId, AsteroidGoldId }
            },
            [IonEngineId] = new InventoryItemDefinition
            {
                Id = IonEngineId,
                DisplayName = "Ion Engine",
                ShortLabel = "ION",
                Description = "A responsive ion drive that improves booster recovery, turning, and light cruise speed.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1200,
                ShopBuyValueAstronsOverride = 4800,
                IconResourcePath = "Items/ion_engine",
                ProjectFileName = "Resources/Items/ion_engine.png",
                SalvageOutputs = new[] { SpaceMineWreckId, AsteroidRareId }
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
                SellValueAstrons = 1700,
                ShopBuyValueAstronsOverride = 6200,
                IconResourcePath = "fusion_engine_icon_resource",
                ProjectFileName = "fusion_ engine.png",
                SalvageOutputs = new[] { PowerEngineId, AsteroidRareId }
            },
            [HybridEngineId] = new InventoryItemDefinition
            {
                Id = HybridEngineId,
                DisplayName = "Hybrid Engine",
                ShortLabel = "HYB",
                Description = "A balanced engine pack that improves speed, booster endurance, and recovery.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2100,
                ShopBuyValueAstronsOverride = 7800,
                IconResourcePath = "Items/hybrid_engine",
                ProjectFileName = "Resources/Items/hybrid_engine.png",
                SalvageOutputs = new[] { FuelTankId, AsteroidRareId }
            },
            [DoubleEngineId] = new InventoryItemDefinition
            {
                Id = DoubleEngineId,
                DisplayName = "Double Engine",
                ShortLabel = "DBL",
                Description = "A twin-drive engine built for extreme speed and boost output at the cost of endurance and handling.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 3000,
                ShopBuyValueAstronsOverride = 12000,
                IconResourcePath = "Items/double_engine",
                ProjectFileName = "Resources/Items/double_engine.png",
                SalvageOutputs = new[] { FusionEngineId, SpaceTruckWreckId, AsteroidRareId }
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
            [AfterburnerStabilizerId] = new InventoryItemDefinition
            {
                Id = AfterburnerStabilizerId,
                DisplayName = "Afterburner Stabilizer",
                ShortLabel = "AST",
                Description = "An engine-slot stabilizer that improves booster handling and turning, and halves the delay before booster recovery starts.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Engine,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1100,
                ShopBuyValueAstronsOverride = 4200,
                IconResourcePath = "Items/afterburner_stabilizer",
                ProjectFileName = "Resources/Items/afterburner_stabilizer.png",
                SalvageOutputs = new[] { DroidScrapId, AsteroidGoldId }
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
            [SpaceBombId] = new InventoryItemDefinition
            {
                Id = SpaceBombId,
                DisplayName = "Space Bomb",
                ShortLabel = "BOM",
                Description = "A single-use heavy bomb that drops behind the ship, arms its engine, then flies forward and detonates in a huge blast on impact.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2600,
                ShopBuyValueAstronsOverride = 9800,
                IconResourcePath = "Items/space_bomb_gadget",
                ProjectFileName = "Resources/Items/space_bomb_gadget.png",
                SalvageOutputs = new[] { SpaceMineWreckId, AsteroidRareId, SpaceJunkStandardId }
            },
            [DropbotId] = new InventoryItemDefinition
            {
                Id = DropbotId,
                DisplayName = "Dropbot",
                ShortLabel = "DRP",
                Description = "A single-use cargo evacuation drone. The first cargo slot after SAFE and ASTRO is marked DROP; activating Dropbot sends that item to extraction so it survives even if the ship is lost.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2800,
                ShopBuyValueAstronsOverride = 11200,
                IconResourcePath = "Items/dropbot",
                ProjectFileName = "Resources/Items/dropbot.png",
                SalvageOutputs = new[] { DroidScrapId, RescueShipSalvageId, AsteroidRareId }
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
                SellValueAstrons = 600,
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
            [RocketAutoTurretId] = new InventoryItemDefinition
            {
                Id = RocketAutoTurretId,
                DisplayName = "Rocket Auto Turret",
                ShortLabel = "RAT",
                Description = "A single-use deployable rocket turret that anchors behind the ship and fires straight explosive missiles at hostile ships.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 3100,
                ShopBuyValueAstronsOverride = 11800,
                IconResourcePath = "Items/rocket_autoturret",
                ProjectFileName = "Resources/Items/rocket_autoturret.png",
                SalvageOutputs = new[] { DroidScrapId, RocketLauncherId, AsteroidRareId }
            },
            [GuidanceSystemId] = new InventoryItemDefinition
            {
                Id = GuidanceSystemId,
                DisplayName = "Guidance System",
                ShortLabel = "GUI",
                Description = "A support-slot tactical navigator that points toward extraction, valuable loot, and the nearest hostile contact when activated.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Support,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 3000,
                IconResourcePath = "guidance_system_resource",
                ProjectFileName = "guidance_system.png",
                SalvageOutputs = new[] { RadarShipSalvageId, SpaceJunkStandardId }
            },
            [TreasureScannerId] = new InventoryItemDefinition
            {
                Id = TreasureScannerId,
                DisplayName = "Treasure Scanner",
                ShortLabel = "SCN",
                Description = "A passive support scanner that hunts Hidden Treasure with audio pings. The signal becomes faster, louder, and higher pitched as your ship gets closer to the hidden stash.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Support,
                Rarity = InventoryItemRarity.VeryRare,
                SellValueAstrons = 2200,
                ShopBuyValueAstronsOverride = 8700,
                IconResourcePath = "Items/treasure_scanner",
                IconSpriteName = "treasure_scanner_0",
                ProjectFileName = "Resources/Items/treasure_scanner.png",
                SalvageOutputs = new[] { RadarShipSalvageId, AsteroidGoldId }
            },
            [ShortScannerId] = new InventoryItemDefinition
            {
                Id = ShortScannerId,
                DisplayName = "Short Scanner",
                ShortLabel = "SSC",
                Description = "Activate to expose ships and enemy bots hiding inside nebulas, fire nebulas, and clouds for 15 seconds. Each equipped Short Scanner adds 3 scans per round.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Support,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2600,
                ShopBuyValueAstronsOverride = 9600,
                IconResourcePath = "Items/short_scanner",
                IconSpriteName = "short_scanner_0",
                ProjectFileName = "Resources/Items/short_scanner.png",
                SalvageOutputs = new[] { RadarShipSalvageId, DroidScrapId, AsteroidGoldId }
            },
            [CloakDeviceId] = new InventoryItemDefinition
            {
                Id = CloakDeviceId,
                DisplayName = "Cloak Device",
                ShortLabel = "CLK",
                Description = "A stealth field generator that hides the ship from players and enemies for a short time while preserving normal collisions.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 3000,
                ShopBuyValueAstronsOverride = 12400,
                IconResourcePath = "Items/cloak_device",
                IconSpriteName = "cloak_device_0",
                ProjectFileName = "Resources/Items/cloak_device.png",
                SalvageOutputs = new[] { RadarShipSalvageId, DroidScrapId, AsteroidGoldId }
            },
            [LootingFriendId] = new InventoryItemDefinition
            {
                Id = LootingFriendId,
                DisplayName = "Looting Friend",
                ShortLabel = "LFR",
                Description = "A support-slot companion drone that follows the ship and automatically collects nearby loot while the pilot keeps flying.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Support,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2400,
                IconResourcePath = "looting_friend_top_down_resource",
                ProjectFileName = "looting_friend_top_down.png",
                SalvageOutputs = new[] { DroidScrapId, SpaceJunkStandardId, AsteroidRareId }
            },
            [FiringFriendId] = new InventoryItemDefinition
            {
                Id = FiringFriendId,
                DisplayName = "Firing Friend",
                ShortLabel = "FFR",
                Description = "A support-slot combat drone that follows the ship and fires short-range laser bursts at the nearest enemy.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Support,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2600,
                ShopBuyValueAstronsOverride = 9600,
                IconResourcePath = "Items/firing_friend",
                ProjectFileName = "Resources/Items/firing_friend.png",
                SalvageOutputs = new[] { DroidScrapId, NeutralFighterSalvageId, AsteroidRareId }
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
            [EmergencySuitBeaconId] = new InventoryItemDefinition
            {
                Id = EmergencySuitBeaconId,
                DisplayName = "Emergency Suit Beacon",
                ShortLabel = "BEA",
                Description = "A rescue-slot beacon that gives a surviving astronaut brief protection and a permanent speed boost after ship loss.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Rescue,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 300,
                ShopBuyValueAstronsOverride = 900,
                IconResourcePath = "Items/emergency_suit_beacon",
                ProjectFileName = "Resources/Items/emergency_suit_beacon.png",
                SalvageOutputs = new[] { AsteroidResourceId }
            },
            [EscapePodId] = new InventoryItemDefinition
            {
                Id = EscapePodId,
                DisplayName = "Escape Pod",
                ShortLabel = "POD",
                Description = "A rescue-slot emergency capsule that replaces the astronaut after ship loss, with stronger hull integrity and faster escape thrust.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Rescue,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 3200,
                ShopBuyValueAstronsOverride = 13200,
                IconResourcePath = "Items/escape_pod",
                ProjectFileName = "Resources/Items/escape_pod.png",
                SalvageOutputs = new[] { RescueShipSalvageId, AsteroidRareId, SpaceJunkStandardId }
            },
            [SalvageMagnetArrayId] = new InventoryItemDefinition
            {
                Id = SalvageMagnetArrayId,
                DisplayName = "Salvage Magnet Array",
                ShortLabel = "SMA",
                Description = "A passive gadget array that doubles collection range for wreck loot and random salvage.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Gadget,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 2300,
                ShopBuyValueAstronsOverride = 8100,
                IconResourcePath = "Items/salvage_magnet_array",
                ProjectFileName = "Resources/Items/salvage_magnet_array.png",
                SalvageOutputs = new[] { TractorBeamId, AsteroidRareId }
            },
            [ShieldReactorId] = new InventoryItemDefinition
            {
                Id = ShieldReactorId,
                DisplayName = "Shield Reactor",
                ShortLabel = "SHR",
                Description = "A compact defensive reactor that adds 40 maximum shield capacity when installed in a shield slot.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 1000,
                ShopBuyValueAstronsOverride = 3600,
                IconResourcePath = "shield_reactor_resource",
                ProjectFileName = "shield_reactor.png",
                SalvageOutputs = new[] { AsteroidResourceId, AsteroidGoldId }
            },
            [KineticDampenerId] = new InventoryItemDefinition
            {
                Id = KineticDampenerId,
                DisplayName = "Kinetic Dampener",
                ShortLabel = "KIN",
                Description = "A shield-slot impact absorber that adds 30 maximum shield capacity, halves kinetic and contact damage, and reduces explosive shock damage.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 1000,
                ShopBuyValueAstronsOverride = 3600,
                IconResourcePath = "Items/kinetic_dampener",
                ProjectFileName = "Resources/Items/kinetic_dampener.png",
                SalvageOutputs = new[] { SpaceMineWreckId, AsteroidRareId }
            },
            [PhaseShieldId] = new InventoryItemDefinition
            {
                Id = PhaseShieldId,
                DisplayName = "Phase Shield",
                ShortLabel = "PHS",
                Description = "A shield-slot failsafe that adds 30 maximum shield capacity and grants 6 seconds of invulnerability when hull integrity drops low.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1800,
                ShopBuyValueAstronsOverride = 6500,
                IconResourcePath = "Items/phase_shield",
                ProjectFileName = "Resources/Items/phase_shield.png",
                SalvageOutputs = new[] { RadarShipSalvageId, AsteroidRareId }
            },
            [CargoBayExtensionId] = new InventoryItemDefinition
            {
                Id = CargoBayExtensionId,
                DisplayName = "Cargo Bay Extension",
                ShortLabel = "CEX",
                Description = "A shield-slot cargo module that adds 20 maximum shield capacity and two extra ship inventory slots.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 1200,
                ShopBuyValueAstronsOverride = 4200,
                IconResourcePath = "Items/cargo_bay_extension",
                ProjectFileName = "Resources/Items/cargo_bay_extension.png",
                SalvageOutputs = new[] { SpaceTruckWreckId, AsteroidGoldId }
            },
            [StrongPlatingId] = new InventoryItemDefinition
            {
                Id = StrongPlatingId,
                DisplayName = "Strong Plating",
                ShortLabel = "PLT",
                Description = "A shield-slot hull plating that adds 40 maximum HP and halves nebula and environmental damage, but slows the ship slightly.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1600,
                ShopBuyValueAstronsOverride = 5600,
                IconResourcePath = "Items/strong_plating",
                ProjectFileName = "Resources/Items/strong_plating.png",
                SalvageOutputs = new[] { RescueShipSalvageId, AsteroidRareId }
            },
            [ShieldCapacitorId] = new InventoryItemDefinition
            {
                Id = ShieldCapacitorId,
                DisplayName = "Shield Capacitor",
                ShortLabel = "CAP",
                Description = "A high-output capacitor that adds 60 maximum shield capacity with no handling penalty.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Uncommon,
                SellValueAstrons = 1300,
                ShopBuyValueAstronsOverride = 4200,
                IconResourcePath = "Items/shield_capacitor",
                ProjectFileName = "Resources/Items/shield_capacitor.png",
                SalvageOutputs = new[] { AsteroidGoldId, AsteroidResourceId }
            },
            [AegisBatteryId] = new InventoryItemDefinition
            {
                Id = AegisBatteryId,
                DisplayName = "Aegis Battery",
                ShortLabel = "AEB",
                Description = "A shield battery bank that adds 50 maximum shield capacity and doubles shield restored by Battery gadgets.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1700,
                ShopBuyValueAstronsOverride = 5800,
                IconResourcePath = "Items/aegis_battery",
                ProjectFileName = "Resources/Items/aegis_battery.png",
                SalvageOutputs = new[] { BatteryId, AsteroidGoldId }
            },
            [RegenerativeShieldMatrixId] = new InventoryItemDefinition
            {
                Id = RegenerativeShieldMatrixId,
                DisplayName = "Regenerative Shield Matrix",
                ShortLabel = "RSM",
                Description = "A shield matrix that adds 60 maximum shield capacity and slowly regenerates active shields after a quiet moment, but slows the ship.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 1900,
                ShopBuyValueAstronsOverride = 6400,
                IconResourcePath = "Items/regenerative_shield_matrix",
                ProjectFileName = "Resources/Items/regenerative_shield_matrix.png",
                SalvageOutputs = new[] { BatteryId, RadarShipSalvageId }
            },
            [BulwarkProjectorId] = new InventoryItemDefinition
            {
                Id = BulwarkProjectorId,
                DisplayName = "Bulwark Projector",
                ShortLabel = "BLW",
                Description = "A heavy shield projector that adds 75 maximum shield capacity and reduces laser and autocannon damage by 25%.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = 2200,
                ShopBuyValueAstronsOverride = 7200,
                IconResourcePath = "Items/bulwark_projector",
                ProjectFileName = "Resources/Items/bulwark_projector.png",
                SalvageOutputs = new[] { StrongPlatingId, AsteroidRareId }
            },
            [AlienAegisCoreId] = new InventoryItemDefinition
            {
                Id = AlienAegisCoreId,
                DisplayName = "Alien Aegis Core",
                ShortLabel = "AAC",
                Description = "An alien shield core that adds 110 maximum shield capacity and grants a 3 second 50% damage barrier after the shield breaks.",
                ItemType = InventoryItemType.Equipment,
                Category = InventoryItemCategory.Shield,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 3200,
                ShopBuyValueAstronsOverride = 11000,
                IconResourcePath = "Items/alien_aegis_core",
                ProjectFileName = "Resources/Items/alien_aegis_core.png",
                SalvageOutputs = new[] { SpaceAnimalRemainsId, AsteroidEpicId }
            },
            [AlienTransmitterId] = new InventoryItemDefinition
            {
                Id = AlienTransmitterId,
                DisplayName = "Alien Transmitter",
                ShortLabel = "ALT",
                Description = "A unique alien signal device recovered from Space Mayhem. It is a quest item for future projects and special events. It can be carried on a ship only in a SAFE cargo slot, so it survives ship loss unless you drop it yourself.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "alien_transmitter_resource",
                ProjectFileName = "alien_transmitter.png",
                RequiresSafePocket = true,
                SalvageOutputs = Array.Empty<string>()
            },
            [PirateSymbolId] = new InventoryItemDefinition
            {
                Id = PirateSymbolId,
                DisplayName = "Pirate Symbol",
                ShortLabel = "PIR",
                Description = "A pirate oath-mark earned through Project OMERTA. It is a quest item for future projects and special events. It can be carried on a ship only in a SAFE cargo slot, so it survives ship loss unless you drop it yourself.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "Items/pirate_symbol",
                ProjectFileName = "Resources/Items/pirate_symbol.png",
                RequiresSafePocket = true,
                SalvageOutputs = Array.Empty<string>()
            },
            [AvengerStartingCodesId] = new InventoryItemDefinition
            {
                Id = AvengerStartingCodesId,
                DisplayName = "Avenger Starting Codes",
                ShortLabel = "AVG",
                Description = "Encrypted military launch codes for the Avenger. Carry them into Deep Space, Ancient Space, Toxic Area, Minefield or Snow Field to reveal an operational military installation. If the Avenger escape fails, the codes are lost.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Legendary,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 7000,
                IconResourcePath = "Items/avenger_starting_codes",
                ProjectFileName = "Resources/Items/avenger_starting_codes.png",
                SalvageOutputs = Array.Empty<string>()
            },
            [ArrowRaceTokenMinefieldId] = new InventoryItemDefinition
            {
                Id = ArrowRaceTokenMinefieldId,
                DisplayName = "Arrow Race Token - Minefield",
                ShortLabel = "ARM",
                Description = "A Minefield route token used to start an Arrow map race. It is consumed when the race starts.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "Items/arrow_race_token_minefield",
                ProjectFileName = "Resources/Items/arrow_race_token_minefield.png",
                CanEnterSafePocket = false,
                SalvageOutputs = Array.Empty<string>()
            },
            [ArrowRaceTokenSnowFieldId] = new InventoryItemDefinition
            {
                Id = ArrowRaceTokenSnowFieldId,
                DisplayName = "Arrow Race Token - Snow Field",
                ShortLabel = "ARS",
                Description = "A Snow Field route token used to start an Arrow map race. It is consumed when the race starts.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "Items/arrow_race_token_snowfield",
                ProjectFileName = "Resources/Items/arrow_race_token_snowfield.png",
                CanEnterSafePocket = false,
                SalvageOutputs = Array.Empty<string>()
            },
            [ArrowRaceTokenDeepSpaceId] = new InventoryItemDefinition
            {
                Id = ArrowRaceTokenDeepSpaceId,
                DisplayName = "Arrow Race Token - Deep Space",
                ShortLabel = "ARD",
                Description = "A Deep Space route token used to start an Arrow map race. It is consumed when the race starts.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "Items/arrow_race_token_deep_space",
                ProjectFileName = "Resources/Items/arrow_race_token_deep_space.png",
                CanEnterSafePocket = false,
                SalvageOutputs = Array.Empty<string>()
            },
            [ArrowRaceTokenPirateBayId] = new InventoryItemDefinition
            {
                Id = ArrowRaceTokenPirateBayId,
                DisplayName = "Arrow Race Token - Pirate Bay",
                ShortLabel = "ARP",
                Description = "A Pirate Bay route token used to start an Arrow map race. It is consumed when the race starts.",
                ItemType = InventoryItemType.Quest,
                Category = InventoryItemCategory.QuestItem,
                Rarity = InventoryItemRarity.Epic,
                SellValueAstrons = 0,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "Items/arrow_race_token_pirate_bay",
                ProjectFileName = "Resources/Items/arrow_race_token_pirate_bay.png",
                CanEnterSafePocket = false,
                SalvageOutputs = Array.Empty<string>()
            }
        };

        AddBlueprintDefinitions(definitions);
        AddContainerDefinitions(definitions);
        AddBlueprintScrapContainerDefinitions(definitions);
        return definitions;
    }

    static void AddBlueprintDefinitions(Dictionary<string, InventoryItemDefinition> definitions)
    {
        if (definitions == null)
            return;

        List<InventoryItemDefinition> equipmentDefinitions = new List<InventoryItemDefinition>();
        foreach (InventoryItemDefinition definition in definitions.Values)
        {
            if (definition != null &&
                definition.ItemType == InventoryItemType.Equipment &&
                !string.IsNullOrWhiteSpace(definition.Id))
            {
                equipmentDefinitions.Add(definition);
            }
        }

        for (int i = 0; i < equipmentDefinitions.Count; i++)
        {
            InventoryItemDefinition equipment = equipmentDefinitions[i];
            string blueprintId = GetBlueprintItemId(equipment.Id);
            definitions[blueprintId] = new InventoryItemDefinition
            {
                Id = blueprintId,
                DisplayName = equipment.DisplayName + " Blueprint",
                ShortLabel = "BPR",
                Description = "Use this blueprint to permanently unlock crafting for " + equipment.DisplayName + ".",
                ItemType = InventoryItemType.Blueprint,
                Category = InventoryItemCategory.Blueprint,
                Rarity = equipment.Rarity,
                SellValueAstrons = BlueprintSellValueAstrons,
                ShopBuyValueAstronsOverride = 0,
                IconResourcePath = "Items/Blueprints/" + equipment.Id + "_blueprint",
                ProjectFileName = "Resources/Items/Blueprints/" + equipment.Id + "_blueprint.png",
                SalvageOutputs = Array.Empty<string>()
            };
        }
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
                SellValueAstrons = 200,
                IconResourcePath = "kontenery_9",
                IconSpriteName = "kontenery_9_" + i,
                ProjectFileName = "Resources/kontenery_9.png",
                SalvageOutputs = new[] { AsteroidUncommonId }
            };
        }
    }

    static void AddBlueprintScrapContainerDefinitions(Dictionary<string, InventoryItemDefinition> definitions)
    {
        if (definitions == null)
            return;

        for (int i = 0; i < BlueprintScrapContainerVariantCount; i++)
        {
            string itemId = GetBlueprintScrapContainerItemId(i);
            definitions[itemId] = new InventoryItemDefinition
            {
                Id = itemId,
                DisplayName = "Blueprint Container",
                ShortLabel = "BCT",
                Description = "A sealed technical cargo container. Opening it yields blueprint scrap.",
                ItemType = InventoryItemType.Resource,
                Category = InventoryItemCategory.Blueprint,
                Rarity = InventoryItemRarity.Rare,
                SellValueAstrons = BlueprintScrapSellValueAstrons,
                IconResourcePath = "Enemies/ContainerShip/containers_set2",
                IconSpriteName = "containers_set2_" + i,
                ProjectFileName = "Resources/Enemies/ContainerShip/containers_set2.png",
                SalvageOutputs = new[] { BlueprintScrapId }
            };
        }
    }

    static InventoryItemDefinition CreateSpaceJunkDefinition(
        string id,
        string displayName,
        string shortLabel,
        string description,
        string iconResourcePath,
        string projectFileName,
        int sellValueAstrons = 500)
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
            SellValueAstrons = Mathf.Max(0, sellValueAstrons),
            IconResourcePath = iconResourcePath,
            ProjectFileName = projectFileName,
            SalvageOutputs = new[] { AsteroidResourceId, AsteroidResourceId }
        };
    }
}
