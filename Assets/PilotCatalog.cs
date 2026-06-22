using System;
using System.Collections.Generic;
using UnityEngine;

public enum PilotUnlockType
{
    Starting,
    PersistentUnlock,
    Level,
    DroneKills,
    CharlieSmart,
    AsteroidSalvage,
    OverloadReturns,
    CompletedProjects,
    AtlasMapReturns
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
    public string ActiveAbilityName { get; }
    public string ActiveAbilityDescription { get; }

    public PilotDefinition(
        string id,
        string displayName,
        string portraitResourcePath,
        string portraitEditorResourcePath,
        string portraitEditorFallbackPath,
        PilotUnlockType unlockType,
        int requiredLevel,
        string unlockDescription,
        string[] abilityDescriptions,
        string activeAbilityName,
        string activeAbilityDescription)
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
        ActiveAbilityName = activeAbilityName ?? string.Empty;
        ActiveAbilityDescription = activeAbilityDescription ?? string.Empty;
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
    public const string CovaxId = "covax";
    public const string AshId = "ash";
    public const string VectorId = "vector";
    public const string AtlasId = "atlas";
    public const int CharlieSmartRequiredSoldAstrons = 20000;
    public const int CharlieSmartRequiredLevel = 10;
    public const int CharlieSmartRequiredPirateBayReturns = 5;
    public const int CovaxRequiredAsteroidSalvage = 200;
    public const int AshRequiredOverloadReturns = 3;
    public const int VectorRequiredCompletedProjects = 2;
    public const int AtlasRequiredNetCargoAstrons = 1000;
    public const int AtlasPassiveNetCargoAstrons = 2000;

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
        },
        "Emergency Barrier",
        "Activate a shield for 10 seconds, reducing all incoming damage by 50%.");

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
        },
        "Scavenger Burst",
        "Channel collection beams for 2.5 seconds to every collectible object within 2.5x the normal collection range, then collect them.");

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
            "Lure Beacons and Space Traps have double charges."
        },
        "Red Mark",
        "Mark the nearest enemy or player for 15 seconds. The marked target takes 50% increased damage from all sources.");

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
        },
        "Breach Protocol",
        "The next 5 salvos deal 100% increased damage to bosses and obstacles. Every projectile fired in those salvos is empowered. Bosses currently include the Mothership and Pirate Base.");

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
        },
        "Field Reboot",
        "Restore 1 charge to an equipped gadget that has been used. If several gadgets are missing charges, one is chosen at random. Also fully restores the booster.");

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
            "Returning with at least 6 more occupied cargo slots than at round start grants extra 1000 Astrons.",
            "Damage taken from pirate enemies is reduced by 20%.",
            "Returning in the last 30 seconds grants extra 2000 Astrons."
        },
        "Confusion Wave",
        "Release a green wave that scrambles all computer-controlled enemies in roughly screen range for 10 seconds, making them move randomly and fire in random directions.");

    static readonly PilotDefinition CovaxDefinition = new PilotDefinition(
        CovaxId,
        "COVAX",
        "UI/Pilots/pilot_07",
        "Assets/Resources/UI/Pilots/pilot_07.png",
        "Assets/pilot_07.png",
        PilotUnlockType.AsteroidSalvage,
        CovaxRequiredAsteroidSalvage,
        "Collect 200 asteroids.",
        new[]
        {
            "Collecting an asteroid has a 10% chance to upgrade it by one rarity level before it enters cargo.",
            "Rocket weapons have +1 ammo.",
            "When losing the ship, engine-slot equipment is kept even on maps with equipment loss."
        },
        "Electromagnetic Wave",
        "Release a cone-shaped EMP wave from the ship nose across roughly one screen. Hit ships are shocked for 10 seconds, moving 3x slower and firing 3x less often.");

    static readonly PilotDefinition AshDefinition = new PilotDefinition(
        AshId,
        "ASH",
        "UI/Pilots/pilot_08",
        "Assets/Resources/UI/Pilots/pilot_08.png",
        "Assets/pilot_08.png",
        PilotUnlockType.OverloadReturns,
        AshRequiredOverloadReturns,
        "Complete 3 Overload Returns: extract with full cargo, cargo worth 1500 Astrons, 3 computer enemy kills and 20 seconds of booster use in one run.",
        new[]
        {
            "Plasma Gun, Rail Gun and Double Ionizer ammo recharges 10% faster.",
            "Booster drains 12% slower.",
            "After ammo reaches zero, the first recovered ammo charge comes back 25% faster."
        },
        "Supercharge",
        "Overload the ship for 10 seconds, increasing ship speed, fire rate and ammo recharge speed by 30% with a visible charge effect.");

    static readonly PilotDefinition VectorDefinition = new PilotDefinition(
        VectorId,
        "VECTOR",
        "UI/Pilots/pilot_09",
        "Assets/Resources/UI/Pilots/pilot_09.png",
        "Assets/Resources/UI/Pilots/pilot_09.png",
        PilotUnlockType.CompletedProjects,
        VectorRequiredCompletedProjects,
        "Complete any 2 projects.",
        new[]
        {
            "Common and Uncommon resource collection is 3x faster.",
            "Repeated hits on the same computer-controlled target or obstacle build analysis, increasing damage by 5% per hit up to 20%. The analysis resets after 3 seconds or when changing targets.",
            "Rocket lock-on is 50% faster, Guidance System lasts 15 seconds longer, and Short Scanner lasts 10 seconds longer."
        },
        "Multi-Vector Barrage",
        "Fire Gatling Super-style barrages toward up to 5 nearest enemies.");

    static readonly PilotDefinition AtlasDefinition = new PilotDefinition(
        AtlasId,
        "ATLAS",
        "UI/Pilots/pilot_10",
        "Assets/Resources/UI/Pilots/pilot_10.png",
        "Assets/Resources/UI/Pilots/pilot_10.png",
        PilotUnlockType.AtlasMapReturns,
        1,
        "Return safely from each current map with cargo worth at least 1000 Astrons more than at round start.",
        new[]
        {
            "When round-gained cargo is worth at least 2000 Astrons, booster drains 30% slower and extraction zones activate 30% faster.",
            "When cargo is full, collecting a more valuable round-gained item automatically drops the least valuable round-gained cargo item. Starting cargo is never auto-dropped.",
            "Shield-slot equipment grants 30% more maximum shield."
        },
        "Suppressor Wave",
        "Emit a wide suppression wave that slows all computer-controlled enemies and enemy players for 5 seconds. Affected targets move slower, fire slower, reload slower and their projectiles travel slower.");

    static readonly PilotDefinition[] Definitions =
    {
        JakeDefinition,
        NovaDefinition,
        RoburDefinition,
        SirNowitzkyDefinition,
        RobyDefinition,
        CharlieSmartDefinition,
        CovaxDefinition,
        AshDefinition,
        VectorDefinition,
        AtlasDefinition
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

        if (definition.UnlockType == PilotUnlockType.AsteroidSalvage)
        {
            int asteroidSalvage = profile != null ? profile.PilotAsteroidSalvageCount : 0;
            return asteroidSalvage >= definition.RequiredLevel;
        }

        if (definition.UnlockType == PilotUnlockType.OverloadReturns)
        {
            int overloadReturns = profile != null ? profile.PilotAshOverloadReturns : 0;
            return overloadReturns >= definition.RequiredLevel;
        }

        if (definition.UnlockType == PilotUnlockType.CompletedProjects)
        {
            int completedProjects = profile != null ? ProjectCatalog.CountCompletedProjects(profile.ProjectProgress) : 0;
            return completedProjects >= definition.RequiredLevel;
        }

        if (definition.UnlockType == PilotUnlockType.AtlasMapReturns)
            return CountAtlasMapReturns(profile) >= GetAtlasRequiredMapReturnCount();

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

        if (definition.UnlockType == PilotUnlockType.AsteroidSalvage)
        {
            int progress = Mathf.Clamp(profile != null ? profile.PilotAsteroidSalvageCount : 0, 0, definition.RequiredLevel);
            return "Collect " + definition.RequiredLevel + " asteroids (" + progress + "/" + definition.RequiredLevel + ").";
        }

        if (definition.UnlockType == PilotUnlockType.OverloadReturns)
        {
            int progress = Mathf.Clamp(profile != null ? profile.PilotAshOverloadReturns : 0, 0, definition.RequiredLevel);
            return "Complete " + definition.RequiredLevel + " Overload Returns (" + progress + "/" + definition.RequiredLevel + ").\n" +
                   "Each return needs full cargo, 1500 Astrons in cargo, 3 computer enemy kills and 20 seconds of booster use.";
        }

        if (definition.UnlockType == PilotUnlockType.CompletedProjects)
        {
            int progress = Mathf.Clamp(profile != null ? ProjectCatalog.CountCompletedProjects(profile.ProjectProgress) : 0, 0, definition.RequiredLevel);
            return "Complete any " + definition.RequiredLevel + " projects (" + progress + "/" + definition.RequiredLevel + ").";
        }

        if (definition.UnlockType == PilotUnlockType.AtlasMapReturns)
        {
            int required = GetAtlasRequiredMapReturnCount();
            int progress = Mathf.Clamp(CountAtlasMapReturns(profile), 0, required);
            return "Return safely from each current map with cargo worth at least " + AtlasRequiredNetCargoAstrons +
                   " Astrons more than at round start (" + progress + "/" + required + ").";
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

    public static int GetAtlasRequiredMapReturnCount()
    {
        return LobbyMapCatalog.AllMaps != null ? LobbyMapCatalog.AllMaps.Count : 0;
    }

    public static int CountAtlasMapReturns(PlayerProfileData profile)
    {
        return NormalizeAtlasMapReturnIds(profile != null ? profile.PilotAtlasMapReturns : null).Length;
    }

    public static bool IsAtlasMapReturnRecorded(PlayerProfileData profile, string mapId)
    {
        string normalized = NormalizeAtlasMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        string[] completed = NormalizeAtlasMapReturnIds(profile != null ? profile.PilotAtlasMapReturns : null);
        for (int i = 0; i < completed.Length; i++)
        {
            if (string.Equals(completed[i], normalized, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static string NormalizeAtlasMapId(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId) || LobbyMapCatalog.AllMaps == null)
            return string.Empty;

        string normalized = mapId.Trim();
        for (int i = 0; i < LobbyMapCatalog.AllMaps.Count; i++)
        {
            LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
            if (map != null && string.Equals(map.Id, normalized, StringComparison.Ordinal))
                return map.Id;
        }

        return string.Empty;
    }

    public static string[] NormalizeAtlasMapReturnIds(string[] source)
    {
        if (LobbyMapCatalog.AllMaps == null || LobbyMapCatalog.AllMaps.Count == 0)
            return Array.Empty<string>();

        HashSet<string> sourceSet = new HashSet<string>(StringComparer.Ordinal);
        if (source != null)
        {
            for (int i = 0; i < source.Length; i++)
            {
                string normalized = NormalizeAtlasMapId(source[i]);
                if (!string.IsNullOrWhiteSpace(normalized))
                    sourceSet.Add(normalized);
            }
        }

        List<string> ordered = new List<string>();
        for (int i = 0; i < LobbyMapCatalog.AllMaps.Count; i++)
        {
            LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
            if (map != null && sourceSet.Contains(map.Id))
                ordered.Add(map.Id);
        }

        return ordered.ToArray();
    }
}
