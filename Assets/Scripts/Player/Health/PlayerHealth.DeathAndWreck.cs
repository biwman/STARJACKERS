using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHealth : MonoBehaviourPun
{
    void HandleDeath(int attackerViewID)
    {
        if (deathHandled)
            return;

        deathHandled = true;

        PlayerMovement localMovement = GetComponent<PlayerMovement>();
        if (localMovement != null)
            localMovement.StopEngineAudioImmediately();

        EnemyBot bot = IsBotControlled ? GetComponent<EnemyBot>() : null;
        bool isEnemyAstronaut = IsEnemyAstronautControlled;
        bool useCustomBotDeath = bot != null && bot.HasCustomDeathExplosion();
        bool useSpaceAnimalDeath = bot != null && EnemyBot.IsSpaceAnimalKind(bot.Kind);
        if (!useCustomBotDeath && !useSpaceAnimalDeath)
            photonView.RPC(nameof(PlayDeathExplosion), RpcTarget.All);

        if (!IsBotControlled && !IsNeutralRiderControlled && !isEnemyAstronaut)
        {
            photonView.RPC(nameof(ShowDeathMessage), RpcTarget.All);
        }

        if (IsBotControlled)
        {
            if (PhotonNetwork.IsMasterClient && bot != null)
                AstronautSurvivor.SpawnEnemySurvivorsFrom(bot);

            if (useCustomBotDeath && bot != null)
            {
                if (ShouldConvertMineShotKillToWreck(bot, attackerViewID))
                {
                    photonView.RPC(nameof(BecomeEnemyWreck), RpcTarget.All, (int)bot.Kind);
                    return;
                }

                bot.RequestDetonation();
                return;
            }

            if (useSpaceAnimalDeath && bot != null)
            {
                StartCoroutine(DestroySpaceAnimalEnemyAfterDeathFrame(bot.Kind));
                return;
            }

            photonView.RPC(nameof(BecomeEnemyWreck), RpcTarget.All, bot != null ? (int)bot.Kind : (int)EnemyBotKind.Drone);
            return;
        }

        if (IsNeutralRiderControlled)
        {
            NeutralRiderController rider = GetComponent<NeutralRiderController>();
            string neutralWreckLoot = rider != null ? rider.BuildWreckLootJson() : PlayerProfileService.SerializeShipInventorySlots(new[] { InventoryItemCatalog.SpaceJunkStandardId });
            int neutralShipSkinIndex = rider != null ? rider.ShipSkinIndex : ShipCatalog.ExplorerBasicSkinIndex;
            photonView.RPC(nameof(BecomeNeutralRiderWreck), RpcTarget.All, neutralWreckLoot, neutralShipSkinIndex);
            return;
        }

        if (IsAstronautControlled)
        {
            if (isEnemyAstronaut)
            {
                if (photonView.Owner != null)
                    photonView.RPC(nameof(DestroySelf), photonView.Owner);
                else
                    DestroySelf();
                return;
            }

            int finalScore = GetCurrentRoundXp();
            if (!IsBotControlled)
            {
                finalScore = RoundResultsTracker.RecordOutcome(photonView.Owner, finalScore, "dead");
            }
            photonView.RPC(nameof(NotifyFinalDeathAndDestroySelf), photonView.Owner, finalScore);
            return;
        }

        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(photonView.Owner, 0);
        string[] currentShipSlots = PlayerProfileService.GetPlayerShipInventorySlots(photonView.Owner);
        int shipCapacity = PlayerProfileService.GetPlayerShipInventoryCapacity(photonView.Owner);
        bool emergencySuitBeaconEquipped = HasEquippedItem(InventoryItemCatalog.EmergencySuitBeaconId);
        bool escapePodEquipped = HasEquippedItem(InventoryItemCatalog.EscapePodId);
        bool equipmentLossEnabled = RoomSettings.IsEquipmentLossEnabled();
        string[] wreckLootSlots = PlayerProfileService.BuildLossWreckLoot(currentShipSlots, shipSkinIndex, shipCapacity);
        string wreckLoot = PlayerProfileService.SerializeShipInventorySlots(wreckLootSlots);
        string[] astronautCargoSlots = PlayerProfileService.BuildAstronautCargoSnapshot(currentShipSlots, shipSkinIndex, shipCapacity);
        string astronautCargo = PlayerProfileService.SerializeShipInventorySlots(astronautCargoSlots);
        Vector3 astronautSpawnPosition = FindSafeAstronautSpawnPosition();
        RoundXpTracker.RecordPlayerShipDestroyed(photonView.Owner);
        string protectedEquipmentItemId = escapePodEquipped ? InventoryItemCatalog.EscapePodId : string.Empty;
        photonView.RPC(nameof(ApplyLocalShipLossForWreck), photonView.Owner, shipSkinIndex, true, equipmentLossEnabled, astronautCargo, protectedEquipmentItemId);
        photonView.RPC(nameof(SpawnAstronautAfterDestruction), photonView.Owner, astronautSpawnPosition.x, astronautSpawnPosition.y, transform.eulerAngles.z, emergencySuitBeaconEquipped, escapePodEquipped);
        photonView.RPC(nameof(BecomeWreck), RpcTarget.All, wreckLoot, shipSkinIndex);
    }

    IEnumerator DestroySpaceAnimalEnemyAfterDeathFrame(EnemyBotKind kind)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
        {
            if (kind == EnemyBotKind.GravitySquid && bot.photonView != null)
                bot.photonView.RPC(nameof(EnemyBot.StopGravitySquidTetherRpc), RpcTarget.All);

            bot.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        if (PhotonNetwork.IsMasterClient)
            SpawnSpaceAnimalDeathLootDrop();

        float visualTargetSize = bot != null ? bot.VisualTargetSize : 2.4f;
        photonView.RPC(
            nameof(PlaySpaceAnimalDeathAnimationRpc),
            RpcTarget.All,
            (int)kind,
            transform.position.x,
            transform.position.y,
            transform.position.z,
            transform.eulerAngles.z,
            visualTargetSize);

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        yield return new WaitForSeconds(0.08f);

        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    void SpawnSpaceAnimalDeathLootDrop()
    {
        Vector2 driftDirection = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) * Vector2.up;
        if (driftDirection.sqrMagnitude < 0.001f)
            driftDirection = Vector2.up;

        string dropItemId = Random.value < SpaceAnimalBonesDropChance
            ? InventoryItemCatalog.SpaceAnimalBonesId
            : InventoryItemCatalog.SpaceAnimalRemainsId;

        GameObject drop = PhotonNetwork.Instantiate(
            "TreasureNetwork",
            transform.position,
            Quaternion.identity,
            0,
            new object[] { dropItemId });

        if (drop == null)
            return;

        Rigidbody2D dropBody = drop.GetComponent<Rigidbody2D>();
        if (dropBody != null)
        {
            dropBody.linearVelocity = driftDirection * Random.Range(0.42f, 0.82f);
            dropBody.angularVelocity = Random.Range(-34f, 34f);
        }
    }

    [PunRPC]
    void PlaySpaceAnimalDeathAnimationRpc(int kindValue, float x, float y, float z, float rotationZ, float visualTargetSize)
    {
        SpaceAnimalDeathVfx.Play((EnemyBotKind)kindValue, new Vector3(x, y, z), rotationZ, visualTargetSize);

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }
    }

    IEnumerator DestroyEnemyWithoutWreckAfterDeathFrame()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
            bot.enabled = false;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        yield return null;

        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    Vector3 FindSafeAstronautSpawnPosition()
    {
        Vector2 origin = transform.position;
        Vector2 right = transform.right;
        Vector2 up = transform.up;
        if (right.sqrMagnitude < 0.001f)
            right = Vector2.right;
        if (up.sqrMagnitude < 0.001f)
            up = Vector2.up;

        Vector2[] offsets =
        {
            right * 0.85f,
            -right * 0.85f,
            up * 0.85f,
            -up * 0.85f,
            (right + up).normalized * 1.05f,
            (right - up).normalized * 1.05f,
            (-right + up).normalized * 1.05f,
            (-right - up).normalized * 1.05f,
            right * 1.25f,
            -right * 1.25f,
            up * 1.25f,
            -up * 1.25f,
            (right + up).normalized * 1.45f,
            (right - up).normalized * 1.45f,
            (-right + up).normalized * 1.45f,
            (-right - up).normalized * 1.45f
        };

        if (TryResolveToxicBorderSafeAstronautSpawn(origin, out Vector2 borderSafeOrigin))
        {
            if (IsAstronautSpawnPositionUsable(borderSafeOrigin))
                return new Vector3(borderSafeOrigin.x, borderSafeOrigin.y, 0f);

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 candidate = borderSafeOrigin + offsets[i];
                if (IsAstronautSpawnPositionUsable(candidate))
                    return new Vector3(candidate.x, candidate.y, 0f);
            }
        }

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 candidate = origin + offsets[i];
            if (IsAstronautSpawnPositionUsable(candidate))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        if (IsAstronautSpawnPositionClearOfToxicBorder(origin))
            return new Vector3(origin.x, origin.y, 0f);

        if (TryResolveToxicBorderSafeAstronautSpawn(origin, out borderSafeOrigin))
            return new Vector3(borderSafeOrigin.x, borderSafeOrigin.y, 0f);

        return new Vector3(origin.x, origin.y, 0f);
    }

    bool IsAstronautSpawnPositionUsable(Vector2 candidate)
    {
        return IsAstronautSpawnPositionFree(candidate) &&
               IsAstronautSpawnPositionClearOfToxicBorder(candidate);
    }

    bool IsAstronautSpawnPositionFree(Vector2 candidate)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, AstronautSpawnClearanceRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    bool TryResolveToxicBorderSafeAstronautSpawn(Vector2 origin, out Vector2 safePosition)
    {
        safePosition = origin;
        if (!MapInstanceService.TryGetBoundsForWorldPosition(origin, out MapInstanceService.BoundsInfo bounds) ||
            !HasToxicBorder(bounds) ||
            IsInsideToxicBorderSafeInnerBounds(origin, bounds))
        {
            return false;
        }

        safePosition = ClampToToxicBorderSafeInnerBounds(origin, bounds);
        return true;
    }

    bool IsAstronautSpawnPositionClearOfToxicBorder(Vector2 candidate)
    {
        if (!MapInstanceService.TryGetBoundsForWorldPosition(candidate, out MapInstanceService.BoundsInfo bounds) ||
            !HasToxicBorder(bounds))
        {
            return true;
        }

        return IsInsideToxicBorderSafeInnerBounds(candidate, bounds);
    }

    static bool HasToxicBorder(MapInstanceService.BoundsInfo bounds)
    {
        return bounds.Size.x > bounds.InnerSize.x + 0.05f ||
               bounds.Size.y > bounds.InnerSize.y + 0.05f;
    }

    static bool IsInsideToxicBorderSafeInnerBounds(Vector2 position, MapInstanceService.BoundsInfo bounds)
    {
        float halfX = Mathf.Max(0.5f, bounds.InnerSize.x * 0.5f - AstronautToxicBorderSpawnMargin);
        float halfY = Mathf.Max(0.5f, bounds.InnerSize.y * 0.5f - AstronautToxicBorderSpawnMargin);
        return position.x >= bounds.Center.x - halfX &&
               position.x <= bounds.Center.x + halfX &&
               position.y >= bounds.Center.y - halfY &&
               position.y <= bounds.Center.y + halfY;
    }

    static Vector2 ClampToToxicBorderSafeInnerBounds(Vector2 position, MapInstanceService.BoundsInfo bounds)
    {
        float halfX = Mathf.Max(0.5f, bounds.InnerSize.x * 0.5f - AstronautToxicBorderSpawnMargin);
        float halfY = Mathf.Max(0.5f, bounds.InnerSize.y * 0.5f - AstronautToxicBorderSpawnMargin);
        return new Vector2(
            Mathf.Clamp(position.x, bounds.Center.x - halfX, bounds.Center.x + halfX),
            Mathf.Clamp(position.y, bounds.Center.y - halfY, bounds.Center.y + halfY));
    }

    bool ShouldConvertMineShotKillToWreck(EnemyBot bot, int attackerViewID)
    {
        if (bot == null || bot.Kind != EnemyBotKind.SpaceMine || attackerViewID <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (attackerView == null)
            return false;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        return attackerHealth != null &&
               !attackerHealth.IsBotControlled &&
               !attackerHealth.IsNeutralRiderControlled &&
               !attackerHealth.IsAstronautControlled &&
               !attackerHealth.IsWreck;
    }

    int GetCurrentRoundXp()
    {
        int propScore = RoomSettings.GetPlayerRoundXp(photonView.Owner);
        if (propScore > 0)
            return propScore;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            return collector.totalScore;

        return 0;
    }

    [PunRPC]
    public void OnEvacuated(int amount)
    {
        if (!photonView.IsMine)
            return;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
        {
            collector.AddScore(amount);
        }
    }

    [PunRPC]
    public async void NotifyFinalEvacuation(int finalScore, string outcome)
    {
        if (!photonView.IsMine)
            return;

        pendingEvacuationFinalScore = Mathf.Max(0, finalScore);
        pendingEvacuationOutcome = string.IsNullOrWhiteSpace(outcome) ? "extracted" : outcome;
        hasPendingEvacuationSummary = true;
        NetworkManager.MarkCurrentRoundEndedForLocalPlayer(pendingEvacuationOutcome);
        if (string.Equals(pendingEvacuationOutcome, "evacuated", System.StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await PlayerProfileService.Instance.RestorePendingAstronautCargoAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to restore astronaut cargo: " + ex);
            }
        }

        try
        {
            int returningShipSkinIndex = ResolveCurrentRoundShipSkinIndex();
            if (string.Equals(pendingEvacuationOutcome, "extracted", System.StringComparison.OrdinalIgnoreCase) &&
                ShipCatalog.GetShipTypeFromSkinIndex(returningShipSkinIndex) == ShipType.Avenger)
            {
                await PlayerProfileService.Instance.CompleteAvengerTheftAttemptAsync(returningShipSkinIndex);
            }
            else
            {
                await PlayerProfileService.Instance.FailAvengerTheftAttemptAsync();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to resolve Avenger theft attempt on round end: " + ex);
        }

        try
        {
            if (string.Equals(pendingEvacuationOutcome, "extracted", System.StringComparison.OrdinalIgnoreCase))
            {
                ViperTestFlightResult viperResult = await PlayerProfileService.Instance.RecordViperTestFlightReturnAsync(ResolveCurrentRoundElapsedSeconds());
                if (viperResult == ViperTestFlightResult.TooShort)
                    RoundAnnouncementUI.Show("Test flight was too short", 2.8f);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to resolve Viper test flight on round end: " + ex);
        }

        try
        {
            if (string.Equals(pendingEvacuationOutcome, "extracted", System.StringComparison.OrdinalIgnoreCase))
            {
                bool arrowCompleted = await PlayerProfileService.Instance.CompleteArrowFinalRunAsync(
                    ResolveCurrentRoundShipSkinIndex(),
                    ArrowRacePlotController.IsLocalFinalRunReadyForExtraction());
                if (arrowCompleted)
                    RoundAnnouncementUI.Show("Arrow unlocked.", 3f);
            }
            else
            {
                await PlayerProfileService.Instance.FailArrowFinalRunAsync();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to resolve Arrow final run on round end: " + ex);
        }
        finally
        {
            ArrowRacePlotController.ClearLocalFinalRunState();
        }

        TryRecordExtractionPilotProgress(pendingEvacuationOutcome);
    }

    int ResolveCurrentRoundShipSkinIndex()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return RoomSettings.GetPlayerShipSkin(owner, ShipCatalog.ExplorerBasicSkinIndex);
    }

    [PunRPC]
    public async void NotifyViperWreckRecovered()
    {
        if (!photonView.IsMine)
            return;

        try
        {
            await PlayerProfileService.Instance.RecordViperWreckRecoveredAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to record Viper wreck recovery: " + ex);
        }
    }

    [PunRPC]
    public async void NotifyBisonIndustrialPartsDelivered()
    {
        if (!photonView.IsMine)
            return;

        try
        {
            int delivered = await PlayerProfileService.Instance.RecordBisonIndustrialPartsDeliveredAsync();
            if (delivered >= PlayerProfileService.BisonIndustrialPartsRequired)
                RoundAnnouncementUI.Show("Bison unlocked.", 3f);
            else
                RoundAnnouncementUI.Show("Industrial parts delivered: " + delivered + "/" + PlayerProfileService.BisonIndustrialPartsRequired, 3f);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to record Bison industrial parts delivery: " + ex);
        }
    }

    [PunRPC]
    public async void NotifyInvaderImprintRecovered(int completedStage)
    {
        if (!photonView.IsMine)
            return;

        try
        {
            int recovered = await PlayerProfileService.Instance.RecordInvaderImprintRecoveredAsync(completedStage);
            if (recovered >= PlayerProfileService.InvaderImprintsRequired)
                RoundAnnouncementUI.Show("Invader unlocked.", 3f);
            else
                RoundAnnouncementUI.Show("Alien imprints recovered: " + recovered + "/" + PlayerProfileService.InvaderImprintsRequired, 3f);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to record Invader imprint recovery: " + ex);
        }
    }

    float ResolveCurrentRoundElapsedSeconds()
    {
        GameTimer timer = FindAnyObjectByType<GameTimer>();
        if (timer != null)
            return Mathf.Max(0f, RoomSettings.GetRoundDuration() - timer.GetCurrentRemainingTime());

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startValue) &&
            startValue is double startTime &&
            startTime >= 0d)
        {
            return Mathf.Max(0f, (float)(PhotonNetwork.Time - startTime));
        }

        return 0f;
    }

    async void TryRecordExtractionPilotProgress(string outcome)
    {
        if (!string.Equals(outcome, "extracted", System.StringComparison.OrdinalIgnoreCase))
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        string[] shipSlots = profile != null && profile.Inventory != null ? profile.Inventory.ShipSlots : null;
        int cargoValue = PilotCatalog.GetCargoValueAstrons(shipSlots);
        if (cargoValue >= RoundXpBalance.RaiderCargoValueThreshold)
            await PlayerProfileService.Instance.UnlockPilotAsync(PilotCatalog.NovaId);

        if (AshPilotRoundTracker.MeetsOverloadReturnRequirements(profile, out _))
            await PlayerProfileService.Instance.RecordPilotAshOverloadReturnAsync();

        if (string.Equals(RoomSettings.GetSelectedLobbyMapId(), "pirate_bay", System.StringComparison.OrdinalIgnoreCase))
            await PlayerProfileService.Instance.RecordPilotPirateBayReturnAsync();

        if (AtlasPilotRoundTracker.GetNetCargoValueAstrons(profile) >= PilotCatalog.AtlasRequiredNetCargoAstrons)
            await PlayerProfileService.Instance.RecordPilotAtlasMapReturnAsync(RoomSettings.GetSelectedLobbyMapId());

        if (ShouldAwardCharlieCargoGrowthExtractionBonus(profile))
            await PlayerProfileService.Instance.AddAstronsAsync(1000);
    }

    bool ShouldAwardCharlieCargoGrowthExtractionBonus(PlayerProfileData profile)
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return PilotCatalog.IsSelectedPilot(owner, PilotCatalog.CharlieSmartId) &&
               AtlasPilotRoundTracker.GetNetOccupiedCargoSlots(profile) >= 6;
    }

    [PunRPC]
    public async void AwardCharlieLastSecondExtractionBonus()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.AddAstronsAsync(2000);
    }

    [PunRPC]
    public async void UnlockRoburPilotAfterHumanKill()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.UnlockPilotAsync(PilotCatalog.RoburId);
    }

    [PunRPC]
    public async void RecordPilotDroneKillProgress()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.RecordPilotDroneKillAsync();
    }

    [PunRPC]
    public async void RecordMothershipKillMapProgress()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.RecordMothershipKillAsync();
    }

    [PunRPC]
    public async void RecordCareerKillProgress(bool killedHumanPlayer, bool killedNeutralRaider)
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.RecordCareerKillAsync(killedHumanPlayer, killedNeutralRaider);
    }

    [PunRPC]
    public void RecordAshComputerEnemyKillProgress()
    {
        if (!photonView.IsMine)
            return;

        AshPilotRoundTracker.RecordComputerEnemyKill();
    }

    [PunRPC]
    public void ApplyNovaKillSpeedBoost()
    {
        if (!photonView.IsMine)
            return;

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ActivatePilotSpeedBoost(1.2f, 5f);
    }

    [PunRPC]
    public void DestroySelf()
    {
        if (!photonView.IsMine)
            return;

        TryDestroyOwnedPhotonObject();
    }

    [PunRPC]
    void NotifyFinalDeath(int finalScore)
    {
        NotifyFinalDeathLocal(finalScore, false);
    }

    [PunRPC]
    void NotifyFinalDeathAndDestroySelf(int finalScore)
    {
        NotifyFinalDeathLocal(finalScore, true);
    }

    async void NotifyFinalDeathLocal(int finalScore, bool destroySelfAfterUi)
    {
        if (!photonView.IsMine)
            return;

        EarlyRoundExitUI.ShowEndRoundButton(finalScore, "dead");
        if (destroySelfAfterUi)
            StartCoroutine(DestroySelfAfterDeathUiFrame());

        PlayerProfileService.Instance.DiscardPendingAstronautCargo();
        try
        {
            await PlayerProfileService.Instance.FailAvengerTheftAttemptAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to clear Avenger theft attempt after death: " + ex);
        }

        try
        {
            await PlayerProfileService.Instance.FailArrowFinalRunAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to clear Arrow final run after death: " + ex);
        }
        finally
        {
            ArrowRacePlotController.ClearLocalFinalRunState();
        }
    }

    IEnumerator DestroySelfAfterDeathUiFrame()
    {
        yield return null;
        DestroySelf();
    }

    IEnumerator EvacuationSequenceRoutine(Vector2 portalCenter)
    {
        isEvacuationAnimating = true;
        if (photonView.IsMine)
            GameplayHudVisibility.SuppressForExtractionCinematic();

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.CancelActiveGadgetEffectsForShipLoss();
            shooting.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        LootingFriendController lootingFriend = GetComponent<LootingFriendController>();
        if (lootingFriend != null)
            lootingFriend.DeactivateForShipLoss();

        FiringFriendController firingFriend = GetComponent<FiringFriendController>();
        if (firingFriend != null)
            firingFriend.DeactivateForShipLoss();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        AudioManager.Instance.PlayExtractionSequenceAt(transform.position);

        Vector3 startPosition = transform.position;
        Vector3 endPosition = new Vector3(portalCenter.x, portalCenter.y, startPosition.z);
        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(
            Mathf.Sign(startScale.x) * MinimumEvacuationScale,
            Mathf.Sign(startScale.y) * MinimumEvacuationScale,
            startScale.z);
        if (Mathf.Abs(endScale.x) < MinimumEvacuationScale)
            endScale.x = MinimumEvacuationScale;
        if (Mathf.Abs(endScale.y) < MinimumEvacuationScale)
            endScale.y = MinimumEvacuationScale;

        float elapsed = 0f;
        while (elapsed < EvacuationAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / EvacuationAnimationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(startPosition, endPosition, eased);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        transform.position = endPosition;
        transform.localScale = endScale;

        if (photonView.IsMine)
        {
            if (hasPendingEvacuationSummary && HasOtherActiveRoundPlayers())
                EarlyRoundExitUI.ShowFinishedRoundSummaryDelayed(pendingEvacuationFinalScore, pendingEvacuationOutcome);

            TryDestroyOwnedPhotonObject();
        }
    }

    bool HasOtherActiveRoundPlayers()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player == this)
                continue;

            if (GameTimer.IsActiveRoundPlayer(player))
                return true;
        }

        return false;
    }

    [PunRPC]
    public void ClearLocalShipInventoryForWreck(int shipSkinIndex)
    {
        ApplyLocalShipLossForWreck(shipSkinIndex, true, false, string.Empty, string.Empty);
    }

    [PunRPC]
    public void ApplyLocalShipLossForWreck(int shipSkinIndex, bool loseShipInventory, bool loseEquipment, string serializedAstronautCargo)
    {
        ApplyLocalShipLossForWreck(shipSkinIndex, loseShipInventory, loseEquipment, serializedAstronautCargo, string.Empty);
    }

    [PunRPC]
    public async void ApplyLocalShipLossForWreck(int shipSkinIndex, bool loseShipInventory, bool loseEquipment, string serializedAstronautCargo, string protectedEquipmentItemId)
    {
        if (!photonView.IsMine)
            return;

        try
        {
            await PlayerProfileService.Instance.ApplyShipLossAsync(shipSkinIndex, loseShipInventory, loseEquipment, serializedAstronautCargo, protectedEquipmentItemId, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to apply local ship loss: " + ex);
        }
    }

    [PunRPC]
    void SpawnAstronautAfterDestruction(float x, float y, float rotationZ, bool emergencySuitBeaconEquipped, bool escapePodEquipped)
    {
        if (!photonView.IsMine)
            return;

        if (astronautSpawnedAfterDestruction)
            return;

        astronautSpawnedAfterDestruction = true;

        PhotonNetwork.LocalPlayer.TagObject = null;
        GameObject astronaut = PhotonNetwork.Instantiate(
            "Player",
            new Vector3(x, y, 0f),
            Quaternion.Euler(0f, 0f, rotationZ),
            0,
            new object[] { AstronautSurvivor.AstronautInstantiationMarker, emergencySuitBeaconEquipped, escapePodEquipped });

        if (astronaut != null)
        {
            AstronautSurvivor survivor = astronaut.GetComponent<AstronautSurvivor>();
            if (survivor == null)
                survivor = astronaut.AddComponent<AstronautSurvivor>();
            survivor.InitializeFromPhotonData();
            ActorIdentity.Ensure(astronaut);

            PlayerHealth astronautHealth = astronaut.GetComponent<PlayerHealth>();
            if (astronautHealth != null)
                GameVisualTheme.ApplyPlayerVisual(astronautHealth);

            PhotonNetwork.LocalPlayer.TagObject = astronaut;
        }
    }

    [PunRPC]
    void BecomeWreck(string serializedLoot, int shipSkinIndex)
    {
        IsWreck = true;
        ActorIdentity.Ensure(gameObject);

        if (photonView != null &&
            photonView.IsMine &&
            PhotonNetwork.LocalPlayer != null &&
            ReferenceEquals(PhotonNetwork.LocalPlayer.TagObject, gameObject))
        {
            PhotonNetwork.LocalPlayer.TagObject = null;
        }

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.CancelActiveGadgetEffectsForShipLoss();
            shooting.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
        {
            collector.ForceCancelCollectionForDeath();
            collector.enabled = false;
        }

        LootingFriendController lootingFriend = GetComponent<LootingFriendController>();
        if (lootingFriend != null)
            lootingFriend.DeactivateForShipLoss();

        FiringFriendController firingFriend = GetComponent<FiringFriendController>();
        if (firingFriend != null)
            firingFriend.DeactivateForShipLoss();

        HealthBarUI healthBarUi = GetComponent<HealthBarUI>();
        if (healthBarUi != null)
            Destroy(healthBarUi);

        ShieldBarUI shieldBarUi = GetComponent<ShieldBarUI>();
        if (shieldBarUi != null)
            Destroy(shieldBarUi);

        RoundVitalsIconHudUI vitalsHudUi = GetComponent<RoundVitalsIconHudUI>();
        if (vitalsHudUi != null)
            Destroy(vitalsHudUi);

        BoosterBarUI boosterBarUi = GetComponent<BoosterBarUI>();
        if (boosterBarUi != null)
            Destroy(boosterBarUi);

        ComplexAmmoBarUI complexAmmoBarUi = GetComponent<ComplexAmmoBarUI>();
        if (complexAmmoBarUi != null)
            Destroy(complexAmmoBarUi);

        SuperAttackUI superAttackUi = GetComponent<SuperAttackUI>();
        if (superAttackUi != null)
            Destroy(superAttackUi);

        WeaponSwitchButtonUI weaponSwitchUi = GetComponent<WeaponSwitchButtonUI>();
        if (weaponSwitchUi != null)
            Destroy(weaponSwitchUi);

        ShipInventoryHudUI cargoHudUi = GetComponent<ShipInventoryHudUI>();
        if (cargoHudUi != null)
            Destroy(cargoHudUi);

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 6f;
            body.linearDamping = 0.62f;
            body.angularDamping = 0.78f;

            Vector2 driftDirection = Random.insideUnitCircle.normalized;
            if (driftDirection.sqrMagnitude < 0.001f)
                driftDirection = Vector2.left;

            body.linearVelocity = driftDirection * 0.14f;
            body.angularVelocity = Random.Range(-5f, 5f);
        }

        ShipWreck wreck = GetComponent<ShipWreck>();
        if (wreck == null)
            wreck = gameObject.AddComponent<ShipWreck>();

        wreck.InitializeFromLootJson(serializedLoot, shipSkinIndex);
        wreck.SetBaseColor(Color.white);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.white;
        }

        GameVisualTheme.ApplyPlayerVisual(this);
    }

    [PunRPC]
    void BecomeNeutralRiderWreck(string serializedLoot, int shipSkinIndex)
    {
        IsWreck = true;
        ActorIdentity.Ensure(gameObject);

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        NeutralRiderController rider = GetComponent<NeutralRiderController>();
        if (rider != null)
            rider.MarkWreckConverted();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 5.2f;
            body.linearDamping = 0.62f;
            body.angularDamping = 0.78f;

            Vector2 driftDirection = UnityEngine.Random.insideUnitCircle.normalized;
            if (driftDirection.sqrMagnitude < 0.001f)
                driftDirection = Vector2.left;

            body.linearVelocity = driftDirection * 0.16f;
            body.angularVelocity = UnityEngine.Random.Range(-5f, 5f);
        }

        ShipWreck wreck = GetComponent<ShipWreck>();
        if (wreck == null)
            wreck = gameObject.AddComponent<ShipWreck>();

        int safeSkinIndex = Mathf.Clamp(shipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        wreck.InitializeFromLootJson(serializedLoot, safeSkinIndex);
        wreck.SetBaseColor(new Color(0.82f, 0.96f, 1f, 0.96f));

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.white;
        }

        GameVisualTheme.ApplyPlayerVisual(this);
    }

    [PunRPC]
    void BecomeEnemyWreck(int kindValue)
    {
        IsWreck = true;
        ActorIdentity.Ensure(gameObject);
        EnemyBotKind enemyKind = (EnemyBotKind)kindValue;
        if (PhotonNetwork.IsMasterClient && enemyKind == EnemyBotKind.MilitaryVan)
            EnemyBotManager.NotifyMilitaryVanDestroyed(GetComponent<EnemyBot>());

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
        {
            if (enemyKind == EnemyBotKind.RescueShip && bot.photonView != null)
                bot.photonView.RPC(nameof(EnemyBot.StopRescueShipBeamRpc), RpcTarget.All);

            if (enemyKind == EnemyBotKind.PirateBase)
            {
                bot.DropPirateBaseCargoOnDeath();
                if (bot.photonView != null)
                    bot.photonView.RPC(nameof(EnemyBot.StopPirateBaseCollectionBeamRpc), RpcTarget.All);
            }

            if (enemyKind == EnemyBotKind.ContainerShip)
            {
                bot.HideContainerShipCargoVisual();
                bot.DropContainerShipCargoOnDeath();
            }

            if (enemyKind == EnemyBotKind.Mothership)
                bot.ConvertMothershipTurretsToWreckVisuals();

            if (enemyKind == EnemyBotKind.CosmicWorm)
            {
                CosmicWormVisualController.StopFor(bot);
                if (photonView != null)
                    CosmicWormSwallowVfx.StopEffect(photonView.ViewID);
            }

            bot.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(enemyKind);
        EnemyWreckProfile wreckProfile = definition != null ? definition.Wreck : null;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = wreckProfile != null ? wreckProfile.Mass : 4.6f;
            body.linearDamping = wreckProfile != null ? wreckProfile.LinearDamping : 0.56f;
            body.angularDamping = wreckProfile != null ? wreckProfile.AngularDamping : 0.72f;

            Vector2 driftDirection = Random.insideUnitCircle.normalized;
            if (driftDirection.sqrMagnitude < 0.001f)
                driftDirection = Vector2.left;

            body.linearVelocity = driftDirection * (wreckProfile != null ? wreckProfile.DriftSpeed : 0.12f);
            float angularRange = wreckProfile != null ? wreckProfile.AngularVelocityRange : 4f;
            body.angularVelocity = Random.Range(-angularRange, angularRange);
        }

        ShipWreck wreck = GetComponent<ShipWreck>();
        if (wreck == null)
            wreck = gameObject.AddComponent<ShipWreck>();

        string[] rewardItemIds = ResolveEnemyWreckRewardItemIds(enemyKind, wreckProfile);
        string serializedLoot = PlayerProfileService.SerializeShipInventorySlots(rewardItemIds);
        wreck.InitializeFromLootJson(serializedLoot, -1, kindValue);
        wreck.SetDestroyWhenEmpty(wreckProfile == null || wreckProfile.DestroyWhenEmpty);

        Color baseColor = wreckProfile != null ? wreckProfile.BaseColor : new Color(0.2f, 0.23f, 0.26f, 0.94f);
        wreck.SetBaseColor(baseColor);
        TryDropPirateCaseNearEnemyWreck(enemyKind, body);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Sprite wreckSprite = wreckProfile != null ? wreckProfile.GetVisualSprite() : null;
            if (wreckSprite != null)
                renderer.sprite = wreckSprite;

            renderer.color = baseColor;
        }

        ConfigureEnemyWreckCollider(enemyKind, renderer);
        GameVisualTheme.ApplyPlayerVisual(this);
    }

    string[] ResolveEnemyWreckRewardItemIds(EnemyBotKind enemyKind, EnemyWreckProfile wreckProfile)
    {
        string rewardItemId = ResolveEnemyWreckRewardItemId(enemyKind, wreckProfile);
        if (enemyKind == EnemyBotKind.RiftWarden)
        {
            string alienSecretItemId = InventoryItemCatalog.GetAlienSecretItemId(Random.Range(0, InventoryItemCatalog.AlienSecretVariantCount));
            return new[] { rewardItemId, alienSecretItemId };
        }

        return new[] { rewardItemId };
    }

    string ResolveEnemyWreckRewardItemId(EnemyBotKind enemyKind, EnemyWreckProfile wreckProfile)
    {
        if (enemyKind == EnemyBotKind.MilitaryVan && Random.value < 0.2f)
            return InventoryItemCatalog.CashSuitcaseId;

        string rewardItemId = wreckProfile != null ? wreckProfile.RewardItemId : InventoryItemCatalog.DroidScrapId;
        if (string.Equals(rewardItemId, InventoryItemCatalog.AlienSecretId, System.StringComparison.Ordinal))
            return InventoryItemCatalog.GetAlienSecretItemId(Random.Range(0, InventoryItemCatalog.AlienSecretVariantCount));

        return rewardItemId;
    }

    void TryDropPirateCaseNearEnemyWreck(EnemyBotKind enemyKind, Rigidbody2D wreckBody)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        float dropChance = GetPirateCaseDropChance(enemyKind);
        if (dropChance <= 0f || Random.value >= dropChance)
            return;

        Vector2 direction = wreckBody != null && wreckBody.linearVelocity.sqrMagnitude > 0.001f
            ? wreckBody.linearVelocity.normalized
            : Random.insideUnitCircle;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        direction.Normalize();
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        float distance = enemyKind == EnemyBotKind.PirateBase ? 1.65f : 0.92f;
        Vector3 dropPosition = transform.position + (Vector3)(direction * distance + tangent * Random.Range(-0.32f, 0.32f));
        Vector2 inheritedDrift = wreckBody != null ? wreckBody.linearVelocity * 0.35f : Vector2.zero;
        Vector2 driftVelocity = inheritedDrift + direction * Random.Range(0.45f, 0.9f);

        DroppedCargoManager.DropItemAtPosition(InventoryItemCatalog.PirateCaseId, dropPosition, driftVelocity);
    }

    static float GetPirateCaseDropChance(EnemyBotKind enemyKind)
    {
        switch (enemyKind)
        {
            case EnemyBotKind.PirateFighter:
                return 0.075f;
            case EnemyBotKind.Corsair:
                return 0.09f;
            case EnemyBotKind.PirateFighterElite:
                return 0.225f;
            case EnemyBotKind.PirateFighterAce:
                return 0.75f;
            case EnemyBotKind.PirateBase:
                return 0.30f;
            default:
                return 0f;
        }
    }

    void ConfigureEnemyWreckCollider(EnemyBotKind enemyKind, SpriteRenderer renderer)
    {
        if (enemyKind != EnemyBotKind.CosmicWorm)
            return;

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Bounds bounds = renderer != null ? renderer.bounds : new Bounds(transform.position, new Vector3(2.6f, 1.8f, 0f));
            Vector2 compactSize = new Vector2(
                Mathf.Clamp(bounds.size.x * 0.22f, 1.7f, 2.9f),
                Mathf.Clamp(bounds.size.y * 0.34f, 1.15f, 2.2f));
            SetWorldBoxColliderSize(boxCollider, compactSize);
            boxCollider.offset = Vector2.zero;
            boxCollider.isTrigger = true;
        }

        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
            circleCollider.enabled = false;
    }

    static void SetWorldBoxColliderSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        if (collider2D == null)
            return;

        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
    }
}
