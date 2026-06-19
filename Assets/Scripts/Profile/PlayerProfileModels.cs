using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShipUnlockSnapshot
{
    public string[] shipIds;
}

[Serializable]
public class AvengerTheftAttemptData
{
    public bool Active;
    public int RefundAstrons;
    public int OriginalShipSkinIndex;

    public static AvengerTheftAttemptData Empty()
    {
        return new AvengerTheftAttemptData
        {
            Active = false,
            RefundAstrons = 0,
            OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex
        };
    }
}

public enum ViperRecoveryStage
{
    Locked = 0,
    WreckRecovered = 1,
    Testing = 2,
    Complete = 3
}

public enum ViperTestFlightResult
{
    NotApplicable = 0,
    TooShort = 1,
    SubsystemUnlocked = 2,
    Complete = 3
}

public enum ArrowLicenseStage
{
    Locked = 0,
    Qualifying = 1,
    TokenCollectionRequired = 2,
    PartsRequired = TokenCollectionRequired,
    MapRacesRequired = 3,
    TimeTrialRequired = MapRacesRequired,
    GhostRaceRequired = 4,
    FinalRunReady = 5,
    Complete = 6
}

public enum ArrowTimeTrialRank
{
    None = 0,
    C = 1,
    B = 2,
    A = 3,
    S = 4
}

public enum PathfinderResearchStage
{
    Locked = 0,
    CollectingData = 1,
    DocumentationReady = 2,
    ResourcesRequired = 3,
    FinalVisitRequired = 4,
    Complete = 5
}

public enum PathfinderResearchStationStatus
{
    None = 0,
    DocumentationDelivered = 1,
    ValuableItemsDelivered = 2,
    DifferentMapRequired = 3,
    Finalized = 4
}

[Serializable]
public class ViperRecoveryProgressData
{
    public int Stage;
    public string[] UnlockedSubsystemIds;

    public static ViperRecoveryProgressData Empty()
    {
        return new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.Locked,
            UnlockedSubsystemIds = Array.Empty<string>()
        };
    }

    public static ViperRecoveryProgressData Complete()
    {
        return new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.Complete,
            UnlockedSubsystemIds = PlayerProfileService.GetAllViperTestSubsystemIds()
        };
    }

    public ViperRecoveryProgressData Clone()
    {
        return new ViperRecoveryProgressData
        {
            Stage = Stage,
            UnlockedSubsystemIds = UnlockedSubsystemIds != null ? (string[])UnlockedSubsystemIds.Clone() : Array.Empty<string>()
        };
    }
}

[Serializable]
public class ArrowLicenseProgressData
{
    public int Stage;
    public int QualifierChips;
    public bool IonNozzleDelivered;
    public bool GyroStabilizerDelivered;
    public bool RaceTransponderDelivered;
    public int BestTimeTrialRank;
    public bool GhostRaceWon;
    public string[] CompletedRaceMapIds;
    public string ActiveRaceMapId;
    public bool FinalRunEntryAvailable;
    public bool FinalRunActive;
    public int OriginalShipSkinIndex;

    public static ArrowLicenseProgressData Empty()
    {
        return new ArrowLicenseProgressData
        {
            Stage = (int)ArrowLicenseStage.Locked,
            QualifierChips = 0,
            IonNozzleDelivered = false,
            GyroStabilizerDelivered = false,
            RaceTransponderDelivered = false,
            BestTimeTrialRank = (int)ArrowTimeTrialRank.None,
            GhostRaceWon = false,
            CompletedRaceMapIds = Array.Empty<string>(),
            ActiveRaceMapId = string.Empty,
            FinalRunEntryAvailable = false,
            FinalRunActive = false,
            OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex
        };
    }

    public static ArrowLicenseProgressData Complete()
    {
        return new ArrowLicenseProgressData
        {
            Stage = (int)ArrowLicenseStage.Complete,
            QualifierChips = PlayerProfileService.ArrowQualifierChipsRequired,
            IonNozzleDelivered = true,
            GyroStabilizerDelivered = true,
            RaceTransponderDelivered = true,
            BestTimeTrialRank = (int)ArrowTimeTrialRank.B,
            GhostRaceWon = true,
            CompletedRaceMapIds = new[]
            {
                LobbyMapCatalog.MinefieldMapId,
                LobbyMapCatalog.SnowFieldMapId,
                LobbyMapCatalog.DeepSpaceMapId,
                LobbyMapCatalog.PirateBayMapId
            },
            ActiveRaceMapId = string.Empty,
            FinalRunEntryAvailable = false,
            FinalRunActive = false,
            OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex
        };
    }

    public ArrowLicenseProgressData Clone()
    {
        return new ArrowLicenseProgressData
        {
            Stage = Stage,
            QualifierChips = QualifierChips,
            IonNozzleDelivered = IonNozzleDelivered,
            GyroStabilizerDelivered = GyroStabilizerDelivered,
            RaceTransponderDelivered = RaceTransponderDelivered,
            BestTimeTrialRank = BestTimeTrialRank,
            GhostRaceWon = GhostRaceWon,
            CompletedRaceMapIds = CompletedRaceMapIds != null ? (string[])CompletedRaceMapIds.Clone() : Array.Empty<string>(),
            ActiveRaceMapId = ActiveRaceMapId,
            FinalRunEntryAvailable = FinalRunEntryAvailable,
            FinalRunActive = FinalRunActive,
            OriginalShipSkinIndex = OriginalShipSkinIndex
        };
    }
}

[Serializable]
public class PathfinderResearchProgressData
{
    public int Stage;
    public string[] HackedShipTypeIds;
    public int DeliveredValuableItems;
    public string ResourceCompletionMapId;

    public static PathfinderResearchProgressData Empty()
    {
        return new PathfinderResearchProgressData
        {
            Stage = (int)PathfinderResearchStage.Locked,
            HackedShipTypeIds = Array.Empty<string>(),
            DeliveredValuableItems = 0,
            ResourceCompletionMapId = string.Empty
        };
    }

    public static PathfinderResearchProgressData Complete()
    {
        return new PathfinderResearchProgressData
        {
            Stage = (int)PathfinderResearchStage.Complete,
            HackedShipTypeIds = Array.Empty<string>(),
            DeliveredValuableItems = PlayerProfileService.PathfinderValuableItemsRequired,
            ResourceCompletionMapId = string.Empty
        };
    }

    public PathfinderResearchProgressData Clone()
    {
        return new PathfinderResearchProgressData
        {
            Stage = Stage,
            HackedShipTypeIds = HackedShipTypeIds != null ? (string[])HackedShipTypeIds.Clone() : Array.Empty<string>(),
            DeliveredValuableItems = DeliveredValuableItems,
            ResourceCompletionMapId = ResourceCompletionMapId
        };
    }
}

public class PathfinderHackRecordResult
{
    public bool Started;
    public bool Added;
    public bool Duplicate;
    public int Count;
    public bool DocumentationReady;
    public bool DocumentationCreated;
    public bool InventoryFull;
}

public class PathfinderResearchStationResult
{
    public PathfinderResearchStationStatus Status;
    public int DeliveredValuableItemsNow;
    public int DeliveredValuableItemsTotal;
    public bool ResourcesCompleted;
}

[Serializable]
public class PlayerProfileData
{
    public string Nickname;
    public int ShipSkinIndex;
    public int GamesPlayed;
    public int TotalXp;
    public int Astrons;
    public PlayerInventoryData Inventory;
    public string SelectedPilotId;
    public string[] UnlockedPilotIds;
    public string[] UnlockedBlueprintIds;
    public string[] UnlockedShipIds;
    public AvengerTheftAttemptData AvengerTheftAttempt;
    public ViperRecoveryProgressData ViperRecoveryProgress;
    public ArrowLicenseProgressData ArrowLicenseProgress;
    public int BisonIndustrialPartsDelivered;
    public int InvaderImprintsRecovered;
    public PathfinderResearchProgressData PathfinderResearchProgress;
    public string[] MissEnigmaPurchasedBlueprintIds;
    public int PilotDroneKills;
    public int PilotSoldItemsAstrons;
    public int PilotPirateBayReturns;
    public int PilotAsteroidSalvageCount;
    public int PilotAshOverloadReturns;
    public string[] PilotAtlasMapReturns;
    public PlayerMapUnlockProgressData MapUnlockProgress;
    public PlayerProjectProgressData ProjectProgress;

    public static PlayerProfileData Default()
    {
        return new PlayerProfileData
        {
            Nickname = "Pilot",
            ShipSkinIndex = 0,
            GamesPlayed = 0,
            TotalXp = 0,
            Astrons = PlayerProfileService.DefaultStartingAstrons,
            Inventory = PlayerInventoryData.Default(),
            SelectedPilotId = PilotCatalog.JakeId,
            UnlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds(),
            UnlockedBlueprintIds = PlayerProfileService.NormalizeUnlockedBlueprintIds(null),
            UnlockedShipIds = PlayerProfileService.NormalizeUnlockedShipIds(null),
            AvengerTheftAttempt = AvengerTheftAttemptData.Empty(),
            ViperRecoveryProgress = ViperRecoveryProgressData.Empty(),
            ArrowLicenseProgress = ArrowLicenseProgressData.Empty(),
            BisonIndustrialPartsDelivered = 0,
            InvaderImprintsRecovered = 0,
            PathfinderResearchProgress = PathfinderResearchProgressData.Empty(),
            MissEnigmaPurchasedBlueprintIds = Array.Empty<string>(),
            PilotDroneKills = 0,
            PilotSoldItemsAstrons = 0,
            PilotPirateBayReturns = 0,
            PilotAsteroidSalvageCount = 0,
            PilotAshOverloadReturns = 0,
            PilotAtlasMapReturns = Array.Empty<string>(),
            MapUnlockProgress = PlayerProfileService.NormalizeMapUnlockProgress(null),
            ProjectProgress = ProjectCatalog.NormalizeProgress(null)
        };
    }
}

[Serializable]
public class PlayerMapUnlockProgressData
{
    public PlayerMapReturnCountEntry[] ReturnCounts;
    public bool MothershipKilled;
    public bool CheatUnlockAllMaps;

    public PlayerMapUnlockProgressData Clone()
    {
        PlayerMapReturnCountEntry[] clonedCounts = ReturnCounts != null
            ? new PlayerMapReturnCountEntry[ReturnCounts.Length]
            : Array.Empty<PlayerMapReturnCountEntry>();

        for (int i = 0; i < clonedCounts.Length; i++)
        {
            PlayerMapReturnCountEntry source = ReturnCounts[i];
            clonedCounts[i] = source != null
                ? new PlayerMapReturnCountEntry { MapId = source.MapId, Count = source.Count }
                : null;
        }

        return new PlayerMapUnlockProgressData
        {
            ReturnCounts = clonedCounts,
            MothershipKilled = MothershipKilled,
            CheatUnlockAllMaps = CheatUnlockAllMaps
        };
    }

    public void SetReturnCount(string mapId, int count)
    {
        string normalizedMapId = LobbyMapUnlockCatalog.NormalizeMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
            return;

        count = Math.Max(0, count);
        List<PlayerMapReturnCountEntry> entries = ReturnCounts != null
            ? new List<PlayerMapReturnCountEntry>(ReturnCounts)
            : new List<PlayerMapReturnCountEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            PlayerMapReturnCountEntry entry = entries[i];
            if (entry == null)
                continue;

            if (!string.Equals(entry.MapId, normalizedMapId, StringComparison.Ordinal))
                continue;

            if (count <= 0)
                entries.RemoveAt(i);
            else
                entry.Count = count;
            ReturnCounts = entries.ToArray();
            return;
        }

        if (count > 0)
        {
            entries.Add(new PlayerMapReturnCountEntry
            {
                MapId = normalizedMapId,
                Count = count
            });
        }

        ReturnCounts = entries.ToArray();
    }
}

[Serializable]
public class PlayerMapReturnCountEntry
{
    public string MapId;
    public int Count;
}

[Serializable]
public class PilotUnlockSnapshot
{
    public string[] pilotIds;
}

[Serializable]
public class AtlasMapReturnSnapshot
{
    public string[] mapIds;
}

[Serializable]
public class BlueprintUnlockSnapshot
{
    public string[] blueprintIds;
}

[Serializable]
public class BlueprintPurchaseSnapshot
{
    public string[] blueprintIds;
}

[Serializable]
public class ShipInventorySnapshot
{
    public string[] slots;
}

[Serializable]
public class EquipmentSnapshot
{
    public string[] slots;
}

[Serializable]
public class PlayerInventoryData
{
    public const int DefaultPlayerSlotCount = 30;
    public const int PlayerSlotCount = DefaultPlayerSlotCount;
    public const int PlayerSlotExtensionSize = 20;
    public const int ShipSlotCount = 15;
    public const int EquipmentSlotCount = 12;
    public const int CraftingSlotCount = 4;
    public string[] PlayerSlots;
    public string[] ShipSlots;
    public string[] EquipmentSlots;
    public string[] CraftingSlots;

    public static PlayerInventoryData Default()
    {
        return new PlayerInventoryData
        {
            PlayerSlots = new string[DefaultPlayerSlotCount],
            ShipSlots = new string[ShipSlotCount],
            EquipmentSlots = new string[EquipmentSlotCount],
            CraftingSlots = new string[CraftingSlotCount]
        };
    }

    public PlayerInventoryData Clone()
    {
        Normalize();
        return new PlayerInventoryData
        {
            PlayerSlots = (string[])PlayerSlots.Clone(),
            ShipSlots = (string[])ShipSlots.Clone(),
            EquipmentSlots = (string[])EquipmentSlots.Clone(),
            CraftingSlots = (string[])CraftingSlots.Clone()
        };
    }

    public void Normalize()
    {
        int playerSlotCount = Mathf.Max(DefaultPlayerSlotCount, PlayerSlots != null ? PlayerSlots.Length : 0);
        if (PlayerSlots == null || PlayerSlots.Length != playerSlotCount)
        {
            string[] old = PlayerSlots;
            PlayerSlots = new string[playerSlotCount];
            CopyInto(old, PlayerSlots);
        }

        if (ShipSlots == null || ShipSlots.Length != ShipSlotCount)
        {
            string[] old = ShipSlots;
            ShipSlots = new string[ShipSlotCount];
            CopyInto(old, ShipSlots);
        }

        if (EquipmentSlots == null || EquipmentSlots.Length != EquipmentSlotCount)
        {
            string[] old = EquipmentSlots;
            EquipmentSlots = new string[EquipmentSlotCount];
            CopyInto(old, EquipmentSlots);
        }

        if (CraftingSlots == null || CraftingSlots.Length != CraftingSlotCount)
        {
            string[] old = CraftingSlots;
            CraftingSlots = new string[CraftingSlotCount];
            CopyInto(old, CraftingSlots);
        }

        NormalizeLegacyAlienSecretSlots(PlayerSlots);
        NormalizeLegacyAlienSecretSlots(ShipSlots);
        NormalizeLegacyAlienSecretSlots(CraftingSlots);
    }

    public int GetFirstEmptyPlayerSlot()
    {
        Normalize();
        return FindFirstEmpty(PlayerSlots);
    }

    public int GetFirstEmptyShipSlot()
    {
        Normalize();
        return FindFirstEmpty(ShipSlots);
    }

    public int GetFirstEmptyShipSlot(int capacity)
    {
        Normalize();
        return FindFirstEmpty(ShipSlots, capacity);
    }

    public int GetFirstEmptyShipSlot(int capacity, int shipSkinIndex, string itemId)
    {
        Normalize();
        int clampedCapacity = Mathf.Clamp(capacity, 0, ShipSlots.Length);
        for (int i = 0; i < clampedCapacity; i++)
        {
            if (!string.IsNullOrWhiteSpace(ShipSlots[i]))
                continue;

            if (PlayerProfileService.CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                return i;
        }

        return -1;
    }

    public bool TryAddToPlayer(string itemId)
    {
        Normalize();
        int slot = GetFirstEmptyPlayerSlot();
        if (slot < 0)
            return false;

        PlayerSlots[slot] = itemId;
        return true;
    }

    public bool TryAddToShip(string itemId)
    {
        Normalize();
        int slot = GetFirstEmptyShipSlot();
        if (slot < 0)
            return false;

        ShipSlots[slot] = itemId;
        return true;
    }

    public bool TryAddToShip(string itemId, int capacity)
    {
        Normalize();
        int slot = GetFirstEmptyShipSlot(capacity);
        if (slot < 0)
            return false;

        ShipSlots[slot] = itemId;
        return true;
    }

    public bool TryAddToShip(string itemId, int capacity, int shipSkinIndex)
    {
        Normalize();
        int slot = GetFirstEmptyShipSlot(capacity, shipSkinIndex, itemId);
        if (slot < 0)
            return false;

        ShipSlots[slot] = itemId;
        return true;
    }

    public string RemoveFromPlayer(int index)
    {
        Normalize();
        if (index < 0 || index >= PlayerSlots.Length)
            return null;

        string item = PlayerSlots[index];
        PlayerSlots[index] = null;
        return item;
    }

    public string RemoveFromShip(int index)
    {
        Normalize();
        if (index < 0 || index >= ShipSlots.Length)
            return null;

        string item = ShipSlots[index];
        ShipSlots[index] = null;
        return item;
    }

    public string RemoveFromEquipment(int index)
    {
        Normalize();
        if (index < 0 || index >= EquipmentSlots.Length)
            return null;

        string item = EquipmentSlots[index];
        EquipmentSlots[index] = null;
        return item;
    }

    public string RemoveFromCrafting(int index)
    {
        Normalize();
        if (index < 0 || index >= CraftingSlots.Length)
            return null;

        string item = CraftingSlots[index];
        CraftingSlots[index] = null;
        return item;
    }

    public void RestorePlayer(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < PlayerSlots.Length)
            PlayerSlots[index] = itemId;
    }

    public void RestoreShip(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < ShipSlots.Length)
            ShipSlots[index] = itemId;
    }

    public void SetEquipment(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < EquipmentSlots.Length)
            EquipmentSlots[index] = itemId;
    }

    public void SetCrafting(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < CraftingSlots.Length)
            CraftingSlots[index] = itemId;
    }

    public bool IsEquipmentSlotEnabled(int slotIndex, int shipSkinIndex)
    {
        Normalize();
        if (slotIndex < 0 || slotIndex >= EquipmentSlots.Length)
            return false;

        return ShipCatalog.IsEquipmentSlotEnabled(slotIndex, shipSkinIndex);
    }

    public void SetShipSlots(string[] source)
    {
        Normalize();
        ShipSlots = new string[ShipSlotCount];
        CopyInto(source, ShipSlots);
    }

    public void ExtendPlayerSlots(int extraSlots)
    {
        Normalize();
        int safeExtraSlots = Mathf.Max(0, extraSlots);
        if (safeExtraSlots == 0)
            return;

        string[] old = PlayerSlots;
        PlayerSlots = new string[old.Length + safeExtraSlots];
        CopyInto(old, PlayerSlots);
    }

    static int FindFirstEmpty(string[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }

    static int FindFirstEmpty(string[] slots, int capacity)
    {
        int safeCapacity = Mathf.Clamp(capacity, 0, slots != null ? slots.Length : 0);
        for (int i = 0; i < safeCapacity; i++)
        {
            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }

    static void CopyInto(string[] source, string[] destination)
    {
        if (source == null)
            return;

        int count = Math.Min(source.Length, destination.Length);
        for (int i = 0; i < count; i++)
        {
            destination[i] = source[i];
        }
    }

    static void NormalizeLegacyAlienSecretSlots(string[] slots)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (string.Equals(slots[i], InventoryItemCatalog.AlienSecretId, StringComparison.Ordinal))
                slots[i] = InventoryItemCatalog.GetAlienSecretItemId(i);
        }
    }
}
