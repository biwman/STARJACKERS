using System.Collections.Generic;
using UnityEngine;

public enum ShipType
{
    Explorer = 0,
    Viper = 1,
    Avenger = 2,
    Arrow = 3,
    Invader = 4,
    CargoTruck = 5,
    Pathfinder = 6
}

public sealed class PlayerShipDefinition
{
    public ShipType Type { get; }
    public string DisplayName { get; }
    public int[] SkinIndices { get; }
    public int CargoCapacity { get; }
    public int SafePocketSlots { get; }
    public int MainGunSlots { get; }
    public int ShieldSlots { get; }
    public int EngineSlots { get; }
    public int GadgetSlots { get; }
    public int SupportSlots { get; }
    public int RescueSlots { get; }
    public int BaseHp { get; }
    public int BaseShield { get; }
    public float BaseSpeed { get; }
    public float TurnRateMultiplier { get; }
    public float BoosterDuration { get; }
    public int MaxBoostPercent { get; }
    public int BrakingDriftLevel { get; }
    public Vector2[] ThrusterOffsetFactors { get; }

    public PlayerShipDefinition(
        ShipType type,
        string displayName,
        int[] skinIndices,
        int cargoCapacity,
        int safePocketSlots,
        int mainGunSlots,
        int shieldSlots,
        int engineSlots,
        int gadgetSlots,
        int supportSlots,
        int rescueSlots,
        int baseHp,
        int baseShield,
        float baseSpeed,
        float turnRateMultiplier,
        float boosterDuration,
        int maxBoostPercent,
        int brakingDriftLevel,
        Vector2[] thrusterOffsetFactors)
    {
        Type = type;
        DisplayName = displayName;
        SkinIndices = skinIndices;
        CargoCapacity = cargoCapacity;
        SafePocketSlots = Mathf.Max(0, safePocketSlots);
        MainGunSlots = mainGunSlots;
        ShieldSlots = shieldSlots;
        EngineSlots = engineSlots;
        GadgetSlots = gadgetSlots;
        SupportSlots = supportSlots;
        RescueSlots = rescueSlots;
        BaseHp = baseHp;
        BaseShield = baseShield;
        BaseSpeed = baseSpeed;
        TurnRateMultiplier = turnRateMultiplier;
        BoosterDuration = boosterDuration;
        MaxBoostPercent = Mathf.Max(0, maxBoostPercent);
        BrakingDriftLevel = Mathf.Clamp(brakingDriftLevel, 0, 10);
        ThrusterOffsetFactors = thrusterOffsetFactors;
    }
}

public static class ShipCatalog
{
    public const int ExplorerBasicSkinIndex = 0;
    public const int ExplorerGildedSkinIndex = 1;
    public const int ExplorerSilverSkinIndex = 2;
    public const int ViperStandardSkinIndex = 3;
    public const int ViperSnowSkinIndex = 4;
    public const int ViperNavySkinIndex = 5;
    public const int AvengerDarkGreenSkinIndex = 6;
    public const int AvengerMilitarySkinIndex = 7;
    public const int AvengerNasaSkinIndex = 8;
    public const int ArrowSmoothSkinIndex = 9;
    public const int ArrowSportySkinIndex = 10;
    public const int ArrowSharkSkinIndex = 11;
    public const int InvaderCamoSkinIndex = 12;
    public const int InvaderVolcanicSkinIndex = 13;
    public const int InvaderGoldplateSkinIndex = 14;
    public const int CargoTruckGreenTruckSkinIndex = 15;
    public const int CargoTruckWhitePantherSkinIndex = 16;
    public const int CargoTruckSandSigmaSkinIndex = 17;
    public const int PathfinderClassicSkinIndex = 18;
    public const int PathfinderAngelSkinIndex = 19;
    public const int PathfinderExtravaganceSkinIndex = 20;
    public const int MaxShipSkinIndex = PathfinderExtravaganceSkinIndex;

    static readonly PlayerShipDefinition ExplorerDefinition = new PlayerShipDefinition(
        ShipType.Explorer,
        "Explorer",
        new[] { ExplorerBasicSkinIndex, ExplorerSilverSkinIndex, ExplorerGildedSkinIndex },
        8,
        0,
        1,
        1,
        1,
        1,
        0,
        1,
        75,
        40,
        3.125f,
        1f,
        10f,
        30,
        4,
        new[] { new Vector2(0f, 0.02f) });

    static readonly PlayerShipDefinition ViperDefinition = new PlayerShipDefinition(
        ShipType.Viper,
        "Viper",
        new[] { ViperStandardSkinIndex, ViperSnowSkinIndex, ViperNavySkinIndex },
        10,
        1,
        2,
        1,
        2,
        0,
        1,
        1,
        60,
        30,
        3.75f,
        1.2f,
        8f,
        40,
        3,
        new[]
        {
            new Vector2(-2.24f, 0.34f),
            new Vector2(2.24f, 0.34f)
        });

    static readonly PlayerShipDefinition AvengerDefinition = new PlayerShipDefinition(
        ShipType.Avenger,
        "Avenger",
        new[] { AvengerDarkGreenSkinIndex, AvengerMilitarySkinIndex, AvengerNasaSkinIndex },
        8,
        1,
        2,
        2,
        0,
        1,
        1,
        1,
        98,
        55,
        2.625f,
        0.82f,
        12f,
        30,
        5,
        new[] { new Vector2(0f, 0.02f) });

    static readonly PlayerShipDefinition ArrowDefinition = new PlayerShipDefinition(
        ShipType.Arrow,
        "Arrow",
        new[] { ArrowSmoothSkinIndex, ArrowSportySkinIndex, ArrowSharkSkinIndex },
        6,
        1,
        1,
        1,
        1,
        1,
        0,
        1,
        63,
        30,
        4.25f,
        1.45f,
        7.2f,
        20,
        1,
        new[]
        {
            new Vector2(-1.82f, 0.28f),
            new Vector2(0f, 0.2f),
            new Vector2(1.82f, 0.28f)
        });

    static readonly PlayerShipDefinition InvaderDefinition = new PlayerShipDefinition(
        ShipType.Invader,
        "Invader",
        new[] { InvaderCamoSkinIndex, InvaderVolcanicSkinIndex, InvaderGoldplateSkinIndex },
        9,
        2,
        1,
        1,
        0,
        2,
        2,
        1,
        72,
        30,
        3.375f,
        1.08f,
        10.8f,
        25,
        2,
        new[] { new Vector2(0f, 0.18f) });

    static readonly PlayerShipDefinition CargoTruckDefinition = new PlayerShipDefinition(
        ShipType.CargoTruck,
        "BISON",
        new[] { CargoTruckGreenTruckSkinIndex, CargoTruckWhitePantherSkinIndex, CargoTruckSandSigmaSkinIndex },
        14,
        1,
        0,
        2,
        1,
        2,
        2,
        1,
        140,
        30,
        2.15f,
        0.68f,
        7f,
        15,
        7,
        new[]
        {
            new Vector2(-1.7f, 0.18f),
            new Vector2(0f, 0.12f),
            new Vector2(1.7f, 0.18f)
        });

    static readonly PlayerShipDefinition PathfinderDefinition = new PlayerShipDefinition(
        ShipType.Pathfinder,
        "Pathfinder",
        new[] { PathfinderClassicSkinIndex, PathfinderAngelSkinIndex, PathfinderExtravaganceSkinIndex },
        8,
        2,
        1,
        1,
        1,
        2,
        2,
        0,
        50,
        50,
        3.85f,
        1.25f,
        6f,
        40,
        3,
        new[]
        {
            new Vector2(-1.35f, 0.22f),
            new Vector2(1.35f, 0.22f)
        });

    static readonly Dictionary<ShipType, PlayerShipDefinition> Definitions = new Dictionary<ShipType, PlayerShipDefinition>
    {
        { ShipType.Explorer, ExplorerDefinition },
        { ShipType.Viper, ViperDefinition },
        { ShipType.Avenger, AvengerDefinition },
        { ShipType.Arrow, ArrowDefinition },
        { ShipType.Invader, InvaderDefinition },
        { ShipType.CargoTruck, CargoTruckDefinition },
        { ShipType.Pathfinder, PathfinderDefinition }
    };

    public static string GetShipTypeId(ShipType shipType)
    {
        return shipType switch
        {
            ShipType.Viper => "viper",
            ShipType.Avenger => "avenger",
            ShipType.Arrow => "arrow",
            ShipType.Invader => "invader",
            ShipType.CargoTruck => "cargo_truck",
            ShipType.Pathfinder => "pathfinder",
            _ => "explorer"
        };
    }

    public static bool TryGetShipTypeFromId(string shipTypeId, out ShipType shipType)
    {
        string normalized = string.IsNullOrWhiteSpace(shipTypeId)
            ? string.Empty
            : shipTypeId.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case "explorer":
                shipType = ShipType.Explorer;
                return true;
            case "viper":
                shipType = ShipType.Viper;
                return true;
            case "avenger":
                shipType = ShipType.Avenger;
                return true;
            case "arrow":
                shipType = ShipType.Arrow;
                return true;
            case "invader":
                shipType = ShipType.Invader;
                return true;
            case "bison":
            case "cargo_truck":
                shipType = ShipType.CargoTruck;
                return true;
            case "pathfinder":
                shipType = ShipType.Pathfinder;
                return true;
            default:
                shipType = ShipType.Explorer;
                return false;
        }
    }

    public static string NormalizeShipTypeId(string shipTypeId)
    {
        return TryGetShipTypeFromId(shipTypeId, out ShipType shipType)
            ? GetShipTypeId(shipType)
            : string.Empty;
    }

    public static string[] GetAllShipTypeIds()
    {
        string[] ids =
        {
            GetShipTypeId(ShipType.Explorer),
            GetShipTypeId(ShipType.Viper),
            GetShipTypeId(ShipType.Avenger),
            GetShipTypeId(ShipType.Arrow),
            GetShipTypeId(ShipType.Invader),
            GetShipTypeId(ShipType.CargoTruck),
            GetShipTypeId(ShipType.Pathfinder)
        };
        return ids;
    }

    public static string[] GetDefaultUnlockedShipTypeIds()
    {
        return new[] { GetShipTypeId(ShipType.Explorer) };
    }

    public static PlayerShipDefinition GetShipDefinition(int skinIndex)
    {
        return GetShipDefinition(GetShipTypeFromSkinIndex(skinIndex));
    }

    public static PlayerShipDefinition GetShipDefinition(ShipType shipType)
    {
        return Definitions.TryGetValue(shipType, out PlayerShipDefinition definition) ? definition : ExplorerDefinition;
    }

    public static ShipType GetShipTypeFromSkinIndex(int skinIndex)
    {
        return skinIndex switch
        {
            >= PathfinderClassicSkinIndex => ShipType.Pathfinder,
            >= CargoTruckGreenTruckSkinIndex => ShipType.CargoTruck,
            >= InvaderCamoSkinIndex => ShipType.Invader,
            >= ArrowSmoothSkinIndex => ShipType.Arrow,
            >= AvengerDarkGreenSkinIndex => ShipType.Avenger,
            >= ViperStandardSkinIndex => ShipType.Viper,
            _ => ShipType.Explorer
        };
    }

    public static string GetShipTypeDisplayName(ShipType shipType)
    {
        return GetShipDefinition(shipType).DisplayName;
    }

    public static int[] GetSkinsForShipType(ShipType shipType)
    {
        return GetShipDefinition(shipType).SkinIndices;
    }

    public static string GetSkinDisplayName(int skinIndex)
    {
        switch (skinIndex)
        {
            case ExplorerBasicSkinIndex: return "Basic";
            case ExplorerSilverSkinIndex: return "Silver Shine";
            case ExplorerGildedSkinIndex: return "Gilded Armour";
            case ViperStandardSkinIndex: return "Standard";
            case ViperSnowSkinIndex: return "Snow";
            case ViperNavySkinIndex: return "Navy";
            case AvengerDarkGreenSkinIndex: return "Darkgreen";
            case AvengerMilitarySkinIndex: return "Military";
            case AvengerNasaSkinIndex: return "NASA";
            case ArrowSmoothSkinIndex: return "Smooth";
            case ArrowSportySkinIndex: return "Sporty";
            case ArrowSharkSkinIndex: return "Shark";
            case InvaderCamoSkinIndex: return "Camo";
            case InvaderVolcanicSkinIndex: return "Volcanic";
            case InvaderGoldplateSkinIndex: return "Goldplate";
            case CargoTruckGreenTruckSkinIndex: return "Green Truck";
            case CargoTruckWhitePantherSkinIndex: return "White Panther";
            case CargoTruckSandSigmaSkinIndex: return "Sand Sigma";
            case PathfinderClassicSkinIndex: return "Classic";
            case PathfinderAngelSkinIndex: return "Angel";
            case PathfinderExtravaganceSkinIndex: return "Extravagance";
            default: return "Skin";
        }
    }

    public static int GetShipInventoryCapacity(int skinIndex)
    {
        return GetShipDefinition(skinIndex).CargoCapacity;
    }

    public static int GetSafePocketSlots(int skinIndex)
    {
        return Mathf.Clamp(GetShipDefinition(skinIndex).SafePocketSlots, 0, GetShipInventoryCapacity(skinIndex));
    }

    public static int GetMainGunSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).MainGunSlots;
    }

    public static int GetShieldSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).ShieldSlots;
    }

    public static int GetEngineSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).EngineSlots;
    }

    public static int GetGadgetSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).GadgetSlots;
    }

    public static int GetSupportSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).SupportSlots;
    }

    public static int GetRescueSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).RescueSlots;
    }

    public static int GetBaseHp(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BaseHp;
    }

    public static int GetBaseShield(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BaseShield;
    }

    public static float GetBaseSpeed(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BaseSpeed;
    }

    public static float GetTurnRateMultiplier(int skinIndex)
    {
        return GetShipDefinition(skinIndex).TurnRateMultiplier;
    }

    public static float GetBoosterDuration(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BoosterDuration;
    }

    public static int GetMaxBoostPercent(int skinIndex)
    {
        return GetShipDefinition(skinIndex).MaxBoostPercent;
    }

    public static int GetBrakingDriftLevel(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BrakingDriftLevel;
    }

    public static Vector2[] GetThrusterOffsetFactors(int skinIndex)
    {
        return GetShipDefinition(skinIndex).ThrusterOffsetFactors;
    }

    public static bool IsEquipmentSlotEnabled(int slotIndex, int shipSkinIndex)
    {
        return slotIndex switch
        {
            0 => GetMainGunSlots(shipSkinIndex) >= 1,
            1 => GetMainGunSlots(shipSkinIndex) >= 2,
            2 => GetShieldSlots(shipSkinIndex) >= 1,
            3 => GetShieldSlots(shipSkinIndex) >= 2,
            4 => GetEngineSlots(shipSkinIndex) >= 1,
            5 => GetEngineSlots(shipSkinIndex) >= 2,
            6 => GetGadgetSlots(shipSkinIndex) >= 1,
            7 => GetGadgetSlots(shipSkinIndex) >= 2,
            8 => GetSupportSlots(shipSkinIndex) >= 1,
            9 => GetSupportSlots(shipSkinIndex) >= 2,
            10 => GetRescueSlots(shipSkinIndex) >= 1,
            11 => GetRescueSlots(shipSkinIndex) >= 2,
            _ => false
        };
    }

    public static string GetEquipmentSlotLabel(int slotIndex)
    {
        return slotIndex switch
        {
            0 => "MAIN GUN",
            1 => "MAIN GUN",
            2 => "SHIELD",
            3 => "SHIELD",
            4 => "ENGINE",
            5 => "ENGINE",
            6 => "GADGET",
            7 => "GADGET",
            8 => "SUPPORT",
            9 => "SUPPORT",
            10 => "RESCUE",
            11 => "RESCUE",
            _ => "SLOT"
        };
    }

    public static string GetShipSkinResourcePath(int skinIndex)
    {
        return skinIndex switch
        {
            ExplorerBasicSkinIndex => "Visuals/Ships/ship1_resource",
            ExplorerGildedSkinIndex => "Visuals/Ships/ship2_resource",
            ExplorerSilverSkinIndex => "Visuals/Ships/ship3_resource",
            ViperStandardSkinIndex => "ship4_resource",
            ViperSnowSkinIndex => "Visuals/Ships/viper_snow_resource",
            ViperNavySkinIndex => "Visuals/Ships/viper_navy_clean_resource",
            AvengerDarkGreenSkinIndex => "Visuals/Ships/avenger_darkgreen_resource",
            AvengerMilitarySkinIndex => "Visuals/Ships/avenger_military_resource",
            AvengerNasaSkinIndex => "Visuals/Ships/avenger_nasa_resource",
            ArrowSmoothSkinIndex => "Visuals/Ships/arrow_skin_smooth_resource",
            ArrowSportySkinIndex => "Visuals/Ships/arrow_skin_sporty_resource",
            ArrowSharkSkinIndex => "Visuals/Ships/arrow_skin_shark_resource",
            InvaderCamoSkinIndex => "Visuals/Ships/invader_camo_resource",
            InvaderVolcanicSkinIndex => "Visuals/Ships/invader_volcanic_resource",
            InvaderGoldplateSkinIndex => "Visuals/Ships/invader_goldplate_resource",
            CargoTruckGreenTruckSkinIndex => "Visuals/Ships/bison_green_truck",
            CargoTruckWhitePantherSkinIndex => "Visuals/Ships/bison_white_panther",
            CargoTruckSandSigmaSkinIndex => "Visuals/Ships/bison_sand_sigma",
            PathfinderClassicSkinIndex => "Visuals/Ships/pathfinder_classic",
            PathfinderAngelSkinIndex => "Visuals/Ships/pathfinder_angel",
            PathfinderExtravaganceSkinIndex => "Visuals/Ships/pathfinder_extravagance",
            _ => "Visuals/Ships/ship1_resource"
        };
    }

    public static string GetShipSkinEditorResourcePath(int skinIndex)
    {
        return skinIndex switch
        {
            ExplorerBasicSkinIndex => "Assets/Resources/Visuals/Ships/ship1_resource.png",
            ExplorerGildedSkinIndex => "Assets/Resources/Visuals/Ships/ship2_resource.png",
            ExplorerSilverSkinIndex => "Assets/Resources/Visuals/Ships/ship3_resource.png",
            ViperStandardSkinIndex => "Assets/Resources/ship4_resource.png",
            ViperSnowSkinIndex => "Assets/Resources/Visuals/Ships/viper_snow_resource.png",
            ViperNavySkinIndex => "Assets/Resources/Visuals/Ships/viper_navy_clean_resource.png",
            AvengerDarkGreenSkinIndex => "Assets/Resources/Visuals/Ships/avenger_darkgreen_resource.png",
            AvengerMilitarySkinIndex => "Assets/Resources/Visuals/Ships/avenger_military_resource.png",
            AvengerNasaSkinIndex => "Assets/Resources/Visuals/Ships/avenger_nasa_resource.png",
            ArrowSmoothSkinIndex => "Assets/Resources/Visuals/Ships/arrow_skin_smooth_resource.png",
            ArrowSportySkinIndex => "Assets/Resources/Visuals/Ships/arrow_skin_sporty_resource.png",
            ArrowSharkSkinIndex => "Assets/Resources/Visuals/Ships/arrow_skin_shark_resource.png",
            InvaderCamoSkinIndex => "Assets/Resources/Visuals/Ships/invader_camo_resource.png",
            InvaderVolcanicSkinIndex => "Assets/Resources/Visuals/Ships/invader_volcanic_resource.png",
            InvaderGoldplateSkinIndex => "Assets/Resources/Visuals/Ships/invader_goldplate_resource.png",
            CargoTruckGreenTruckSkinIndex => "Assets/Resources/Visuals/Ships/bison_green_truck.png",
            CargoTruckWhitePantherSkinIndex => "Assets/Resources/Visuals/Ships/bison_white_panther.png",
            CargoTruckSandSigmaSkinIndex => "Assets/Resources/Visuals/Ships/bison_sand_sigma.png",
            PathfinderClassicSkinIndex => "Assets/Resources/Visuals/Ships/pathfinder_classic.png",
            PathfinderAngelSkinIndex => "Assets/Resources/Visuals/Ships/pathfinder_angel.png",
            PathfinderExtravaganceSkinIndex => "Assets/Resources/Visuals/Ships/pathfinder_extravagance.png",
            _ => "Assets/Resources/Visuals/Ships/ship1_resource.png"
        };
    }

    public static string GetShipSkinEditorFallbackPath(int skinIndex)
    {
        return skinIndex switch
        {
            ExplorerBasicSkinIndex => "Assets/ship1.png",
            ExplorerGildedSkinIndex => "Assets/ship2.png",
            ExplorerSilverSkinIndex => "Assets/ship3.png",
            ViperStandardSkinIndex => "Assets/ship4.png",
            ViperSnowSkinIndex => "Assets/Viper_skin_white.png",
            ViperNavySkinIndex => "Assets/Viper_skin_navy_clean_v2.png",
            AvengerDarkGreenSkinIndex => "Assets/Avenger_skin_darkgreen.png",
            AvengerMilitarySkinIndex => "Assets/Avenger_skin_military.png",
            AvengerNasaSkinIndex => "Assets/Avenger_skin_nasa.png",
            ArrowSmoothSkinIndex => "Assets/arrow_skin_smooth.png",
            ArrowSportySkinIndex => "Assets/arrow_skin_sporty.png",
            ArrowSharkSkinIndex => "Assets/arrow_skin_shark.png",
            InvaderCamoSkinIndex => "Assets/invader_camo.png",
            InvaderVolcanicSkinIndex => "Assets/invader_volcanic.png",
            InvaderGoldplateSkinIndex => "Assets/invader_goldplate.png",
            CargoTruckGreenTruckSkinIndex => "Assets/Resources/Visuals/Ships/bison_green_truck.png",
            CargoTruckWhitePantherSkinIndex => "Assets/Resources/Visuals/Ships/bison_white_panther.png",
            CargoTruckSandSigmaSkinIndex => "Assets/Resources/Visuals/Ships/bison_sand_sigma.png",
            PathfinderClassicSkinIndex => "Assets/Resources/Visuals/Ships/pathfinder_classic.png",
            PathfinderAngelSkinIndex => "Assets/Resources/Visuals/Ships/pathfinder_angel.png",
            PathfinderExtravaganceSkinIndex => "Assets/Resources/Visuals/Ships/pathfinder_extravagance.png",
            _ => "Assets/ship1.png"
        };
    }

    public static string GetWreckResourcePathForSkin(int skinIndex)
    {
        if (GetShipTypeFromSkinIndex(skinIndex) == ShipType.Pathfinder)
            return "Visuals/Ships/pathfinder_wreck";

        if (GetShipTypeFromSkinIndex(skinIndex) == ShipType.CargoTruck)
            return "Visuals/Ships/bison_wreck";

        return GetShipTypeFromSkinIndex(skinIndex) switch
        {
            ShipType.Viper => "wrak2_resource",
            ShipType.Avenger => "wrak3_resource",
            ShipType.Arrow => "Visuals/Ships/arrow_ship_wreck_resource",
            ShipType.Invader => "Visuals/Ships/invader_wreck_resource",
            _ => "wrak1_resource"
        };
    }

    public static string GetWreckEditorResourcePathForSkin(int skinIndex)
    {
        if (GetShipTypeFromSkinIndex(skinIndex) == ShipType.Pathfinder)
            return "Assets/Resources/Visuals/Ships/pathfinder_wreck.png";

        if (GetShipTypeFromSkinIndex(skinIndex) == ShipType.CargoTruck)
            return "Assets/Resources/Visuals/Ships/bison_wreck.png";

        return GetShipTypeFromSkinIndex(skinIndex) switch
        {
            ShipType.Viper => "Assets/Resources/wrak2_resource.png",
            ShipType.Avenger => "Assets/Resources/wrak3_resource.png",
            ShipType.Arrow => "Assets/Resources/Visuals/Ships/arrow_ship_wreck_resource.png",
            ShipType.Invader => "Assets/Resources/Visuals/Ships/invader_wreck_resource.png",
            _ => "Assets/Resources/wrak1_resource.png"
        };
    }

    public static string GetWreckEditorFallbackPathForSkin(int skinIndex)
    {
        if (GetShipTypeFromSkinIndex(skinIndex) == ShipType.Pathfinder)
            return "Assets/Resources/Visuals/Ships/pathfinder_wreck.png";

        if (GetShipTypeFromSkinIndex(skinIndex) == ShipType.CargoTruck)
            return "Assets/Resources/Visuals/Ships/bison_wreck.png";

        return GetShipTypeFromSkinIndex(skinIndex) switch
        {
            ShipType.Viper => "Assets/wrak2.png",
            ShipType.Avenger => "Assets/wrak3.png",
            ShipType.Arrow => "Assets/arrow_ship_wreck.png",
            ShipType.Invader => "Assets/invader_wreck.png",
            _ => "Assets/wrak1.png"
        };
    }
}
