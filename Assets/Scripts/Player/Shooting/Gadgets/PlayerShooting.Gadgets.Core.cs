using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class PlayerShooting
{
    bool TryDeployGadgetMine()
    {
        EnemyBotDefinition mineDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        if (mineDefinition == null)
            return false;

        Vector3 spawnPosition = ResolveGadgetMineSpawnPosition();
        GameObject mineObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            transform.rotation,
            0,
            new object[]
            {
                mineDefinition.InstantiationMarker,
                "player_gadget_mine",
                photonView != null ? photonView.ViewID : 0
            });

        if (mineObject != null)
        {
            EnemyBot bot = mineObject.GetComponent<EnemyBot>();
            if (bot == null)
                bot = mineObject.AddComponent<EnemyBot>();

            bot.InitializeFromPhotonData();
        }

        return true;
    }

    Vector3 ResolveGadgetMineSpawnPosition()
    {
        float baseDistance = GetGadgetMinePlacementDistance();
        Vector2 forward = transform.up.sqrMagnitude > 0.001f ? (Vector2)transform.up.normalized : Vector2.up;
        Vector2 right = transform.right.sqrMagnitude > 0.001f ? (Vector2)transform.right.normalized : Vector2.right;
        Vector2 origin = transform.position;
        Vector2[] directions =
        {
            -forward,
            right,
            -right,
            (-forward + right).normalized,
            (-forward - right).normalized,
            forward
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 candidate = origin + (directions[i] * baseDistance);
            if (IsMineSpawnPositionFree(candidate))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin - (forward * baseDistance);
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    float GetGadgetMinePlacementDistance()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            float shipExtent = Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y);
            return Mathf.Max(0.9f, shipExtent + 0.55f);
        }

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(0.9f, Mathf.Max(collider2D.bounds.extents.x, collider2D.bounds.extents.y) + 0.55f);

        return 1.15f;
    }

    bool IsMineSpawnPositionFree(Vector2 candidate)
    {
        int hitCount = Physics2D.OverlapCircle(candidate, 0.38f, CreatePhysicsQueryFilter(), SpawnClearanceHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = SpawnClearanceHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    bool TryActivateBatteryCharge()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        return health != null && health.TryBeginBatteryShieldChargeAuthority();
    }

    public bool CanUseGadget(string itemId)
    {
        if (!photonView.IsMine || !IsGameStarted() || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (AreShipControlsBlocked())
            return false;

        if (!gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return false;

        if (IsLocalInstantDeployPending(itemId))
            return false;

        if (state.RemainingCharges <= 0 || Time.time < state.NextUseTime)
            return false;

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            return health != null && health.CanActivateBatteryChargeLocally();
        }

        if (string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
            return HasUsableDropbotCargoLocally();

        if (string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
            return PlayerProfileService.HasInstance && PlayerProfileService.Instance.HasFreeShipInventorySlot();

        if (string.Equals(itemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal) &&
            ShortScannerRevealStatus.IsActive)
        {
            return false;
        }

        if (string.Equals(itemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
        {
            HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
            return nebulaTarget == null || !nebulaTarget.IsCloaked;
        }

        if (string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return HasHackingDeviceCandidate();

        if (string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal) &&
            IsShipDamageActive(ShipDamageType.Booster))
        {
            ShipDamageState damageState = GetComponent<ShipDamageState>();
            if (damageState != null && damageState.IsBoosterDisabled())
                return false;
        }

        return true;
    }

    bool HasUsableDropbotCargoLocally()
    {
        if (!PlayerProfileService.HasInstance)
            return false;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        PlayerInventoryData inventory = profile != null ? profile.Inventory : null;
        if (inventory == null || !PlayerProfileService.Instance.HasFreePlayerInventorySlot())
            return false;

        inventory.Normalize();
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        int baseCapacity = PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        int shipCapacity = damageState != null ? damageState.GetAdjustedCargoCapacity(baseCapacity) : ShipDamageState.GetLocalCargoAdjustedCapacity(baseCapacity);
        int dropSlotIndex = PlayerProfileService.GetDropbotCargoSlotIndex(shipSkinIndex, shipCapacity, inventory.EquipmentSlots);
        return dropSlotIndex >= 0 &&
               dropSlotIndex < inventory.ShipSlots.Length &&
               !string.IsNullOrWhiteSpace(inventory.ShipSlots[dropSlotIndex]);
    }

    bool AreShipControlsBlocked()
    {
        if (IsEnemyBotShip())
            return false;

        if (AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null) ||
            GetComponent<AstronautSurvivor>() != null)
        {
            return true;
        }

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && (health.IsWreck || health.IsAstronautControlled || health.IsEvacuationAnimating || health.CurrentHP <= 0))
            return true;

        PlayerRepairDocking repairDocking = GetCachedRepairDocking();
        return repairDocking != null && repairDocking.IsBusy;
    }

    public int GetRemainingGadgetCharges(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return 0;

        return Mathf.Max(0, state.RemainingCharges);
    }

    public int GetMaxGadgetCharges(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return 0;

        return Mathf.Max(0, state.MaxCharges);
    }

    public Sprite GetGadgetIcon(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId) ? null : InventoryItemCatalog.GetIcon(itemId);
    }

    public string GetGadgetButtonLabel(string itemId)
    {
        return InventoryItemCatalog.GetShortLabel(itemId);
    }

    public Color GetGadgetButtonColor(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return new Color(0.14f, 0.5f, 0.28f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
            return new Color(0.78f, 0.22f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
            return new Color(0.08f, 0.48f, 0.72f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return new Color(0.16f, 0.46f, 0.78f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return new Color(0.08f, 0.36f, 0.86f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return new Color(0.72f, 0.5f, 0.08f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
            return new Color(0.76f, 0.42f, 0.08f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
            return new Color(0.2f, 0.62f, 0.88f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.TetherHarpoonId, StringComparison.Ordinal))
            return new Color(0.86f, 0.68f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
            return new Color(0.84f, 0.28f, 0.14f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.BioTrapId, StringComparison.Ordinal))
            return new Color(0.34f, 0.74f, 0.32f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.AsteroidBreacherBombId, StringComparison.Ordinal))
            return new Color(0.82f, 0.44f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.MetalDriftWallId, StringComparison.Ordinal))
            return new Color(0.34f, 0.42f, 0.48f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
            return new Color(0.68f, 0.2f, 0.72f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
            return new Color(0.72f, 0.22f, 0.18f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
            return new Color(0.62f, 0.24f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
            return new Color(0.18f, 0.62f, 0.58f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
            return new Color(0.1f, 0.54f, 0.78f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
            return new Color(0.22f, 0.34f, 0.5f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return new Color(0.1f, 0.58f, 0.72f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal))
            return new Color(0.74f, 0.58f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceDrillId, StringComparison.Ordinal))
            return new Color(0.86f, 0.54f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTrapId, StringComparison.Ordinal))
            return new Color(0.62f, 0.18f, 0.18f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return new Color(0.18f, 0.48f, 0.86f, 0.96f);

        return new Color(0.22f, 0.3f, 0.4f, 0.94f);
    }

    float ResolveGadgetCooldown(string gadgetItemId)
    {
        if (string.Equals(gadgetItemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return GadgetMinePlacementCooldown;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
            return GetAdjustedShortScannerRevealDuration();

        if (string.Equals(gadgetItemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return HackingDeviceWindupDuration;

        return 0f;
    }

    int ResolveGadgetMaxCharges(string gadgetItemId, int equippedCount)
    {
        equippedCount = Mathf.Max(0, equippedCount);
        if (equippedCount <= 0)
            return 0;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return GadgetMineDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
            return SpaceBombDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
            return DropbotDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
        {
            int charges = BatteryDefaultCharges * equippedCount;
            if (ShouldApplyRobyBatteryBonus())
                charges += equippedCount;

            return charges;
        }

        if (string.Equals(gadgetItemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return MagneticBeamDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return TractorBeamDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
            return LootHookDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
            return StasisBuoyDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.TetherHarpoonId, StringComparison.Ordinal))
            return TetherHarpoonDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
            return SpaceTorpedoDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.BioTrapId, StringComparison.Ordinal))
            return BioTrapDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.AsteroidBreacherBombId, StringComparison.Ordinal))
            return AsteroidBreacherBombDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.MetalDriftWallId, StringComparison.Ordinal))
            return MetalDriftWallDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
        {
            int charges = LureBeaconDefaultCharges * equippedCount;
            if (ShouldApplyRoburTrapGadgetBonus())
                charges *= 2;

            return charges;
        }

        if (string.Equals(gadgetItemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
            return AutoTurretDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
            return RocketAutoTurretDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
            return GuidanceSystemDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
            return ShortScannerDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
            return CloakDeviceDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return HackingDeviceDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceDrillId, StringComparison.Ordinal))
            return SpaceDrillDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceTrapId, StringComparison.Ordinal))
        {
            int charges = SpaceTrapDefaultCharges * equippedCount;
            if (ShouldApplyRoburTrapGadgetBonus())
                charges *= 2;

            return charges;
        }

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return SuperBoosterDefaultCharges * equippedCount;

        return 0;
    }

    public bool TryRestoreOneMissingGadgetChargeOnMaster()
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null)
            return false;

        Photon.Realtime.Player owner = photonView.Owner;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        if (orderedItems.Count == 0)
            return false;

        List<string> missingChargeItems = new List<string>();
        for (int i = 0; i < orderedItems.Count; i++)
        {
            string itemId = orderedItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
            int maxCharges = ResolveGadgetMaxCharges(itemId, equippedCount);
            if (maxCharges <= 0)
                continue;

            int remainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, maxCharges);
            if (remainingCharges < maxCharges)
                missingChargeItems.Add(itemId);
        }

        if (missingChargeItems.Count == 0)
            return false;

        string selectedItemId = missingChargeItems.Count == 1
            ? missingChargeItems[0]
            : missingChargeItems[UnityEngine.Random.Range(0, missingChargeItems.Count)];

        int selectedEquippedCount = gadgetCounts.TryGetValue(selectedItemId, out int selectedCount) ? selectedCount : 0;
        int selectedMaxCharges = ResolveGadgetMaxCharges(selectedItemId, selectedEquippedCount);
        int selectedRemainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, selectedItemId, selectedMaxCharges);
        if (selectedMaxCharges <= 0 || selectedRemainingCharges >= selectedMaxCharges)
            return false;

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, selectedItemId, selectedRemainingCharges + 1, selectedMaxCharges);
        return true;
    }

    bool ShouldApplyRoburTrapGadgetBonus()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return PilotCatalog.IsSelectedPilot(owner, PilotCatalog.RoburId);
    }

    bool ShouldApplyRobyBatteryBonus()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return PilotCatalog.IsSelectedPilot(owner, PilotCatalog.RobyId);
    }

    [PunRPC]
    void RequestAuthoritativeGadgetUse(string itemId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !IsGameStarted() || string.IsNullOrWhiteSpace(itemId))
            return;

        if (AreShipControlsBlocked())
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null || messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        Photon.Realtime.Player owner = photonView.Owner;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
        int maxCharges = ResolveGadgetMaxCharges(itemId, equippedCount);
        if (maxCharges <= 0)
            return;

        int remainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, maxCharges);
        if (remainingCharges <= 0)
            return;

        if (ShouldDebounceInstantDeploy(itemId) && IsAuthoritativeInstantDeployDebounced(itemId))
            return;

        if (string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
        {
            TryBeginDropbotLaunchRequest(owner, itemId);
            return;
        }

        if (string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
        {
            TryBeginLootHookRequest(owner, itemId);
            return;
        }

        if (!TryExecuteAuthoritativeGadgetUse(itemId))
            return;

        if (ShouldDebounceInstantDeploy(itemId))
            authoritativeInstantDeployDebounceUntil[itemId] = Time.time + InstantDeployAuthoritativeDebounceSeconds;

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, remainingCharges - 1, maxCharges);
    }

    static bool ShouldDebounceInstantDeploy(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal);
    }

    bool IsLocalInstantDeployPending(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) &&
               localInstantDeployPendingUntil.TryGetValue(itemId, out float pendingUntil) &&
               pendingUntil > Time.time;
    }

    bool IsAuthoritativeInstantDeployDebounced(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) &&
               authoritativeInstantDeployDebounceUntil.TryGetValue(itemId, out float pendingUntil) &&
               pendingUntil > Time.time;
    }

    void ClearExpiredInstantDeployLocks()
    {
        ClearExpiredInstantDeployLocks(localInstantDeployPendingUntil);
        if (PhotonNetwork.IsMasterClient)
            ClearExpiredInstantDeployLocks(authoritativeInstantDeployDebounceUntil);
    }

    static void ClearExpiredInstantDeployLocks(Dictionary<string, float> locks)
    {
        if (locks == null || locks.Count == 0)
            return;

        List<string> expired = null;
        foreach (KeyValuePair<string, float> pair in locks)
        {
            if (pair.Value > Time.time)
                continue;

            expired ??= new List<string>();
            expired.Add(pair.Key);
        }

        if (expired == null)
            return;

        for (int i = 0; i < expired.Count; i++)
            locks.Remove(expired[i]);
    }
}
