using System;
using UnityEngine;

public sealed class BlueprintTradeOffer
{
    public string BlueprintItemId;
    public string[] CostItemIds;
}

sealed class BlueprintDropCandidate
{
    public string TargetItemId;
    public float Chance;
}

sealed class WreckBlueprintDropRule
{
    public string WreckItemId;
    public BlueprintDropCandidate[] Candidates;
}

public static class BlueprintCatalog
{
    const float ContainerBlueprintDropChanceMultiplier = 2f;
    const float ContainerBlueprintDropChanceCap = 0.08f;

    static readonly string[] StarterUnlockedBlueprintTargetItemIds =
    {
        InventoryItemCatalog.TripleGunId,
        InventoryItemCatalog.BatteryId,
        InventoryItemCatalog.ShieldReactorId,
        InventoryItemCatalog.EmergencySuitBeaconId,
        InventoryItemCatalog.KineticDampenerId,
        InventoryItemCatalog.PowerEngineId,
        InventoryItemCatalog.MagneticBeamId,
        InventoryItemCatalog.CargoBayExtensionId
    };

    static readonly BlueprintTradeOffer[] MissEnigmaOffers =
    {
        Offer(InventoryItemCatalog.RailGunId, InventoryItemCatalog.PirateBaseCoreId),
        Offer(InventoryItemCatalog.PhaseShieldId, InventoryItemCatalog.SpaceAnimalRemainsId, InventoryItemCatalog.SpaceAnimalRemainsId),
        Offer(InventoryItemCatalog.EscapePodId, InventoryItemCatalog.RescueShipSalvageId, InventoryItemCatalog.CashSuitcaseId),
        Offer(InventoryItemCatalog.SalvageMagnetArrayId, InventoryItemCatalog.CashSuitcaseId, InventoryItemCatalog.RadarShipSalvageId),
        Offer(InventoryItemCatalog.PulseDisruptorId, InventoryItemCatalog.SpaceAnimalRemainsId, InventoryItemCatalog.PirateFighterSalvageId),
        Offer(InventoryItemCatalog.StrongPlatingId, InventoryItemCatalog.PlatinumChunkId),
        Offer(InventoryItemCatalog.AlienAegisCoreId, InventoryItemCatalog.SpaceAnimalRemainsId, InventoryItemCatalog.AsteroidEpicId),
        Offer(InventoryItemCatalog.TractorBeamId, InventoryItemCatalog.PlatinumChunkId),
        Offer(InventoryItemCatalog.AfterburnerStabilizerId, InventoryItemCatalog.PlatinumChunkId),
        Offer(InventoryItemCatalog.SpaceBombId, InventoryItemCatalog.PirateBaseCoreId, InventoryItemCatalog.SpaceMineWreckId),
        ScrapOffer(InventoryItemCatalog.ShieldCapacitorId, 2),
        ScrapOffer(InventoryItemCatalog.AegisBatteryId, 3),
        ScrapOffer(InventoryItemCatalog.RegenerativeShieldMatrixId, 3),
        ScrapOffer(InventoryItemCatalog.BulwarkProjectorId, 4),
        ScrapOffer(InventoryItemCatalog.AutoTurretId, 3),
        ScrapOffer(InventoryItemCatalog.RocketAutoTurretId, 5),
        ScrapOffer(InventoryItemCatalog.FiringFriendId, 4),
        ScrapOffer(InventoryItemCatalog.DropbotId, 5),
        ScrapOffer(InventoryItemCatalog.SpaceDrillId, 3),
        ScrapOffer(InventoryItemCatalog.TreasureScannerId, 4),
        ScrapOffer(InventoryItemCatalog.ShortScannerId, 5),
        ScrapOffer(InventoryItemCatalog.CloakDeviceId, 5),
        ScrapOffer(InventoryItemCatalog.GuidanceSystemId, 5),
        ScrapOffer(InventoryItemCatalog.DoubleEngineId, 6)
    };

    static readonly string[] ContainerBlueprintTargetItemIds =
    {
        InventoryItemCatalog.TripleGunId,
        InventoryItemCatalog.ArtilleryGunId,
        InventoryItemCatalog.PowerEngineId,
        InventoryItemCatalog.FuelTankId,
        InventoryItemCatalog.GadgetMineId,
        InventoryItemCatalog.BatteryId
    };

    public static string[] GetStarterUnlockedBlueprintItemIds()
    {
        string[] blueprintItemIds = new string[StarterUnlockedBlueprintTargetItemIds.Length];
        for (int i = 0; i < StarterUnlockedBlueprintTargetItemIds.Length; i++)
            blueprintItemIds[i] = InventoryItemCatalog.GetBlueprintItemId(StarterUnlockedBlueprintTargetItemIds[i]);

        return blueprintItemIds;
    }

    public static bool IsStarterUnlockedBlueprint(string blueprintItemId)
    {
        if (!InventoryItemCatalog.IsBlueprintItem(blueprintItemId))
            return false;

        for (int i = 0; i < StarterUnlockedBlueprintTargetItemIds.Length; i++)
        {
            string starterBlueprintItemId = InventoryItemCatalog.GetBlueprintItemId(StarterUnlockedBlueprintTargetItemIds[i]);
            if (string.Equals(starterBlueprintItemId, blueprintItemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static readonly WreckBlueprintDropRule[] WreckBlueprintDropRules =
    {
        WreckRule(InventoryItemCatalog.DroidScrapId,
            Drop(InventoryItemCatalog.EmergencySuitBeaconId, 0.012f),
            Drop(InventoryItemCatalog.ShieldCapacitorId, 0.008f),
            Drop(InventoryItemCatalog.LureBeaconId, 0.0075f),
            Drop(InventoryItemCatalog.MagneticBeamId, 0.006f),
            Drop(InventoryItemCatalog.PowerEngineId, 0.006f),
            Drop(InventoryItemCatalog.SpaceDrillId, 0.0055f),
            Drop(InventoryItemCatalog.FiringFriendId, 0.003f),
            Drop(InventoryItemCatalog.FusionEngineId, 0.003f),
            Drop(InventoryItemCatalog.LootingFriendId, 0.002f)),
        WreckRule(InventoryItemCatalog.SpaceMineWreckId,
            Drop(InventoryItemCatalog.SpaceTrapId, 0.012f),
            Drop(InventoryItemCatalog.KineticDampenerId, 0.0075f),
            Drop(InventoryItemCatalog.ShieldCapacitorId, 0.0065f),
            Drop(InventoryItemCatalog.AutoTurretId, 0.0055f),
            Drop(InventoryItemCatalog.RocketAutoTurretId, 0.0025f),
            Drop(InventoryItemCatalog.IonEngineId, 0.005f),
            Drop(InventoryItemCatalog.SpaceDrillId, 0.004f),
            Drop(InventoryItemCatalog.SpaceBombId, 0.0022f)),
        WreckRule(InventoryItemCatalog.SpaceTruckWreckId,
            Drop(InventoryItemCatalog.CargoBayExtensionId, 0.0115f),
            Drop(InventoryItemCatalog.ShieldCapacitorId, 0.008f),
            Drop(InventoryItemCatalog.FusionEngineId, 0.0055f),
            Drop(InventoryItemCatalog.SuperBoosterId, 0.0045f),
            Drop(InventoryItemCatalog.DropbotId, 0.003f),
            Drop(InventoryItemCatalog.HybridEngineId, 0.0035f),
            Drop(InventoryItemCatalog.StrongPlatingId, 0.0035f)),
        WreckRule(InventoryItemCatalog.ContainerShipWreckId,
            Drop(InventoryItemCatalog.CargoBayExtensionId, 0.0105f),
            Drop(InventoryItemCatalog.SalvageMagnetArrayId, 0.0065f),
            Drop(InventoryItemCatalog.BulwarkProjectorId, 0.0055f),
            Drop(InventoryItemCatalog.StrongPlatingId, 0.0045f),
            Drop(InventoryItemCatalog.DropbotId, 0.0032f),
            Drop(InventoryItemCatalog.SuperBoosterId, 0.0035f)),
        WreckRule(InventoryItemCatalog.NeutralFighterSalvageId,
            Drop(InventoryItemCatalog.GatlingGunId, 0.0095f),
            Drop(InventoryItemCatalog.DoubleIonizerId, 0.0085f),
            Drop(InventoryItemCatalog.RocketLauncherId, 0.0075f),
            Drop(InventoryItemCatalog.AutoTurretId, 0.006f),
            Drop(InventoryItemCatalog.RocketAutoTurretId, 0.003f),
            Drop(InventoryItemCatalog.FiringFriendId, 0.0048f),
            Drop(InventoryItemCatalog.KineticDampenerId, 0.005f)),
        WreckRule(InventoryItemCatalog.RadarShipSalvageId,
            Drop(InventoryItemCatalog.MagneticBeamId, 0.0085f),
            Drop(InventoryItemCatalog.RegenerativeShieldMatrixId, 0.006f),
            Drop(InventoryItemCatalog.DoubleIonizerId, 0.0055f),
            Drop(InventoryItemCatalog.AstroCutterId, 0.005f),
            Drop(InventoryItemCatalog.TreasureScannerId, 0.0048f),
            Drop(InventoryItemCatalog.ShortScannerId, 0.0045f),
            Drop(InventoryItemCatalog.CloakDeviceId, 0.0032f),
            Drop(InventoryItemCatalog.IonEngineId, 0.0045f),
            Drop(InventoryItemCatalog.GuidanceSystemId, 0.004f),
            Drop(InventoryItemCatalog.StrongPlatingId, 0.0035f)),
        WreckRule(InventoryItemCatalog.RescueShipSalvageId,
            Drop(InventoryItemCatalog.EmergencySuitBeaconId, 0.011f),
            Drop(InventoryItemCatalog.CargoBayExtensionId, 0.0075f),
            Drop(InventoryItemCatalog.AegisBatteryId, 0.006f),
            Drop(InventoryItemCatalog.LureBeaconId, 0.0065f),
            Drop(InventoryItemCatalog.StrongPlatingId, 0.004f),
            Drop(InventoryItemCatalog.DropbotId, 0.0038f),
            Drop(InventoryItemCatalog.HybridEngineId, 0.0032f),
            Drop(InventoryItemCatalog.LootingFriendId, 0.003f)),
        WreckRule(InventoryItemCatalog.PirateFighterSalvageId,
            Drop(InventoryItemCatalog.RocketLauncherId, 0.0095f),
            Drop(InventoryItemCatalog.SpaceTrapId, 0.0065f),
            Drop(InventoryItemCatalog.AutoTurretId, 0.0055f),
            Drop(InventoryItemCatalog.RocketAutoTurretId, 0.0026f),
            Drop(InventoryItemCatalog.CloakDeviceId, 0.0022f),
            Drop(InventoryItemCatalog.DoubleRocketLauncherId, 0.0045f),
            Drop(InventoryItemCatalog.PlasmaGunId, 0.0035f)),
        WreckRule(InventoryItemCatalog.CorsairSalvageId,
            Drop(InventoryItemCatalog.PlasmaGunId, 0.0075f),
            Drop(InventoryItemCatalog.GatlingGunId, 0.0065f),
            Drop(InventoryItemCatalog.DoubleRocketLauncherId, 0.005f),
            Drop(InventoryItemCatalog.SuperBoosterId, 0.0045f),
            Drop(InventoryItemCatalog.DoubleEngineId, 0.003f)),
        WreckRule(InventoryItemCatalog.HunterLanceCoreId,
            Drop(InventoryItemCatalog.AstroCutterId, 0.0085f),
            Drop(InventoryItemCatalog.GuidanceSystemId, 0.007f),
            Drop(InventoryItemCatalog.PlasmaGunId, 0.0045f),
            Drop(InventoryItemCatalog.DoubleRocketLauncherId, 0.0045f)),
        WreckRule(InventoryItemCatalog.PirateBaseCoreId,
            Drop(InventoryItemCatalog.DoubleRocketLauncherId, 0.007f),
            Drop(InventoryItemCatalog.GuidanceSystemId, 0.0065f),
            Drop(InventoryItemCatalog.PlasmaGunId, 0.006f),
            Drop(InventoryItemCatalog.BulwarkProjectorId, 0.006f),
            Drop(InventoryItemCatalog.StrongPlatingId, 0.0055f),
            Drop(InventoryItemCatalog.SuperBoosterId, 0.005f),
            Drop(InventoryItemCatalog.DoubleEngineId, 0.004f),
            Drop(InventoryItemCatalog.SpaceBombId, 0.0038f)),
        WreckRule(InventoryItemCatalog.MothershipCoreId,
            Drop(InventoryItemCatalog.GuidanceSystemId, 0.0075f),
            Drop(InventoryItemCatalog.AlienAegisCoreId, 0.007f),
            Drop(InventoryItemCatalog.StrongPlatingId, 0.0065f),
            Drop(InventoryItemCatalog.AstroCutterId, 0.006f),
            Drop(InventoryItemCatalog.SuperBoosterId, 0.006f),
            Drop(InventoryItemCatalog.DoubleEngineId, 0.0045f),
            Drop(InventoryItemCatalog.LootingFriendId, 0.005f),
            Drop(InventoryItemCatalog.FiringFriendId, 0.0048f),
            Drop(InventoryItemCatalog.DropbotId, 0.0045f),
            Drop(InventoryItemCatalog.CloakDeviceId, 0.0038f),
            Drop(InventoryItemCatalog.SpaceBombId, 0.0042f))
    };

    public static BlueprintTradeOffer[] GetMissEnigmaOffers()
    {
        BlueprintTradeOffer[] offers = new BlueprintTradeOffer[MissEnigmaOffers.Length];
        for (int i = 0; i < MissEnigmaOffers.Length; i++)
        {
            BlueprintTradeOffer source = MissEnigmaOffers[i];
            offers[i] = new BlueprintTradeOffer
            {
                BlueprintItemId = source.BlueprintItemId,
                CostItemIds = source.CostItemIds != null ? (string[])source.CostItemIds.Clone() : Array.Empty<string>()
            };
        }

        return offers;
    }

    public static BlueprintTradeOffer GetMissEnigmaOffer(string blueprintItemId)
    {
        if (string.IsNullOrWhiteSpace(blueprintItemId))
            return null;

        for (int i = 0; i < MissEnigmaOffers.Length; i++)
        {
            BlueprintTradeOffer offer = MissEnigmaOffers[i];
            if (offer != null && string.Equals(offer.BlueprintItemId, blueprintItemId, StringComparison.Ordinal))
            {
                return new BlueprintTradeOffer
                {
                    BlueprintItemId = offer.BlueprintItemId,
                    CostItemIds = offer.CostItemIds != null ? (string[])offer.CostItemIds.Clone() : Array.Empty<string>()
                };
            }
        }

        return null;
    }

    public static bool TryRollContainerBlueprintDrop(string itemId, out string blueprintItemId)
    {
        return TryRollContainerBlueprintDrop(itemId, null, out blueprintItemId);
    }

    public static bool TryRollContainerBlueprintDrop(string itemId, string[] unlockedBlueprintIds, out string blueprintItemId)
    {
        blueprintItemId = string.Empty;
        if (!InventoryItemCatalog.IsContainerItem(itemId) || ContainerBlueprintTargetItemIds.Length == 0)
            return false;

        if (UnityEngine.Random.value >= GetContainerBlueprintDropChance())
            return false;

        string targetItemId = RollWeightedBlueprintTargetItemId(ContainerBlueprintTargetItemIds, unlockedBlueprintIds);
        if (string.IsNullOrWhiteSpace(targetItemId))
            return false;

        blueprintItemId = InventoryItemCatalog.GetBlueprintItemId(targetItemId);
        return InventoryItemCatalog.IsBlueprintItem(blueprintItemId);
    }

    public static bool TryRollWreckBlueprintDrop(string wreckItemId, int sourceEnemyKindValue, out string blueprintItemId)
    {
        return TryRollWreckBlueprintDrop(wreckItemId, sourceEnemyKindValue, null, out blueprintItemId);
    }

    public static bool TryRollWreckBlueprintDrop(string wreckItemId, int sourceEnemyKindValue, string[] unlockedBlueprintIds, out string blueprintItemId)
    {
        blueprintItemId = string.Empty;
        WreckBlueprintDropRule rule = GetWreckBlueprintDropRule(wreckItemId);
        if (rule == null || rule.Candidates == null || rule.Candidates.Length == 0)
            return false;

        EnemyBotKind sourceEnemyKind = ResolveWreckSourceEnemyKind(wreckItemId, sourceEnemyKindValue);
        float multiplier = GetRoomDifficultyMultiplier(sourceEnemyKind) * GetEnemyWreckBlueprintMultiplier(sourceEnemyKind);
        float totalChance = Mathf.Min(GetWreckBlueprintChance(rule, unlockedBlueprintIds) * multiplier, GetEnemyWreckBlueprintChanceCap(sourceEnemyKind));
        if (totalChance <= 0f || UnityEngine.Random.value >= totalChance)
            return false;

        string targetItemId = RollWreckBlueprintTargetItemId(rule, unlockedBlueprintIds);
        if (string.IsNullOrWhiteSpace(targetItemId))
            return false;

        blueprintItemId = InventoryItemCatalog.GetBlueprintItemId(targetItemId);
        return InventoryItemCatalog.IsBlueprintItem(blueprintItemId);
    }

    public static string ResolveContainerBlueprintDrop(string itemId)
    {
        return ResolveContainerBlueprintDrop(itemId, null);
    }

    public static string ResolveContainerBlueprintDrop(string itemId, string[] unlockedBlueprintIds)
    {
        if (InventoryItemCatalog.IsBlueprintScrapContainerItem(itemId))
            return InventoryItemCatalog.BlueprintScrapId;

        return TryRollContainerBlueprintDrop(itemId, unlockedBlueprintIds, out string blueprintItemId)
            ? blueprintItemId
            : itemId;
    }

    public static string RollScienceStationBlueprint(int scrapCount)
    {
        return RollScienceStationBlueprint(scrapCount, null);
    }

    public static string RollScienceStationBlueprint(int scrapCount, string[] unlockedBlueprintIds)
    {
        string[] blueprintItemIds = InventoryItemCatalog.GetAllBlueprintItemIds();
        if (blueprintItemIds == null || blueprintItemIds.Length == 0)
            return string.Empty;

        int effectiveScrapCount = Mathf.Max(1, scrapCount);
        float totalWeight = 0f;
        for (int i = 0; i < blueprintItemIds.Length; i++)
            totalWeight += IsBlueprintRollAvailable(blueprintItemIds[i], unlockedBlueprintIds)
                ? GetScienceStationBlueprintWeight(blueprintItemIds[i], effectiveScrapCount)
                : 0f;

        if (totalWeight <= 0f && unlockedBlueprintIds != null && unlockedBlueprintIds.Length > 0)
            return RollScienceStationBlueprint(scrapCount, null);

        if (totalWeight <= 0f)
            return string.Empty;

        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < blueprintItemIds.Length; i++)
        {
            float weight = IsBlueprintRollAvailable(blueprintItemIds[i], unlockedBlueprintIds)
                ? GetScienceStationBlueprintWeight(blueprintItemIds[i], effectiveScrapCount)
                : 0f;
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll <= 0f)
                return blueprintItemIds[i];
        }

        return string.Empty;
    }

    public static bool RollBlueprintScrapContainerBonus()
    {
        return UnityEngine.Random.value < GetBlueprintScrapContainerBonusChance();
    }

    public static float GetBlueprintScrapContainerBonusChance()
    {
        return 0.12f;
    }

    public static float GetContainerBlueprintDropChance()
    {
        float chance = 0.01f;

        int randomLootWreckCount = RoomSettings.GetRandomLootWreckCount();
        chance += Mathf.Clamp(randomLootWreckCount, 0, 5) * 0.0025f;

        if (RoomSettings.IsInventoryLossEnabled())
            chance += 0.0025f;

        if (RoomSettings.IsEquipmentLossEnabled())
            chance += 0.005f;

        return Mathf.Clamp(chance * ContainerBlueprintDropChanceMultiplier, 0.005f, ContainerBlueprintDropChanceCap);
    }

    static WreckBlueprintDropRule GetWreckBlueprintDropRule(string wreckItemId)
    {
        if (string.IsNullOrWhiteSpace(wreckItemId))
            return null;

        for (int i = 0; i < WreckBlueprintDropRules.Length; i++)
        {
            WreckBlueprintDropRule rule = WreckBlueprintDropRules[i];
            if (rule != null && string.Equals(rule.WreckItemId, wreckItemId, StringComparison.Ordinal))
                return rule;
        }

        return null;
    }

    static float GetWreckBlueprintChance(WreckBlueprintDropRule rule, string[] unlockedBlueprintIds)
    {
        if (rule == null || rule.Candidates == null)
            return 0f;

        float chance = 0f;
        for (int i = 0; i < rule.Candidates.Length; i++)
        {
            BlueprintDropCandidate candidate = rule.Candidates[i];
            if (candidate != null && IsTargetBlueprintRollAvailable(candidate.TargetItemId, unlockedBlueprintIds))
                chance += GetBoostedWreckBlueprintCandidateChance(candidate);
        }

        return chance;
    }

    static string RollWreckBlueprintTargetItemId(WreckBlueprintDropRule rule, string[] unlockedBlueprintIds)
    {
        if (rule == null || rule.Candidates == null)
            return string.Empty;

        float totalWeight = 0f;
        for (int i = 0; i < rule.Candidates.Length; i++)
        {
            BlueprintDropCandidate candidate = rule.Candidates[i];
            if (candidate != null && IsTargetBlueprintRollAvailable(candidate.TargetItemId, unlockedBlueprintIds))
                totalWeight += GetBoostedWreckBlueprintCandidateChance(candidate);
        }

        if (totalWeight <= 0f)
            return string.Empty;

        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < rule.Candidates.Length; i++)
        {
            BlueprintDropCandidate candidate = rule.Candidates[i];
            if (candidate == null)
                continue;

            float weight = IsTargetBlueprintRollAvailable(candidate.TargetItemId, unlockedBlueprintIds)
                ? GetBoostedWreckBlueprintCandidateChance(candidate)
                : 0f;
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll <= 0f)
                return candidate.TargetItemId;
        }

        return string.Empty;
    }

    static string RollWeightedBlueprintTargetItemId(string[] targetItemIds, string[] unlockedBlueprintIds)
    {
        if (targetItemIds == null || targetItemIds.Length == 0)
            return string.Empty;

        float totalWeight = 0f;
        for (int i = 0; i < targetItemIds.Length; i++)
            totalWeight += IsTargetBlueprintRollAvailable(targetItemIds[i], unlockedBlueprintIds)
                ? GetBlueprintTargetWeight(targetItemIds[i])
                : 0f;

        if (totalWeight <= 0f)
            return string.Empty;

        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < targetItemIds.Length; i++)
        {
            float weight = IsTargetBlueprintRollAvailable(targetItemIds[i], unlockedBlueprintIds)
                ? GetBlueprintTargetWeight(targetItemIds[i])
                : 0f;
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll <= 0f)
                return targetItemIds[i];
        }

        return string.Empty;
    }

    static bool IsTargetBlueprintRollAvailable(string targetItemId, string[] unlockedBlueprintIds)
    {
        return IsBlueprintRollAvailable(InventoryItemCatalog.GetBlueprintItemId(targetItemId), unlockedBlueprintIds);
    }

    static float GetBoostedWreckBlueprintCandidateChance(BlueprintDropCandidate candidate)
    {
        if (candidate == null)
            return 0f;

        return Mathf.Max(0f, candidate.Chance) * GetWreckBlueprintFindMultiplier(candidate.TargetItemId);
    }

    static float GetWreckBlueprintFindMultiplier(string targetItemId)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(targetItemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return 1f;

        switch (definition.Rarity)
        {
            case InventoryItemRarity.Common:
            case InventoryItemRarity.Uncommon:
            case InventoryItemRarity.Rare:
                return 2f;
            case InventoryItemRarity.VeryRare:
                return 1.7f;
            case InventoryItemRarity.Epic:
                return 1.35f;
            case InventoryItemRarity.Legendary:
                return 1.15f;
            default:
                return 1f;
        }
    }

    static bool IsBlueprintRollAvailable(string blueprintItemId, string[] unlockedBlueprintIds)
    {
        if (!InventoryItemCatalog.IsBlueprintItem(blueprintItemId) || IsStarterUnlockedBlueprint(blueprintItemId))
            return false;

        if (unlockedBlueprintIds == null)
            return true;

        for (int i = 0; i < unlockedBlueprintIds.Length; i++)
        {
            if (string.Equals(unlockedBlueprintIds[i], blueprintItemId, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    static float GetBlueprintTargetWeight(string targetItemId)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(targetItemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return 0f;

        float rarityWeight;
        switch (definition.Rarity)
        {
            case InventoryItemRarity.Common:
                rarityWeight = 1.45f;
                break;
            case InventoryItemRarity.Uncommon:
                rarityWeight = 1.2f;
                break;
            case InventoryItemRarity.Rare:
                rarityWeight = 1f;
                break;
            case InventoryItemRarity.VeryRare:
                rarityWeight = 0.72f;
                break;
            case InventoryItemRarity.Epic:
                rarityWeight = 0.45f;
                break;
            case InventoryItemRarity.Legendary:
                rarityWeight = 0.18f;
                break;
            default:
                rarityWeight = 1f;
                break;
        }

        int buyValue = InventoryItemCatalog.GetShopBuyValueAstrons(targetItemId);
        if (buyValue <= 0)
            buyValue = Mathf.Max(1000, InventoryItemCatalog.GetSellValueAstrons(targetItemId) * 3);

        float priceWeight = Mathf.Clamp(5000f / Mathf.Max(1000f, buyValue), 0.45f, 1.25f);
        return Mathf.Max(0.01f, rarityWeight * priceWeight);
    }

    static float GetScienceStationBlueprintWeight(string blueprintItemId, int scrapCount)
    {
        string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(blueprintItemId);
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(targetItemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return 0f;

        int buyValue = InventoryItemCatalog.GetShopBuyValueAstrons(targetItemId);
        if (buyValue <= 0)
            buyValue = Mathf.Max(1000, InventoryItemCatalog.GetSellValueAstrons(targetItemId) * 3);

        float priceQuality = Mathf.Clamp(Mathf.Sqrt(buyValue / 5000f), 0.7f, 1.6f);
        return Mathf.Max(0.01f, GetScienceStationRarityWeight(definition.Rarity, scrapCount) * priceQuality);
    }

    static float GetScienceStationRarityWeight(InventoryItemRarity rarity, int scrapCount)
    {
        if (scrapCount <= 2)
        {
            switch (rarity)
            {
                case InventoryItemRarity.Common: return 1.4f;
                case InventoryItemRarity.Uncommon: return 1.1f;
                case InventoryItemRarity.Rare: return 0.75f;
                case InventoryItemRarity.VeryRare: return 0.3f;
                case InventoryItemRarity.Epic: return 0.08f;
                case InventoryItemRarity.Legendary: return 0.02f;
            }
        }

        if (scrapCount <= 4)
        {
            switch (rarity)
            {
                case InventoryItemRarity.Common: return 0.85f;
                case InventoryItemRarity.Uncommon: return 1.05f;
                case InventoryItemRarity.Rare: return 1.2f;
                case InventoryItemRarity.VeryRare: return 0.75f;
                case InventoryItemRarity.Epic: return 0.25f;
                case InventoryItemRarity.Legendary: return 0.06f;
            }
        }

        if (scrapCount <= 6)
        {
            switch (rarity)
            {
                case InventoryItemRarity.Common: return 0.35f;
                case InventoryItemRarity.Uncommon: return 0.7f;
                case InventoryItemRarity.Rare: return 1.25f;
                case InventoryItemRarity.VeryRare: return 1.25f;
                case InventoryItemRarity.Epic: return 0.75f;
                case InventoryItemRarity.Legendary: return 0.18f;
            }
        }

        switch (rarity)
        {
            case InventoryItemRarity.Common: return 0.14f;
            case InventoryItemRarity.Uncommon: return 0.35f;
            case InventoryItemRarity.Rare: return 0.9f;
            case InventoryItemRarity.VeryRare: return 1.25f;
            case InventoryItemRarity.Epic: return 1.45f;
            case InventoryItemRarity.Legendary: return 0.65f;
            default: return 1f;
        }
    }

    static EnemyBotKind ResolveWreckSourceEnemyKind(string wreckItemId, int sourceEnemyKindValue)
    {
        if (Enum.IsDefined(typeof(EnemyBotKind), sourceEnemyKindValue))
            return (EnemyBotKind)sourceEnemyKindValue;

        if (string.Equals(wreckItemId, InventoryItemCatalog.DroidScrapId, StringComparison.Ordinal))
            return EnemyBotKind.Drone;
        if (string.Equals(wreckItemId, InventoryItemCatalog.SpaceMineWreckId, StringComparison.Ordinal))
            return EnemyBotKind.SpaceMine;
        if (string.Equals(wreckItemId, InventoryItemCatalog.SpaceTruckWreckId, StringComparison.Ordinal))
            return EnemyBotKind.SpaceTruck;
        if (string.Equals(wreckItemId, InventoryItemCatalog.ContainerShipWreckId, StringComparison.Ordinal))
            return EnemyBotKind.ContainerShip;
        if (string.Equals(wreckItemId, InventoryItemCatalog.NeutralFighterSalvageId, StringComparison.Ordinal))
            return EnemyBotKind.NeutralFighter;
        if (string.Equals(wreckItemId, InventoryItemCatalog.RadarShipSalvageId, StringComparison.Ordinal))
            return EnemyBotKind.RadarShip;
        if (string.Equals(wreckItemId, InventoryItemCatalog.RescueShipSalvageId, StringComparison.Ordinal))
            return EnemyBotKind.RescueShip;
        if (string.Equals(wreckItemId, InventoryItemCatalog.PirateFighterSalvageId, StringComparison.Ordinal))
            return EnemyBotKind.PirateFighter;
        if (string.Equals(wreckItemId, InventoryItemCatalog.CorsairSalvageId, StringComparison.Ordinal))
            return EnemyBotKind.Corsair;
        if (string.Equals(wreckItemId, InventoryItemCatalog.HunterLanceCoreId, StringComparison.Ordinal))
            return EnemyBotKind.HunterLance;
        if (string.Equals(wreckItemId, InventoryItemCatalog.PirateBaseCoreId, StringComparison.Ordinal))
            return EnemyBotKind.PirateBase;
        if (string.Equals(wreckItemId, InventoryItemCatalog.MothershipCoreId, StringComparison.Ordinal))
            return EnemyBotKind.Mothership;

        return EnemyBotKind.Drone;
    }

    static float GetEnemyWreckBlueprintMultiplier(EnemyBotKind sourceEnemyKind)
    {
        switch (sourceEnemyKind)
        {
            case EnemyBotKind.Drone:
            case EnemyBotKind.SpaceMine:
            case EnemyBotKind.NeutralFighter:
                return 0.75f;
            case EnemyBotKind.SpaceTruck:
            case EnemyBotKind.RescueShip:
                return 1f;
            case EnemyBotKind.RadarShip:
            case EnemyBotKind.PirateFighter:
            case EnemyBotKind.Corsair:
                return 1.2f;
            case EnemyBotKind.HunterLance:
            case EnemyBotKind.PirateFighterElite:
                return 1.45f;
            case EnemyBotKind.PirateFighterAce:
            case EnemyBotKind.PirateBase:
                return 1.7f;
            case EnemyBotKind.Mothership:
                return 2.1f;
            default:
                return 1f;
        }
    }

    static float GetEnemyWreckBlueprintChanceCap(EnemyBotKind sourceEnemyKind)
    {
        switch (sourceEnemyKind)
        {
            case EnemyBotKind.Mothership:
            case EnemyBotKind.PirateBase:
                return 0.12f;
            case EnemyBotKind.PirateFighterAce:
            case EnemyBotKind.PirateFighterElite:
            case EnemyBotKind.HunterLance:
                return 0.09f;
            default:
                return 0.08f;
        }
    }

    static float GetRoomDifficultyMultiplier(EnemyBotKind sourceEnemyKind)
    {
        float multiplier = 1f;
        if (RoomSettings.IsInventoryLossEnabled())
            multiplier += 0.05f;
        if (RoomSettings.IsEquipmentLossEnabled())
            multiplier += 0.1f;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(sourceEnemyKind);
        if (definition != null)
        {
            multiplier += GetDifficultyRatioBonus(RoomSettings.GetEnemyHp(sourceEnemyKind), definition.DefaultHp, 0.18f);
            multiplier += GetDifficultyRatioBonus(RoomSettings.GetEnemyShield(sourceEnemyKind), definition.DefaultShield, 0.14f);
            multiplier += GetDifficultyRatioBonus(RoomSettings.GetEnemyDamage(sourceEnemyKind), definition.Weapon != null ? definition.Weapon.Damage : 0, 0.1f);
            multiplier += Mathf.Clamp(RoomSettings.GetEnemySpeedMultiplier(sourceEnemyKind) - definition.DefaultSpeedMultiplier, -0.25f, 0.35f) * 0.16f;
        }

        return Mathf.Clamp(multiplier, 0.85f, 1.45f);
    }

    static float GetDifficultyRatioBonus(int currentValue, int baselineValue, float scale)
    {
        if (baselineValue <= 0)
            return 0f;

        float ratio = currentValue / (float)baselineValue;
        return Mathf.Clamp(ratio - 1f, -0.3f, 0.6f) * scale;
    }

    static BlueprintTradeOffer Offer(string targetItemId, params string[] costItemIds)
    {
        return new BlueprintTradeOffer
        {
            BlueprintItemId = InventoryItemCatalog.GetBlueprintItemId(targetItemId),
            CostItemIds = costItemIds ?? Array.Empty<string>()
        };
    }

    static BlueprintTradeOffer ScrapOffer(string targetItemId, int scrapCost)
    {
        return Offer(targetItemId, RepeatItem(InventoryItemCatalog.BlueprintScrapId, scrapCost));
    }

    static string[] RepeatItem(string itemId, int count)
    {
        count = Mathf.Max(0, count);
        string[] itemIds = new string[count];
        for (int i = 0; i < itemIds.Length; i++)
            itemIds[i] = itemId;

        return itemIds;
    }

    static WreckBlueprintDropRule WreckRule(string wreckItemId, params BlueprintDropCandidate[] candidates)
    {
        return new WreckBlueprintDropRule
        {
            WreckItemId = wreckItemId,
            Candidates = candidates ?? Array.Empty<BlueprintDropCandidate>()
        };
    }

    static BlueprintDropCandidate Drop(string targetItemId, float chance)
    {
        return new BlueprintDropCandidate
        {
            TargetItemId = targetItemId,
            Chance = chance
        };
    }
}
