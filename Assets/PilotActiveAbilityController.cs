using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed class PilotActiveAbilityController : MonoBehaviourPun
{
    const float JakeBarrierDuration = 10f;
    const float RoburMarkDuration = 15f;
    const float CharlieConfusionDuration = 10f;
    const float CharlieConfusionRadius = 14f;
    const float CovaxElectromagneticDuration = 10f;
    const float CovaxElectromagneticRange = 18f;
    const float CovaxElectromagneticConeAngle = 76f;
    const float CovaxElectromagneticSpeedMultiplier = 1f / 3f;
    const float CovaxElectromagneticFireIntervalMultiplier = 3f;
    const float AshSuperchargeDuration = 10f;
    const int SirNowitzkyBreachShots = 5;
    const float NovaCollectRangeMultiplier = 2.5f;
    const float NovaScavengerChannelDuration = 2.5f;

    static readonly Dictionary<int, float> RoburMarkedTargets = new Dictionary<int, float>();

    bool used;
    float activeUntil;
    int sirNowitzkyBreachShotsRemaining;
    string activePilotId = PilotCatalog.JakeId;

    public bool HasBeenUsed => used;
    public bool IsJakeBarrierActive => IsSelectedPilot(PilotCatalog.JakeId) && Time.time < activeUntil;
    public int SirNowitzkyBreachShotsRemaining => Mathf.Max(0, sirNowitzkyBreachShotsRemaining);
    public float ActiveRemainingSeconds => Mathf.Max(0f, activeUntil - Time.time);

    void Update()
    {
        CleanupExpiredRoburMarks();
        if (sirNowitzkyBreachShotsRemaining <= 0 && Time.time >= activeUntil)
            activeUntil = 0f;
    }

    public bool CanUseAbility()
    {
        if (!photonView.IsMine || used || !IsGameStarted())
            return false;

        return !AreShipControlsBlocked();
    }

    public void TryUseAbility()
    {
        if (!CanUseAbility())
            return;

        string pilotId = ResolvePilotId();
        bool boosterNeedsRefill = false;
        if (string.Equals(pilotId, PilotCatalog.RobyId, StringComparison.Ordinal))
        {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            boosterNeedsRefill = movement != null && movement.BoosterNormalized < 0.995f;
        }

        photonView.RPC(nameof(RequestUsePilotActiveAbility), RpcTarget.MasterClient, pilotId, boosterNeedsRefill);
    }

    public string GetHudStatusText()
    {
        if (!used)
            return "ACTIVE";

        if (IsSelectedPilot(PilotCatalog.SirNowitzkyId) && sirNowitzkyBreachShotsRemaining > 0)
            return sirNowitzkyBreachShotsRemaining.ToString();

        float remaining = ActiveRemainingSeconds;
        if (remaining > 0.05f)
            return Mathf.CeilToInt(remaining) + "s";

        return "USED";
    }

    public bool TryConsumeSirNowitzkyBreachSalvo()
    {
        if (!IsSelectedPilot(PilotCatalog.SirNowitzkyId) || sirNowitzkyBreachShotsRemaining <= 0)
            return false;

        sirNowitzkyBreachShotsRemaining = Mathf.Max(0, sirNowitzkyBreachShotsRemaining - 1);
        if (photonView != null && photonView.IsMine)
            photonView.RPC(nameof(SyncSirNowitzkyBreachShotsRpc), RpcTarget.All, sirNowitzkyBreachShotsRemaining);

        return true;
    }

    public static bool IsRoburMarked(int targetViewId)
    {
        if (targetViewId <= 0)
            return false;

        CleanupExpiredRoburMarks();
        return RoburMarkedTargets.ContainsKey(targetViewId);
    }

    [PunRPC]
    void RequestUsePilotActiveAbility(string requestedPilotId, bool localBoosterNeedsRefill, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || used || !IsGameStarted())
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null ||
            messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
        {
            return;
        }

        if (AreShipControlsBlocked())
            return;

        string pilotId = ResolvePilotId();
        if (!string.Equals(PilotCatalog.NormalizePilotId(requestedPilotId), pilotId, StringComparison.Ordinal))
            return;

        bool activated = false;
        float duration = 0f;
        int breachShots = 0;

        if (string.Equals(pilotId, PilotCatalog.JakeId, StringComparison.Ordinal))
        {
            activated = true;
            duration = JakeBarrierDuration;
            photonView.RPC(nameof(PlayJakeBarrierVfxRpc), RpcTarget.All, JakeBarrierDuration);
        }
        else if (string.Equals(pilotId, PilotCatalog.NovaId, StringComparison.Ordinal))
        {
            TreasureCollector collector = GetComponent<TreasureCollector>();
            string targetViewIds = string.Empty;
            activated = collector != null && collector.TryNovaScavengerBurstOnMaster(NovaCollectRangeMultiplier, NovaScavengerChannelDuration, out targetViewIds);
            if (activated)
            {
                duration = NovaScavengerChannelDuration;
                photonView.RPC(nameof(PlayNovaScavengerBurstVfxRpc), RpcTarget.All, targetViewIds, NovaScavengerChannelDuration);
            }
        }
        else if (string.Equals(pilotId, PilotCatalog.RoburId, StringComparison.Ordinal))
        {
            PhotonView targetView = FindNearestRoburMarkTarget();
            activated = targetView != null;
            if (activated)
            {
                duration = RoburMarkDuration;
                photonView.RPC(nameof(ApplyRoburMarkRpc), RpcTarget.All, targetView.ViewID, RoburMarkDuration);
            }
        }
        else if (string.Equals(pilotId, PilotCatalog.SirNowitzkyId, StringComparison.Ordinal))
        {
            activated = true;
            breachShots = SirNowitzkyBreachShots;
        }
        else if (string.Equals(pilotId, PilotCatalog.RobyId, StringComparison.Ordinal))
        {
            PlayerShooting shooting = GetComponent<PlayerShooting>();
            bool restoredGadget = shooting != null && shooting.TryRestoreOneMissingGadgetChargeOnMaster();
            activated = restoredGadget || localBoosterNeedsRefill;
            if (activated)
                photonView.RPC(nameof(ApplyRobyBoosterRefillRpc), photonView.Owner);
        }
        else if (string.Equals(pilotId, PilotCatalog.CharlieSmartId, StringComparison.Ordinal))
        {
            string confusedTargets = ApplyCharlieConfusionOnMaster();
            activated = !string.IsNullOrWhiteSpace(confusedTargets);
            if (activated)
            {
                duration = CharlieConfusionDuration;
                photonView.RPC(nameof(PlayCharlieConfusionWaveRpc), RpcTarget.All, CharlieConfusionRadius);
            }
        }
        else if (string.Equals(pilotId, PilotCatalog.CovaxId, StringComparison.Ordinal))
        {
            Vector2 forward = ResolveForwardDirection();
            Vector3 origin = GetShipTipPosition(forward);
            ApplyCovaxElectromagneticWaveOnMaster(origin, forward);
            activated = true;
            duration = CovaxElectromagneticDuration;
            photonView.RPC(nameof(PlayCovaxElectromagneticWaveRpc), RpcTarget.All, origin.x, origin.y, forward.x, forward.y, CovaxElectromagneticRange, CovaxElectromagneticConeAngle);
        }
        else if (string.Equals(pilotId, PilotCatalog.AshId, StringComparison.Ordinal))
        {
            activated = true;
            duration = AshSuperchargeDuration;
            photonView.RPC(nameof(ApplyAshSuperchargeRpc), RpcTarget.All, AshSuperchargeDuration);
        }

        if (!activated)
            return;

        used = true;
        activePilotId = pilotId;
        activeUntil = duration > 0f ? Time.time + duration : 0f;
        sirNowitzkyBreachShotsRemaining = breachShots;
        photonView.RPC(nameof(SyncPilotActiveAbilityStateRpc), RpcTarget.All, pilotId, true, duration, breachShots);
    }

    [PunRPC]
    void SyncPilotActiveAbilityStateRpc(string pilotId, bool hasBeenUsed, float activeDuration, int breachShots)
    {
        activePilotId = PilotCatalog.NormalizePilotId(pilotId);
        used = hasBeenUsed;
        activeUntil = activeDuration > 0f ? Time.time + activeDuration : 0f;
        sirNowitzkyBreachShotsRemaining = Mathf.Max(0, breachShots);
    }

    [PunRPC]
    void SyncSirNowitzkyBreachShotsRpc(int remainingShots)
    {
        sirNowitzkyBreachShotsRemaining = Mathf.Max(0, remainingShots);
        if (sirNowitzkyBreachShotsRemaining <= 0 && IsSelectedPilot(PilotCatalog.SirNowitzkyId))
            activeUntil = 0f;
    }

    [PunRPC]
    void PlayJakeBarrierVfxRpc(float duration)
    {
        PilotBarrierVfx.Attach(gameObject, duration);
    }

    [PunRPC]
    void PlayNovaScavengerBurstVfxRpc(string targetViewIds, float duration)
    {
        NovaScavengerBurstVfx.Spawn(photonView != null ? photonView.ViewID : 0, targetViewIds, duration);
    }

    [PunRPC]
    void ApplyRoburMarkRpc(int targetViewId, float duration)
    {
        if (targetViewId <= 0 || duration <= 0f)
            return;

        RoburMarkedTargets[targetViewId] = Time.time + duration;
        RoburMarkAuraVfx.Attach(targetViewId, duration);
    }

    [PunRPC]
    void ApplyRobyBoosterRefillRpc()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.RefillBooster();
    }

    [PunRPC]
    void PlayCharlieConfusionWaveRpc(float radius)
    {
        ConfusionWaveVfx.Spawn(transform.position, radius, CharlieConfusionDuration);
    }

    [PunRPC]
    void PlayCovaxElectromagneticWaveRpc(float originX, float originY, float directionX, float directionY, float range, float coneAngle)
    {
        ElectromagneticConeWaveVfx.Spawn(new Vector3(originX, originY, transform.position.z), new Vector2(directionX, directionY), range, coneAngle);
    }

    [PunRPC]
    void ApplyAshSuperchargeRpc(float duration)
    {
        AshSuperchargeStatus.Apply(gameObject, duration);
    }

    PhotonView FindNearestRoburMarkTarget()
    {
        PhotonView bestView = null;
        float bestDistanceSq = float.MaxValue;
        Vector3 origin = transform.position;
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsValidRoburMarkTarget(candidate))
                continue;

            float distanceSq = (candidate.transform.position - origin).sqrMagnitude;
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestView = candidate.photonView;
            }
        }

        return bestView;
    }

    bool IsValidRoburMarkTarget(PlayerHealth candidate)
    {
        if (candidate == null || candidate.photonView == null || candidate.photonView == photonView || candidate.IsWreck || candidate.CurrentHP <= 0)
            return false;

        EnemyBot bot = candidate.GetComponent<EnemyBot>();
        if (bot != null)
            return bot.CanReceivePilotHostileEffect();

        if (candidate.IsAstronautControlled)
            return false;

        return candidate.photonView.Owner != null &&
               photonView.Owner != null &&
               candidate.photonView.Owner.ActorNumber != photonView.Owner.ActorNumber;
    }

    string ApplyCharlieConfusionOnMaster()
    {
        if (!PhotonNetwork.IsMasterClient)
            return string.Empty;

        List<int> targetIds = new List<int>();
        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        float radiusSq = CharlieConfusionRadius * CharlieConfusionRadius;
        Vector3 origin = transform.position;
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot bot = bots[i];
            if (bot == null || !bot.CanReceivePilotHostileEffect())
                continue;

            if ((bot.transform.position - origin).sqrMagnitude > radiusSq)
                continue;

            PhotonView botView = bot.photonView;
            if (botView == null)
                continue;

            targetIds.Add(botView.ViewID);
            botView.RPC(nameof(EnemyBot.ApplyConfusionRpc), RpcTarget.All, CharlieConfusionDuration);
        }

        return BuildViewIdList(targetIds);
    }

    void ApplyCovaxElectromagneticWaveOnMaster(Vector3 origin, Vector2 forward)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        Vector2 normalizedForward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
        float rangeSq = CovaxElectromagneticRange * CovaxElectromagneticRange;
        float minDot = Mathf.Cos((CovaxElectromagneticConeAngle * 0.5f) * Mathf.Deg2Rad);
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsValidCovaxElectromagneticTarget(candidate))
                continue;

            Vector2 toTarget = (Vector2)(candidate.transform.position - origin);
            float distanceSq = toTarget.sqrMagnitude;
            if (distanceSq <= 0.0001f || distanceSq > rangeSq)
                continue;

            Vector2 targetDirection = toTarget.normalized;
            if (Vector2.Dot(normalizedForward, targetDirection) < minDot)
                continue;

            candidate.photonView.RPC(
                nameof(PlayerHealth.ApplyElectromagneticShockRpc),
                RpcTarget.All,
                CovaxElectromagneticDuration,
                CovaxElectromagneticSpeedMultiplier,
                CovaxElectromagneticFireIntervalMultiplier);
        }
    }

    bool IsValidCovaxElectromagneticTarget(PlayerHealth candidate)
    {
        if (candidate == null || candidate.photonView == null || candidate.photonView == photonView || candidate.IsWreck || candidate.CurrentHP <= 0)
            return false;

        if (candidate.IsAstronautControlled || candidate.IsEvacuationAnimating)
            return false;

        EnemyBot bot = candidate.GetComponent<EnemyBot>();
        if (bot != null)
            return bot.CanReceivePilotHostileEffect();

        return candidate.photonView.Owner != null &&
               photonView.Owner != null &&
               candidate.photonView.Owner.ActorNumber != photonView.Owner.ActorNumber;
    }

    Vector2 ResolveForwardDirection()
    {
        Vector2 forward = transform.up;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
    }

    Vector3 GetShipTipPosition(Vector2 forward)
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        float offset = 0.85f;
        if (spriteRenderer != null)
            offset = Mathf.Max(0.72f, Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y) * 0.88f);

        Vector2 direction = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
        return transform.position + (Vector3)(direction * offset);
    }

    string ResolvePilotId()
    {
        string fallback = PilotCatalog.JakeId;
        if (PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null)
            fallback = PlayerProfileService.Instance.CurrentProfile.SelectedPilotId;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return RoomSettings.GetPlayerPilotId(owner, fallback);
    }

    bool IsSelectedPilot(string pilotId)
    {
        return string.Equals(activePilotId, PilotCatalog.NormalizePilotId(pilotId), StringComparison.Ordinal) ||
               PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, pilotId);
    }

    bool AreShipControlsBlocked()
    {
        if (GetComponent<EnemyBot>() != null)
            return false;

        if (AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null) ||
            GetComponent<AstronautSurvivor>() != null)
        {
            return true;
        }

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && (health.IsWreck || health.IsAstronautControlled || health.IsEvacuationAnimating || health.CurrentHP <= 0))
            return true;

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        return repairDocking != null && repairDocking.IsBusy;
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    static string BuildViewIdList(List<int> viewIds)
    {
        if (viewIds == null || viewIds.Count == 0)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < viewIds.Count; i++)
        {
            if (viewIds[i] <= 0)
                continue;

            if (builder.Length > 0)
                builder.Append(',');

            builder.Append(viewIds[i]);
        }

        return builder.ToString();
    }

    static void CleanupExpiredRoburMarks()
    {
        if (RoburMarkedTargets.Count == 0)
            return;

        List<int> expired = null;
        foreach (KeyValuePair<int, float> pair in RoburMarkedTargets)
        {
            if (Time.time < pair.Value)
                continue;

            if (expired == null)
                expired = new List<int>();

            expired.Add(pair.Key);
        }

        if (expired == null)
            return;

        for (int i = 0; i < expired.Count; i++)
            RoburMarkedTargets.Remove(expired[i]);
    }
}
