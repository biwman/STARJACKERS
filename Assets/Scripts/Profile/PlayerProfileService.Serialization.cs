using System;
using UnityEngine;

public partial class PlayerProfileService
{
    string SerializeInventory(PlayerInventoryData inventory)
    {
        PlayerInventoryData normalized = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        normalized.Normalize();
        return JsonUtility.ToJson(normalized);
    }

    PlayerInventoryData DeserializeInventory(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PlayerInventoryData.Default();

        try
        {
            PlayerInventoryData inventory = JsonUtility.FromJson<PlayerInventoryData>(json);
            if (inventory == null)
                return PlayerInventoryData.Default();

            inventory.Normalize();
            return inventory;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize inventory: " + ex.Message);
            return PlayerInventoryData.Default();
        }
    }

    string SerializeProjectProgress(PlayerProjectProgressData progress)
    {
        return JsonUtility.ToJson(ProjectCatalog.NormalizeProgress(progress));
    }

    PlayerProjectProgressData DeserializeProjectProgress(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ProjectCatalog.NormalizeProgress(null);

        try
        {
            PlayerProjectProgressData progress = JsonUtility.FromJson<PlayerProjectProgressData>(json);
            return ProjectCatalog.NormalizeProgress(progress);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize project progress: " + ex.Message);
            return ProjectCatalog.NormalizeProgress(null);
        }
    }

    string SerializeCareerStats(PlayerCareerStatsData stats)
    {
        return JsonUtility.ToJson(NormalizeCareerStats(stats));
    }

    PlayerCareerStatsData DeserializeCareerStats(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PlayerCareerStatsData.Empty();

        try
        {
            PlayerCareerStatsData stats = JsonUtility.FromJson<PlayerCareerStatsData>(json);
            return NormalizeCareerStats(stats);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize career stats: " + ex.Message);
            return PlayerCareerStatsData.Empty();
        }
    }

    string SerializePilotUnlocks(string[] pilotIds)
    {
        PilotUnlockSnapshot snapshot = new PilotUnlockSnapshot
        {
            pilotIds = PilotCatalog.NormalizeUnlockedPilotIds(pilotIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializePilotUnlocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PilotCatalog.GetDefaultUnlockedPilotIds();

        try
        {
            PilotUnlockSnapshot snapshot = JsonUtility.FromJson<PilotUnlockSnapshot>(json);
            return PilotCatalog.NormalizeUnlockedPilotIds(snapshot != null ? snapshot.pilotIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize pilot unlocks: " + ex.Message);
            return PilotCatalog.GetDefaultUnlockedPilotIds();
        }
    }

    string SerializeAtlasMapReturns(string[] mapIds)
    {
        AtlasMapReturnSnapshot snapshot = new AtlasMapReturnSnapshot
        {
            mapIds = PilotCatalog.NormalizeAtlasMapReturnIds(mapIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeAtlasMapReturns(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            AtlasMapReturnSnapshot snapshot = JsonUtility.FromJson<AtlasMapReturnSnapshot>(json);
            return PilotCatalog.NormalizeAtlasMapReturnIds(snapshot != null ? snapshot.mapIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Atlas map returns: " + ex.Message);
            return Array.Empty<string>();
        }
    }

    string SerializeMapUnlockProgress(PlayerMapUnlockProgressData progress)
    {
        return JsonUtility.ToJson(NormalizeMapUnlockProgress(progress));
    }

    PlayerMapUnlockProgressData DeserializeMapUnlockProgress(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NormalizeMapUnlockProgress(null);

        try
        {
            PlayerMapUnlockProgressData progress = JsonUtility.FromJson<PlayerMapUnlockProgressData>(json);
            return NormalizeMapUnlockProgress(progress);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize map unlock progress: " + ex.Message);
            return NormalizeMapUnlockProgress(null);
        }
    }

    string SerializeBlueprintUnlocks(string[] blueprintIds)
    {
        BlueprintUnlockSnapshot snapshot = new BlueprintUnlockSnapshot
        {
            blueprintIds = NormalizeUnlockedBlueprintIds(blueprintIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeBlueprintUnlocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            BlueprintUnlockSnapshot snapshot = JsonUtility.FromJson<BlueprintUnlockSnapshot>(json);
            return NormalizeUnlockedBlueprintIds(snapshot != null ? snapshot.blueprintIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize blueprint unlocks: " + ex.Message);
            return Array.Empty<string>();
        }
    }

    string SerializeShipUnlocks(string[] shipIds)
    {
        ShipUnlockSnapshot snapshot = new ShipUnlockSnapshot
        {
            shipIds = NormalizeUnlockedShipIds(shipIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeShipUnlocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ShipCatalog.GetDefaultUnlockedShipTypeIds();

        try
        {
            ShipUnlockSnapshot snapshot = JsonUtility.FromJson<ShipUnlockSnapshot>(json);
            return NormalizeUnlockedShipIds(snapshot != null ? snapshot.shipIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize ship unlocks: " + ex.Message);
            return ShipCatalog.GetDefaultUnlockedShipTypeIds();
        }
    }

    string SerializeAvengerTheftAttempt(AvengerTheftAttemptData attempt)
    {
        return JsonUtility.ToJson(NormalizeAvengerTheftAttempt(attempt));
    }

    AvengerTheftAttemptData DeserializeAvengerTheftAttempt(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return AvengerTheftAttemptData.Empty();

        try
        {
            AvengerTheftAttemptData attempt = JsonUtility.FromJson<AvengerTheftAttemptData>(json);
            return NormalizeAvengerTheftAttempt(attempt);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Avenger theft attempt: " + ex.Message);
            return AvengerTheftAttemptData.Empty();
        }
    }

    string SerializeViperRecoveryProgress(ViperRecoveryProgressData progress)
    {
        return JsonUtility.ToJson(NormalizeViperRecoveryProgress(progress, CurrentProfile != null ? CurrentProfile.UnlockedShipIds : null));
    }

    ViperRecoveryProgressData DeserializeViperRecoveryProgress(string json, string[] shipIds)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NormalizeViperRecoveryProgress(null, shipIds);

        try
        {
            ViperRecoveryProgressData progress = JsonUtility.FromJson<ViperRecoveryProgressData>(json);
            return NormalizeViperRecoveryProgress(progress, shipIds);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Viper recovery progress: " + ex.Message);
            return NormalizeViperRecoveryProgress(null, shipIds);
        }
    }

    string SerializeArrowLicenseProgress(ArrowLicenseProgressData progress)
    {
        return JsonUtility.ToJson(NormalizeArrowLicenseProgress(progress, CurrentProfile != null ? CurrentProfile.UnlockedShipIds : null));
    }

    ArrowLicenseProgressData DeserializeArrowLicenseProgress(string json, string[] shipIds)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NormalizeArrowLicenseProgress(null, shipIds);

        try
        {
            ArrowLicenseProgressData progress = JsonUtility.FromJson<ArrowLicenseProgressData>(json);
            return NormalizeArrowLicenseProgress(progress, shipIds);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Arrow license progress: " + ex.Message);
            return NormalizeArrowLicenseProgress(null, shipIds);
        }
    }

    string SerializePathfinderResearchProgress(PathfinderResearchProgressData progress)
    {
        return JsonUtility.ToJson(NormalizePathfinderResearchProgress(progress, CurrentProfile != null ? CurrentProfile.UnlockedShipIds : null));
    }

    PathfinderResearchProgressData DeserializePathfinderResearchProgress(string json, string[] shipIds)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NormalizePathfinderResearchProgress(null, shipIds);

        try
        {
            PathfinderResearchProgressData progress = JsonUtility.FromJson<PathfinderResearchProgressData>(json);
            return NormalizePathfinderResearchProgress(progress, shipIds);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Pathfinder research progress: " + ex.Message);
            return NormalizePathfinderResearchProgress(null, shipIds);
        }
    }

    string SerializeMissEnigmaBlueprintPurchases(string[] blueprintIds)
    {
        BlueprintPurchaseSnapshot snapshot = new BlueprintPurchaseSnapshot
        {
            blueprintIds = NormalizeMissEnigmaBlueprintPurchases(blueprintIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeMissEnigmaBlueprintPurchases(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            BlueprintPurchaseSnapshot snapshot = JsonUtility.FromJson<BlueprintPurchaseSnapshot>(json);
            return NormalizeMissEnigmaBlueprintPurchases(snapshot != null ? snapshot.blueprintIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Miss Enigma blueprint purchases: " + ex.Message);
            return Array.Empty<string>();
        }
    }

}
