using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class LootingFriendController : MonoBehaviourPun
{
    const float FollowRightOffset = 0.72f;
    const float FollowBackOffset = -0.05f;
    const float CollectDuration = 6f;
    const float ScanInterval = 0.18f;
    const float CollectRangeMultiplier = 1.3f;
    const float VisualTargetWorldSize = GameVisualTheme.PlayerTargetSize * 0.22f;
    const float EquipmentRefreshInterval = 0.5f;

    PlayerHealth health;
    TreasureCollector collector;
    SpriteRenderer visualRenderer;
    LineRenderer beam;
    AudioSource collectAudioSource;
    Coroutine collectRoutine;
    PhotonView currentTarget;
    float nextScanTime;
    bool visualActive;
    bool forcedForNeutralRider;
    bool cachedEquipped;
    float nextEquipmentRefreshTime;

    float CollectRange => Treasure.CollectRange * CollectRangeMultiplier;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        collector = GetComponent<TreasureCollector>();
        EnsureVisual();
        SetupCollectAudio();
    }

    void Update()
    {
        if (!CanLootingFriendRun())
        {
            SetVisualActive(false);
            StopCollecting();
            return;
        }

        bool equipped = IsLootingFriendEquipped();
        SetVisualActive(equipped);
        if (!equipped)
        {
            StopCollecting();
            return;
        }

        UpdateVisualTransform();
        UpdateBeam();
        if (currentTarget != null && beam != null && beam.enabled)
            SetCollectAudio(true);

        if (forcedForNeutralRider)
            return;

        if (!photonView.IsMine)
            return;

        if (collector == null)
            collector = GetComponent<TreasureCollector>();

        if (collector != null && collector.IsCollectingAny)
        {
            StopCollecting();
            return;
        }

        if (collectRoutine != null || Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + ScanInterval;
        PhotonView target = FindAutoLootTarget();
        if (target != null)
            collectRoutine = StartCoroutine(CollectRoutine(target));
    }

    public void DeactivateForShipLoss()
    {
        StopCollecting();
        SetVisualActive(false);
        enabled = false;
    }

    public void SetForcedForNeutralRider(bool forced)
    {
        forcedForNeutralRider = forced;
        if (forced)
            enabled = true;
        else
            StopCollecting();
    }

    public void SetNeutralRiderCollectFx(int targetViewId, bool active)
    {
        SetLootingFriendCollectFx(targetViewId, active);
    }

    bool CanLootingFriendRun()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();

        return health != null &&
               health.isActiveAndEnabled &&
               !health.IsWreck &&
               !health.IsEvacuationAnimating &&
               !health.IsAstronautControlled;
    }

    bool IsLootingFriendEquipped()
    {
        if (forcedForNeutralRider)
            return true;

        if (Time.unscaledTime < nextEquipmentRefreshTime)
            return cachedEquipped;

        nextEquipmentRefreshTime = Time.unscaledTime + EquipmentRefreshInterval;
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipment = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        cachedEquipped = InventoryItemCatalog.HasEquippedItem(equipment, shipSkinIndex, InventoryItemCatalog.LootingFriendId);
        return cachedEquipped;
    }

    PhotonView FindAutoLootTarget()
    {
        if (!CanLootingFriendRun())
            return null;

        if (!PlayerProfileService.Instance.HasFreeShipInventorySlot())
            return null;

        Vector2 origin = transform.position;
        PhotonView best = null;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = RuntimeSceneQueryCache.GetTreasures();
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected || InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
                continue;

            string itemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
            if (!PlayerProfileService.Instance.HasFreeShipInventorySlot(itemId))
                continue;

            ConsiderTarget(treasure.GetComponent<PhotonView>(), treasure.transform.position, origin, ref best, ref bestDistance);
        }

        ShipWreck[] wrecks = RuntimeSceneQueryCache.GetShipWrecks();
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            string itemId = wreck.GetLootItemAt(wreck.GetFirstLootIndex());
            if (!PlayerProfileService.Instance.HasFreeShipInventorySlot(itemId))
                continue;

            ConsiderTarget(wreck.GetComponent<PhotonView>(), wreck.transform.position, origin, ref best, ref bestDistance);
        }

        DroppedCargoCrate[] crates = RuntimeSceneQueryCache.GetDroppedCargoCrates();
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            PhotonView crateView = crate.GetComponent<PhotonView>();
            if (crateView != null && crateView.CreatorActorNr == photonView.OwnerActorNr)
                continue;

            if (!PlayerProfileService.Instance.HasFreeShipInventorySlot(crate.StoredItemId))
                continue;

            ConsiderTarget(crateView, crate.transform.position, origin, ref best, ref bestDistance);
        }

        return best;
    }

    void ConsiderTarget(PhotonView view, Vector2 position, Vector2 origin, ref PhotonView best, ref float bestDistance)
    {
        if (view == null)
            return;

        float distance = Vector2.Distance(origin, position);
        if (distance > CollectRange || distance >= bestDistance)
            return;

        bestDistance = distance;
        best = view;
    }

    IEnumerator CollectRoutine(PhotonView target)
    {
        if (!CanLootingFriendRun())
        {
            collectRoutine = null;
            yield break;
        }

        int targetViewId = target != null ? target.ViewID : 0;
        SetLootingFriendCollectFx(targetViewId, true);
        float startedAt = Time.time;
        while (Time.time < startedAt + CollectDuration)
        {
            if (!CanLootingFriendRun())
                break;

            if (collector == null)
                collector = GetComponent<TreasureCollector>();

            if (collector != null && collector.IsCollectingAny)
                break;

            if (!IsTargetStillCollectible(target))
                break;

            yield return null;
        }

        bool completed = target != null && Time.time >= startedAt + CollectDuration && IsTargetStillCollectible(target);
        SetLootingFriendCollectFx(targetViewId, false);
        collectRoutine = null;

        if (completed)
            RequestLoot(target);
    }

    bool IsTargetStillCollectible(PhotonView target)
    {
        if (target == null)
            return false;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        if (distance > CollectRange)
            return false;

        Treasure treasure = target.GetComponent<Treasure>();
        if (treasure != null)
            return !treasure.isBeingCollected && PlayerProfileService.Instance.HasFreeShipInventorySlot(treasure.itemId);

        ShipWreck wreck = target.GetComponent<ShipWreck>();
        if (wreck != null)
            return wreck.HasLoot && !wreck.isBeingCollected && PlayerProfileService.Instance.HasFreeShipInventorySlot(wreck.GetLootItemAt(wreck.GetFirstLootIndex()));

        DroppedCargoCrate crate = target.GetComponent<DroppedCargoCrate>();
        if (crate != null)
        {
            if (target.CreatorActorNr == photonView.OwnerActorNr)
                return false;

            return crate.HasLoot && !crate.isBeingCollected && PlayerProfileService.Instance.HasFreeShipInventorySlot(crate.StoredItemId);
        }

        return false;
    }

    void RequestLoot(PhotonView target)
    {
        if (target == null || photonView == null || !PhotonNetwork.InRoom || !CanLootingFriendRun())
            return;

        if (target.GetComponent<Treasure>() != null)
            photonView.RPC(nameof(RequestLootingFriendTreasureRpc), RpcTarget.MasterClient, target.ViewID);
        else if (target.GetComponent<ShipWreck>() != null)
            photonView.RPC(nameof(RequestLootingFriendWreckRpc), RpcTarget.MasterClient, target.ViewID);
        else if (target.GetComponent<DroppedCargoCrate>() != null)
            photonView.RPC(nameof(RequestLootingFriendCrateRpc), RpcTarget.MasterClient, target.ViewID);
    }

    [PunRPC]
    void RequestLootingFriendTreasureRpc(int targetViewId)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || !CanLootingFriendRun())
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (treasure == null || treasure.isBeingCollected || InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
            return;

        string itemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return;

        photonView.RPC(nameof(ReceiveLootingFriendItemRpc), photonView.Owner, targetViewId, itemId, true, -1);
    }

    [PunRPC]
    void RequestLootingFriendWreckRpc(int targetViewId)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || !CanLootingFriendRun())
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        ShipWreck wreck = target != null ? target.GetComponent<ShipWreck>() : null;
        if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
            return;

        int lootIndex = wreck.GetFirstLootIndex();
        string itemId = wreck.GetLootItemAt(lootIndex);
        if (lootIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return;

        photonView.RPC(nameof(ReceiveLootingFriendItemRpc), photonView.Owner, targetViewId, itemId, false, lootIndex);
    }

    [PunRPC]
    void RequestLootingFriendCrateRpc(int targetViewId)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || !CanLootingFriendRun())
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        DroppedCargoCrate crate = target != null ? target.GetComponent<DroppedCargoCrate>() : null;
        if (crate == null || !crate.HasLoot || crate.isBeingCollected || target.CreatorActorNr == photonView.OwnerActorNr)
            return;

        string itemId = crate.StoredItemId;
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return;

        photonView.RPC(nameof(ReceiveLootingFriendCrateItemRpc), photonView.Owner, targetViewId, itemId);
    }

    [PunRPC]
    async void ReceiveLootingFriendItemRpc(int targetViewId, string itemId, bool treasure, int lootIndex)
    {
        if (!CanLootingFriendRun())
        {
            if (photonView != null)
                photonView.RPC(nameof(ResolveLootingFriendLootRpc), RpcTarget.MasterClient, targetViewId, itemId, treasure, lootIndex, false);
            return;
        }

        bool stored = false;
        try
        {
            string storedItemId = BlueprintCatalog.ResolveContainerBlueprintDrop(
                itemId,
                PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null
                    ? PlayerProfileService.Instance.CurrentProfile.UnlockedBlueprintIds
                    : new string[0]);
            stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(storedItemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Looting Friend failed to store item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveLootingFriendLootRpc), RpcTarget.MasterClient, targetViewId, itemId, treasure, lootIndex, stored);
    }

    [PunRPC]
    void ResolveLootingFriendLootRpc(int targetViewId, string itemId, bool treasure, int lootIndex, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient || !stored || !CanLootingFriendRun())
            return;

        SpaceTrapTarget.DetonateIfArmed(targetViewId, photonView != null ? photonView.ViewID : 0);
        PhotonView target = PhotonView.Find(targetViewId);
        if (target == null)
            return;

        if (treasure)
        {
            Treasure treasureComponent = target.GetComponent<Treasure>();
            string resolvedTreasureItemId = treasureComponent != null
                ? InventoryItemCatalog.ResolveAlienSecretItemId(treasureComponent.itemId, treasureComponent.visualVariantIndex)
                : null;
            if (treasureComponent != null && string.Equals(resolvedTreasureItemId, itemId, StringComparison.Ordinal))
                PhotonNetwork.Destroy(target.gameObject);
            return;
        }

        ShipWreck wreck = target.GetComponent<ShipWreck>();
        if (wreck != null && wreck.HasLoot && string.Equals(wreck.GetLootItemAt(lootIndex), itemId, StringComparison.Ordinal))
            target.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
    }

    [PunRPC]
    async void ReceiveLootingFriendCrateItemRpc(int targetViewId, string itemId)
    {
        if (!CanLootingFriendRun())
        {
            if (photonView != null)
                photonView.RPC(nameof(ResolveLootingFriendCrateLootRpc), RpcTarget.MasterClient, targetViewId, itemId, false);
            return;
        }

        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Looting Friend failed to store crate item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveLootingFriendCrateLootRpc), RpcTarget.MasterClient, targetViewId, itemId, stored);
    }

    [PunRPC]
    void ResolveLootingFriendCrateLootRpc(int targetViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient || !stored || !CanLootingFriendRun())
            return;

        SpaceTrapTarget.DetonateIfArmed(targetViewId, photonView != null ? photonView.ViewID : 0);
        PhotonView target = PhotonView.Find(targetViewId);
        DroppedCargoCrate crate = target != null ? target.GetComponent<DroppedCargoCrate>() : null;
        if (crate == null || !crate.HasLoot || !string.Equals(crate.StoredItemId, itemId, StringComparison.Ordinal))
            return;

        target.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
        PhotonNetwork.Destroy(target.gameObject);
    }

    void StopCollecting()
    {
        if (collectRoutine != null)
        {
            StopCoroutine(collectRoutine);
            collectRoutine = null;
        }

        if (photonView != null && photonView.IsMine && currentTarget != null)
            SetLootingFriendCollectFx(currentTarget.ViewID, false);

        currentTarget = null;
        SetBeamEnabled(false);
        SetCollectAudio(false);
    }

    void SetLootingFriendCollectFx(int targetViewId, bool active)
    {
        if (photonView != null && PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(SetLootingFriendCollectFxRpc), RpcTarget.All, targetViewId, active);
            return;
        }

        SetLootingFriendCollectFxRpc(targetViewId, active);
    }

    void EnsureVisual()
    {
        if (visualRenderer != null)
            return;

        GameObject visual = new GameObject("LootingFriendVisual");
        visual.transform.SetParent(transform, false);
        visualRenderer = visual.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = RuntimeSpriteUtility.LoadSprite("looting_friend_top_down_resource", "Assets/looting_friend_top_down.png");
        visualRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        visualRenderer.sortingOrder = 48;
        visualRenderer.color = Color.white;
        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);
        visualActive = true;
        SetupCollectAudio();
    }

    void SetVisualActive(bool active)
    {
        EnsureVisual();
        if (visualRenderer != null)
            visualRenderer.enabled = active;
        visualActive = active;
        if (!active)
        {
            SetBeamEnabled(false);
            SetCollectAudio(false);
        }
    }

    void UpdateVisualTransform()
    {
        if (visualRenderer == null || !visualActive)
            return;

        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);
        visualRenderer.transform.position = transform.position + transform.right * FollowRightOffset + transform.up * FollowBackOffset;
        visualRenderer.transform.rotation = transform.rotation;
    }

    void SetBeamEnabled(bool active)
    {
        if (active)
            EnsureBeam();

        if (beam != null)
            beam.enabled = active;
    }

    [PunRPC]
    void SetLootingFriendCollectFxRpc(int targetViewId, bool active)
    {
        EnsureVisual();
        PhotonView target = active && targetViewId > 0 ? PhotonView.Find(targetViewId) : null;
        currentTarget = target;
        SetBeamEnabled(active && target != null);
        SetCollectAudio(active && target != null);
    }

    void SetupCollectAudio()
    {
        if (collectAudioSource != null || visualRenderer == null || AudioManager.Instance.LootingFriendDrillClip == null)
            return;

        collectAudioSource = visualRenderer.gameObject.AddComponent<AudioSource>();
        collectAudioSource.clip = AudioManager.Instance.LootingFriendDrillClip;
        collectAudioSource.loop = true;
        collectAudioSource.playOnAwake = false;
        AudioManager.Instance.ConfigureSpatialSource(collectAudioSource, 0.42f);
    }

    void SetCollectAudio(bool active)
    {
        if (active)
            SetupCollectAudio();

        if (collectAudioSource == null || collectAudioSource.clip == null)
            return;

        if (active)
        {
            if (!collectAudioSource.isPlaying)
                collectAudioSource.Play();
        }
        else if (collectAudioSource.isPlaying)
        {
            collectAudioSource.Stop();
        }
    }

    void EnsureBeam()
    {
        if (beam != null)
            return;

        GameObject beamObject = new GameObject("LootingFriendBeam");
        beamObject.transform.SetParent(transform, false);
        beam = beamObject.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 11;
        beam.widthMultiplier = 0.07f;
        beam.numCapVertices = 8;
        beam.material = new Material(Shader.Find("Sprites/Default"));
        beam.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        beam.sortingOrder = 75;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.78f, 0.08f), 0f), new GradientColorKey(new Color(1f, 0.98f, 0.5f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0f, 1f) });
        beam.colorGradient = gradient;
    }

    void UpdateBeam()
    {
        if (beam == null || !beam.enabled)
            return;

        if (currentTarget == null || visualRenderer == null)
        {
            SetBeamEnabled(false);
            SetCollectAudio(false);
            return;
        }

        Vector2 start = visualRenderer.transform.position;
        Vector2 end = currentTarget.transform.position;
        Vector2 direction = end - start;
        Vector2 perpendicular = direction.sqrMagnitude > 0.001f ? new Vector2(-direction.y, direction.x).normalized : Vector2.right;
        for (int i = 0; i < beam.positionCount; i++)
        {
            float t = i / (float)(beam.positionCount - 1);
            float wave = Mathf.Sin(Time.time * 11f + t * Mathf.PI * 6f) * 0.045f;
            Vector2 point = Vector2.Lerp(start, end, t) + perpendicular * wave;
            beam.SetPosition(i, new Vector3(point.x, point.y, -0.34f));
        }
    }
}
