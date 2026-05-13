using System;
using System.Collections.Generic;
using UnityEngine;

public enum PilotUnlockType
{
    Starting,
    PersistentUnlock,
    Level,
    DroneKills,
    CharlieSmart
}

public sealed class PilotDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string PortraitResourcePath { get; }
    public string PortraitEditorResourcePath { get; }
    public string PortraitEditorFallbackPath { get; }
    public PilotUnlockType UnlockType { get; }
    public int RequiredLevel { get; }
    public string UnlockDescription { get; }
    public string[] AbilityDescriptions { get; }

    public PilotDefinition(
        string id,
        string displayName,
        string portraitResourcePath,
        string portraitEditorResourcePath,
        string portraitEditorFallbackPath,
        PilotUnlockType unlockType,
        int requiredLevel,
        string unlockDescription,
        string[] abilityDescriptions)
    {
        Id = string.IsNullOrWhiteSpace(id) ? PilotCatalog.JakeId : id.Trim().ToLowerInvariant();
        DisplayName = displayName;
        PortraitResourcePath = portraitResourcePath;
        PortraitEditorResourcePath = portraitEditorResourcePath;
        PortraitEditorFallbackPath = portraitEditorFallbackPath;
        UnlockType = unlockType;
        RequiredLevel = Mathf.Max(1, requiredLevel);
        UnlockDescription = unlockDescription ?? string.Empty;
        AbilityDescriptions = abilityDescriptions ?? Array.Empty<string>();
    }
}

public static class PilotCatalog
{
    public const string JakeId = "jake";
    public const string NovaId = "nova";
    public const string RoburId = "robur";
    public const string SirNowitzkyId = "sir_nowitzky";
    public const string RobyId = "roby";
    public const string CharlieSmartId = "charlie_smart";
    public const int CharlieSmartRequiredSoldAstrons = 20000;
    public const int CharlieSmartRequiredLevel = 10;
    public const int CharlieSmartRequiredPirateBayReturns = 5;

    static readonly PilotDefinition JakeDefinition = new PilotDefinition(
        JakeId,
        "JAKE",
        "UI/Pilots/pilot_01",
        "Assets/Resources/UI/Pilots/pilot_01.png",
        "Assets/pilot_01.png",
        PilotUnlockType.Starting,
        1,
        string.Empty,
        new[]
        {
            "The first time HP drops below 50%, regenerates 1 HP per second for 30 seconds.",
            "Damage from ramming is reduced by 50%.",
            "The astronaut has 3x more HP after losing the ship."
        });

    static readonly PilotDefinition NovaDefinition = new PilotDefinition(
        NovaId,
        "NOVA",
        "UI/Pilots/pilot_02",
        "Assets/Resources/UI/Pilots/pilot_02.png",
        "Assets/pilot_02.png",
        PilotUnlockType.PersistentUnlock,
        1,
        "Return to base with loot worth 1000 Astrons.",
        new[]
        {
            "Killing any enemy grants +20% speed for 5 seconds.",
            "Looting space junk grants 2 items instead of 1.",
            "When flying with no equipment, the main gun has 2x ammo."
        });

    static readonly PilotDefinition RoburDefinition = new PilotDefinition(
        RoburId,
        "ROBUR",
        "UI/Pilots/pilot_03",
        "Assets/Resources/UI/Pilots/pilot_03.png",
        "Assets/pilot_03.png",
        PilotUnlockType.PersistentUnlock,
        1,
        "Kill an enemy human player.",
        new[]
        {
            "Killing an enemy human player grants 2x XP for 60 seconds.",
            "Damage from space mines is reduced by 50%.",
            "Lure Beacons have double charges."
        });

    static readonly PilotDefinition SirNowitzkyDefinition = new PilotDefinition(
        SirNowitzkyId,
        "SIR NOWITZKY",
        "UI/Pilots/pilot_04",
        "Assets/Resources/UI/Pilots/pilot_04.png",
        "Assets/pilot_04.png",
        PilotUnlockType.Level,
        15,
        "Reach XP level 15.",
        new[]
        {
            "Deals 15% more damage to the Mothership.",
            "Nebulas damage shields only and never decrease HP.",
            "Deals 50% more damage to obstacles."
        });

    static readonly PilotDefinition RobyDefinition = new PilotDefinition(
        RobyId,
        "ROBY",
        "UI/Pilots/pilot_05",
        "Assets/Resources/UI/Pilots/pilot_05.png",
        "Assets/pilot_05.png",
        PilotUnlockType.DroneKills,
        30,
        "Kill 30 drones.",
        new[]
        {
            "Battery gadgets have +1 charge each.",
            "Booster recovery cooldown is 1 second shorter.",
            "Looting treasures takes 0.5 second less."
        });

    static readonly PilotDefinition CharlieSmartDefinition = new PilotDefinition(
        CharlieSmartId,
        "CHARLIE \"SMART\"",
        "UI/Pilots/pilot_06",
        "Assets/Resources/UI/Pilots/pilot_06.png",
        "Assets/pilot_06.png",
        PilotUnlockType.CharlieSmart,
        CharlieSmartRequiredLevel,
        "Sell items worth 20000 Astrons. Reach XP level 10. Return to base from Pirate Bay 5 times.",
        new[]
        {
            "Gets a 5% discount when buying items from traders/shop.",
            "Pirate Fighters, Elites and Aces do not attack unless attacked first.",
            "Returning in the last 30 seconds grants extra 1000 Astrons."
        });

    static readonly PilotDefinition[] Definitions =
    {
        JakeDefinition,
        NovaDefinition,
        RoburDefinition,
        SirNowitzkyDefinition,
        RobyDefinition,
        CharlieSmartDefinition
    };

    static readonly Dictionary<string, PilotDefinition> DefinitionsById = BuildDefinitionsById();

    public static IReadOnlyList<PilotDefinition> AllDefinitions => Definitions;

    static Dictionary<string, PilotDefinition> BuildDefinitionsById()
    {
        Dictionary<string, PilotDefinition> result = new Dictionary<string, PilotDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].Id] = Definitions[i];

        return result;
    }

    public static string NormalizePilotId(string pilotId)
    {
        if (string.IsNullOrWhiteSpace(pilotId))
            return JakeId;

        string normalized = pilotId.Trim().ToLowerInvariant();
        return DefinitionsById != null && DefinitionsById.ContainsKey(normalized) ? normalized : JakeId;
    }

    public static bool IsValidPilotId(string pilotId)
    {
        if (string.IsNullOrWhiteSpace(pilotId))
            return false;

        return DefinitionsById.ContainsKey(pilotId.Trim().ToLowerInvariant());
    }

    public static PilotDefinition GetDefinition(string pilotId)
    {
        string normalized = NormalizePilotId(pilotId);
        return DefinitionsById.TryGetValue(normalized, out PilotDefinition definition) ? definition : JakeDefinition;
    }

    public static int GetPilotIndex(string pilotId)
    {
        string normalized = NormalizePilotId(pilotId);
        for (int i = 0; i < Definitions.Length; i++)
        {
            if (Definitions[i].Id == normalized)
                return i;
        }

        return 0;
    }

    public static string[] GetDefaultUnlockedPilotIds()
    {
        return new[] { JakeId };
    }

    public static string[] NormalizeUnlockedPilotIds(string[] source)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal) { JakeId };
        if (source != null)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (IsValidPilotId(source[i]))
                    ids.Add(NormalizePilotId(source[i]));
            }
        }

        string[] result = new string[ids.Count];
        ids.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    public static bool IsPilotUnlocked(PlayerProfileData profile, string pilotId)
    {
        PilotDefinition definition = GetDefinition(pilotId);
        if (definition.UnlockType == PilotUnlockType.Starting)
            return true;

        if (definition.UnlockType == PilotUnlockType.Level)
        {
            int totalXp = profile != null ? profile.TotalXp : 0;
            return RoundXpBalance.GetLevelForTotalXp(totalXp) >= definition.RequiredLevel;
        }

        if (definition.UnlockType == PilotUnlockType.DroneKills)
        {
            int droneKills = profile != null ? profile.PilotDroneKills : 0;
            return droneKills >= definition.RequiredLevel;
        }

        if (definition.UnlockType == PilotUnlockType.CharlieSmart)
        {
            int totalXp = profile != null ? profile.TotalXp : 0;
            int level = RoundXpBalance.GetLevelForTotalXp(totalXp);
            int soldAstrons = profile != null ? profile.PilotSoldItemsAstrons : 0;
            int pirateBayReturns = profile != null ? profile.PilotPirateBayReturns : 0;
            return soldAstrons >= CharlieSmartRequiredSoldAstrons &&
                   level >= CharlieSmartRequiredLevel &&
                   pirateBayReturns >= CharlieSmartRequiredPirateBayReturns;
        }

        string normalized = NormalizePilotId(pilotId);
        string[] unlocked = profile != null ? profile.UnlockedPilotIds : null;
        if (unlocked == null)
            return false;

        for (int i = 0; i < unlocked.Length; i++)
        {
            if (NormalizePilotId(unlocked[i]) == normalized)
                return true;
        }

        return false;
    }

    public static bool IsSelectedPilot(Photon.Realtime.Player player, string pilotId)
    {
        return string.Equals(RoomSettings.GetPlayerPilotId(player, JakeId), NormalizePilotId(pilotId), StringComparison.Ordinal);
    }

    public static string GetUnlockRequirementText(PlayerProfileData profile, string pilotId)
    {
        PilotDefinition definition = GetDefinition(pilotId);
        if (IsPilotUnlocked(profile, definition.Id))
            return "UNLOCKED";

        return string.IsNullOrWhiteSpace(definition.UnlockDescription)
            ? "LOCKED"
            : GetUnlockDescriptionWithProgress(profile, definition).ToUpperInvariant();
    }

    static string GetUnlockDescriptionWithProgress(PlayerProfileData profile, PilotDefinition definition)
    {
        if (definition.UnlockType == PilotUnlockType.DroneKills)
        {
            int progress = Mathf.Clamp(profile != null ? profile.PilotDroneKills : 0, 0, definition.RequiredLevel);
            return "Kill " + definition.RequiredLevel + " drones (" + progress + "/" + definition.RequiredLevel + ").";
        }

        if (definition.UnlockType == PilotUnlockType.CharlieSmart)
        {
            int soldAstrons = Mathf.Clamp(profile != null ? profile.PilotSoldItemsAstrons : 0, 0, CharlieSmartRequiredSoldAstrons);
            int totalXp = profile != null ? profile.TotalXp : 0;
            int level = Mathf.Clamp(RoundXpBalance.GetLevelForTotalXp(totalXp), 0, CharlieSmartRequiredLevel);
            int pirateBayReturns = Mathf.Clamp(profile != null ? profile.PilotPirateBayReturns : 0, 0, CharlieSmartRequiredPirateBayReturns);
            return "Sell items worth " + CharlieSmartRequiredSoldAstrons + " Astrons (" + soldAstrons + "/" + CharlieSmartRequiredSoldAstrons + ").\n" +
                   "Reach XP level " + CharlieSmartRequiredLevel + " (" + level + "/" + CharlieSmartRequiredLevel + ").\n" +
                   "Return from Pirate Bay " + CharlieSmartRequiredPirateBayReturns + " times (" + pirateBayReturns + "/" + CharlieSmartRequiredPirateBayReturns + ").";
        }

        return definition.UnlockDescription;
    }

    public static bool IsSpaceJunkItem(string itemId)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        return definition != null && definition.Category == InventoryItemCategory.SpaceJunk;
    }

    public static int GetCargoValueAstrons(string[] shipSlots)
    {
        if (shipSlots == null)
            return 0;

        int total = 0;
        for (int i = 0; i < shipSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(shipSlots[i]))
                total += InventoryItemCatalog.GetSellValueAstrons(shipSlots[i]);
        }

        return Mathf.Max(0, total);
    }
}
