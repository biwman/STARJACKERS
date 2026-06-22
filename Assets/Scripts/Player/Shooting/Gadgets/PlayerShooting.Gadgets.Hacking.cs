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
    bool HasHackingDeviceCandidate()
    {
        return FindClosestHackingDeviceTarget(HackingDeviceRange) != null;
    }

    bool TryStartHackingDevice(string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            !IsGameStarted() ||
            !string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal) ||
            photonView == null ||
            authoritativeHackingDeviceRoutine != null)
        {
            return false;
        }

        PhotonView targetView = FindClosestHackingDeviceTarget(HackingDeviceRange);
        if (targetView == null)
            return false;

        activeHackingDeviceTargetViewId = targetView.ViewID;
        activeHackingDeviceItemId = itemId;
        photonView.RPC(nameof(StartHackingDeviceEffects), RpcTarget.All, photonView.ViewID, targetView.ViewID, HackingDeviceWindupDuration);
        authoritativeHackingDeviceRoutine = StartCoroutine(HackingDeviceRoutine(targetView.ViewID));
        return true;
    }

    IEnumerator HackingDeviceRoutine(int targetViewId)
    {
        float startedAt = Time.time;
        WaitForSeconds wait = new WaitForSeconds(HackingDeviceValidationInterval);
        while (Time.time - startedAt < HackingDeviceWindupDuration)
        {
            PhotonView targetView = PhotonView.Find(targetViewId);
            PlayerHealth targetHealth = targetView != null ? targetView.GetComponent<PlayerHealth>() : null;
            if (!IsValidHackingDeviceTarget(targetHealth, HackingDeviceKeepAliveRange, true))
                break;

            yield return wait;
        }

        bool completed = Time.time - startedAt >= HackingDeviceWindupDuration;
        authoritativeHackingDeviceRoutine = null;

        PhotonView completedTargetView = PhotonView.Find(targetViewId);
        PlayerHealth completedTargetHealth = completedTargetView != null ? completedTargetView.GetComponent<PlayerHealth>() : null;
        if (completed && IsValidHackingDeviceTarget(completedTargetHealth, HackingDeviceKeepAliveRange, true))
        {
            ApplyCompletedHackingDeviceEffect(completedTargetView);
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, InventoryItemCatalog.HackingDeviceId);
            NotifyPathfinderHackCompleted(completedTargetView);
        }

        StopAuthoritativeHackingDevice(true);
    }

    void ApplyCompletedHackingDeviceEffect(PhotonView targetView)
    {
        if (!PhotonNetwork.IsMasterClient || targetView == null)
            return;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null || targetHealth.IsWreck)
            return;

        float duration = ResolveHackingDeviceEffectDuration(targetHealth);
        int sourceViewId = photonView != null ? photonView.ViewID : 0;

        PlayerShooting targetShooting = targetView.GetComponent<PlayerShooting>();
        if (targetShooting != null)
            targetView.RPC(nameof(ApplyHackedStatusRpc), RpcTarget.All, duration, sourceViewId);

        EnemyBot targetBot = targetView.GetComponent<EnemyBot>();
        if (targetBot != null)
            targetView.RPC(nameof(EnemyBot.ApplyConfusionRpc), RpcTarget.All, duration);
    }

    void NotifyPathfinderHackCompleted(PhotonView targetView)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null || targetView == null)
            return;

        string hackedTypeId = ResolvePathfinderHackedShipTypeId(targetView);
        if (string.IsNullOrWhiteSpace(hackedTypeId))
            return;

        photonView.RPC(nameof(RecordPathfinderHackProgressRpc), photonView.Owner, hackedTypeId);
    }

    string ResolvePathfinderHackedShipTypeId(PhotonView targetView)
    {
        if (targetView == null)
            return string.Empty;

        EnemyBot targetBot = targetView.GetComponent<EnemyBot>();
        if (targetBot != null)
            return PlayerProfileService.BuildPathfinderEnemyHackTypeId(targetBot.Kind);

        NeutralRiderController neutralRider = targetView.GetComponent<NeutralRiderController>();
        if (neutralRider != null)
            return PlayerProfileService.BuildPathfinderPlayerHackTypeId(ShipCatalog.GetShipTypeFromSkinIndex(neutralRider.ShipSkinIndex));

        if (targetView.Owner != null)
        {
            int shipSkinIndex = RoomSettings.GetPlayerShipSkin(targetView.Owner, ShipCatalog.ExplorerBasicSkinIndex);
            return PlayerProfileService.BuildPathfinderPlayerHackTypeId(ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex));
        }

        return string.Empty;
    }

    [PunRPC]
    async void RecordPathfinderHackProgressRpc(string hackedTypeId)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(hackedTypeId) || !PlayerProfileService.HasInstance)
            return;

        try
        {
            PathfinderHackRecordResult result = await PlayerProfileService.Instance.RecordPathfinderHackedShipTypeAsync(hackedTypeId);
            if (result == null || (!result.Started && !result.Added && !result.DocumentationReady && !result.InventoryFull))
                return;

            if (result.InventoryFull)
            {
                RoundAnnouncementUI.Show("Ship Prototype Documentation ready - make room in inventory.", 3.2f);
                return;
            }

            if (result.DocumentationReady && result.DocumentationCreated)
            {
                RoundAnnouncementUI.Show("Ship Prototype Documentation acquired.", 3.2f);
                return;
            }

            if (result.Started)
            {
                RoundAnnouncementUI.Show("Documentation copied - Pathfinder project initiated. Hack more data from different ship types", 4f);
                return;
            }

            if (result.Added)
                RoundAnnouncementUI.Show("Pathfinder ship data copied: " + result.Count + "/" + PlayerProfileService.PathfinderHackedShipTypesRequired, 2.8f);
        }
        catch (Exception ex)
        {
            Debug.LogError("Pathfinder hack progress failed: " + ex);
        }
    }

    float ResolveHackingDeviceEffectDuration(PlayerHealth targetHealth)
    {
        EnemyBot targetBot = targetHealth != null ? targetHealth.GetComponent<EnemyBot>() : null;
        if (targetBot == null)
            return HackingDevicePlayerDuration;

        float size = Mathf.Max(0.6f, targetBot.VisualTargetSize);
        float sizeT = Mathf.InverseLerp(0.82f, 5.6f, size);
        return Mathf.Lerp(HackingDeviceComputerMaxDuration, HackingDeviceComputerMinDuration, sizeT);
    }

    void StopAuthoritativeHackingDevice(bool notifyClients)
    {
        if (authoritativeHackingDeviceRoutine != null)
        {
            StopCoroutine(authoritativeHackingDeviceRoutine);
            authoritativeHackingDeviceRoutine = null;
        }

        if (notifyClients && photonView != null)
            photonView.RPC(nameof(StopHackingDeviceEffects), RpcTarget.All, photonView.ViewID);

        activeHackingDeviceTargetViewId = 0;
        activeHackingDeviceItemId = null;
    }

    [PunRPC]
    void RequestCancelHackingDevice(PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null || messageInfo.Sender == null)
            return;

        if (messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        StopAuthoritativeHackingDevice(true);
    }

    [PunRPC]
    void StartHackingDeviceEffects(int sourceViewId, int targetViewId, float windupDuration)
    {
        AudioManager.Instance.PlayHackingDeviceAt(transform.position);
        HackingBeamVfx.StartBeam(sourceViewId, targetViewId, windupDuration);
    }

    [PunRPC]
    void StopHackingDeviceEffects(int sourceViewId)
    {
        HackingBeamVfx.StopBeam(sourceViewId);
    }

    [PunRPC]
    void ApplyHackedStatusRpc(float duration, int sourceViewId)
    {
        AudioManager.Instance.PlayHackingDeviceAt(transform.position);
        HackingStatus.Attach(gameObject, duration, sourceViewId);
    }

    PhotonView FindClosestHackingDeviceTarget(float maxRange)
    {
        Vector2 sourcePosition = transform.position;
        PhotonView bestView = null;
        float bestDistance = float.MaxValue;

        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsValidHackingDeviceTarget(candidate, maxRange, true))
                continue;

            float distance = GetHackingDeviceDistance(candidate, sourcePosition);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestView = candidate.photonView;
        }

        return bestView;
    }

    bool IsValidHackingDeviceTarget(PlayerHealth candidate, float maxRange, bool requireTargetBlind)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate == GetComponent<PlayerHealth>())
            return false;

        if (candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.CurrentHP <= 0)
            return false;

        PhotonView candidateView = candidate.photonView;
        if (candidateView == null || candidateView == photonView)
            return false;

        if (candidate.GetComponent<PlayerDeployableBase>() != null || candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        ActorIdentity identity = ActorIdentity.Ensure(candidate.gameObject);
        if (identity == null || !identity.IsShip)
            return false;

        if (!candidate.IsBotControlled && !candidate.IsNeutralRiderControlled && !candidate.IsHumanShipControlled)
            return false;

        if (candidate.IsHumanShipControlled &&
            photonView != null &&
            photonView.Owner != null &&
            candidateView.Owner != null &&
            candidateView.Owner.ActorNumber == photonView.Owner.ActorNumber)
        {
            return false;
        }

        EnemyBot targetBot = candidate.GetComponent<EnemyBot>();
        if (targetBot != null && !targetBot.CanReceivePilotHostileEffect())
            return false;

        if (HackingStatus.IsActiveOn(candidate.gameObject))
            return false;

        if (GetHackingDeviceDistance(candidate, transform.position) > maxRange)
            return false;

        return !requireTargetBlind || IsHackingTargetUnableToSeeSource(candidate);
    }

    float GetHackingDeviceDistance(PlayerHealth candidate, Vector2 sourcePosition)
    {
        if (candidate == null)
            return float.MaxValue;

        Collider2D collider = candidate.GetComponent<Collider2D>();
        if (collider == null)
            collider = candidate.GetComponentInChildren<Collider2D>();

        Vector2 targetPoint = collider != null ? collider.ClosestPoint(sourcePosition) : (Vector2)candidate.transform.position;
        return Vector2.Distance(sourcePosition, targetPoint);
    }

    bool IsHackingTargetUnableToSeeSource(PlayerHealth target)
    {
        return IsSourceHiddenFromHackingTarget(target) || IsSourceBehindHackingTarget(target);
    }

    bool IsSourceHiddenFromHackingTarget(PlayerHealth target)
    {
        HideInNebulaTarget sourceNebulaState = GetComponent<HideInNebulaTarget>();
        if (sourceNebulaState == null)
            return false;

        HideInNebulaTarget targetNebulaState = target != null ? target.GetComponent<HideInNebulaTarget>() : null;
        return sourceNebulaState.IsHiddenFromObserver(targetNebulaState);
    }

    bool IsSourceBehindHackingTarget(PlayerHealth target)
    {
        if (target == null)
            return false;

        Vector2 toSource = (Vector2)transform.position - (Vector2)target.transform.position;
        if (toSource.sqrMagnitude <= 0.001f)
            return false;

        Vector2 targetForward = ResolveHackingTargetForward(target);
        if (targetForward.sqrMagnitude <= 0.001f)
            return false;

        return Vector2.Dot(targetForward.normalized, toSource.normalized) <= HackingDeviceBehindDotThreshold;
    }

    Vector2 ResolveHackingTargetForward(PlayerHealth target)
    {
        if (target == null)
            return Vector2.up;

        if (target.GetComponent<EnemyBot>() != null)
            return -target.transform.up;

        return target.transform.up;
    }
}
