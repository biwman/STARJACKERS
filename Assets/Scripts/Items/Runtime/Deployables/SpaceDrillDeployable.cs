using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class SpaceDrillDeployable : PlayerDeployableBase
{
    enum DrillState
    {
        ToTarget,
        Mining,
        Returning
    }

    const float MoveSpeed = 4.7f;
    const float ReturnSpeed = 5.3f;
    const float MiningRange = 0.72f;
    const float MiningDuration = 5f;
    const float DeliveryRange = 0.82f;

    DrillState state;
    int targetViewId;
    float miningStartedAt;
    string heldItemId;
    bool deliveryRequested;
    int deliveryRequestId;
    int pendingDeliveryRequestId;
    int pendingDeliveryActorNumber;
    string pendingDeliveryItemId;
    LineRenderer beam;
    TrailRenderer engineTrail;
    AudioSource miningAudioSource;

    protected override int MaxHp => 10;
    protected override int MaxShield => 20;
    protected override float VisualTargetSize => 0.62f;
    protected override float CollisionRadius => 0.27f;
    protected override string SpriteResourcePath => "space_drill_top_down_resource";
    protected override string EditorSpritePath => "Assets/space_drill_top_down.png";

    void Awake()
    {
        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Start()
    {
        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
        else
            enabled = false;
    }

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        object[] data = photonView != null ? photonView.InstantiationData : null;
        targetViewId = data != null && data.Length > 2 ? ConvertToInt(data[2]) : 0;
        InitializeCommon();
        ConfigureEngineTrail();
        state = DrillState.ToTarget;
    }

    public static PhotonView FindNearestLootableAsteroid(Vector2 origin)
    {
        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        PhotonView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected)
                continue;

            if (!IsLootableAsteroidItem(treasure.itemId))
                continue;

            PhotonView view = treasure.GetComponent<PhotonView>();
            if (view == null)
                continue;

            float distance = Vector2.Distance(origin, treasure.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = view;
        }

        return best;
    }

    static bool IsLootableAsteroidItem(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.AsteroidCommonId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidUncommonId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidRareId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidVeryRareId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidEpicId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidLegendaryId, StringComparison.Ordinal);
    }

    void Update()
    {
        if (!initialized || destroyed)
            return;

        UpdateBeamVisual();
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (state == DrillState.ToTarget)
            TickToTarget();
        else if (state == DrillState.Mining)
            TickMining();
        else
            TickReturning();
    }

    void TickToTarget()
    {
        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (target == null || treasure == null || treasure.isBeingCollected)
        {
            DespawnOnMaster();
            return;
        }

        MoveToward(target.transform.position, MoveSpeed);
        if (Vector2.Distance(transform.position, target.transform.position) <= MiningRange)
        {
            treasure.isBeingCollected = true;
            miningStartedAt = Time.time;
            state = DrillState.Mining;
            photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, true);
        }
    }

    void TickMining()
    {
        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (target == null || treasure == null)
        {
            photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, false);
            DespawnOnMaster();
            return;
        }

        if (Vector2.Distance(transform.position, target.transform.position) > Treasure.CollectRange + 0.35f)
        {
            treasure.isBeingCollected = false;
            photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, false);
            state = DrillState.ToTarget;
            return;
        }

        if (Time.time < miningStartedAt + MiningDuration)
            return;

        heldItemId = treasure.itemId;
        treasure.isBeingCollected = false;
        photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, false);
        SpaceTrapTarget.DetonateIfArmed(targetViewId, photonView != null ? photonView.ViewID : 0);
        PhotonNetwork.Destroy(target.gameObject);
        state = DrillState.Returning;
    }

    void TickReturning()
    {
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null || ownerView.Owner == null)
        {
            DespawnOnMaster();
            return;
        }

        MoveToward(ownerView.transform.position, ReturnSpeed);
        if (string.IsNullOrWhiteSpace(heldItemId))
        {
            DespawnOnMaster();
            return;
        }

        if (Vector2.Distance(transform.position, ownerView.transform.position) > DeliveryRange)
            return;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(ownerView.Owner, heldItemId))
            return;

        if (deliveryRequested)
            return;

        deliveryRequested = true;
        pendingDeliveryRequestId = ++deliveryRequestId;
        pendingDeliveryActorNumber = ownerView.Owner != null ? ownerView.Owner.ActorNumber : 0;
        pendingDeliveryItemId = heldItemId;
        photonView.RPC(nameof(ReceiveSpaceDrillLootRpc), ownerView.Owner, pendingDeliveryRequestId, heldItemId);
    }

    void MoveToward(Vector3 target, float speed)
    {
        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, target, Mathf.Max(0.1f, speed) * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        Vector2 direction = (Vector2)target - current;
        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), 720f * Time.deltaTime);
        }
    }

    [PunRPC]
    async void ReceiveSpaceDrillLootRpc(int requestId, string itemId)
    {
        PhotonView cachedView = photonView;
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
            Debug.LogError("Space Drill loot delivery failed: " + ex);
        }

        if (cachedView != null)
            cachedView.RPC(nameof(ResolveSpaceDrillDeliveryRpc), RpcTarget.MasterClient, requestId, itemId, stored);
    }

    [PunRPC]
    void ResolveSpaceDrillDeliveryRpc(int requestId, string itemId, bool stored, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!deliveryRequested ||
            requestId != pendingDeliveryRequestId ||
            messageInfo.Sender == null ||
            messageInfo.Sender.ActorNumber != pendingDeliveryActorNumber ||
            !string.Equals(itemId, pendingDeliveryItemId, StringComparison.Ordinal) ||
            !string.Equals(itemId, heldItemId, StringComparison.Ordinal))
        {
            return;
        }

        if (!stored)
        {
            deliveryRequested = false;
            ClearPendingDelivery();
            return;
        }

        heldItemId = null;
        ClearPendingDelivery();
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        BroadcastDeliverySound(ownerView, transform.position);
        SetMiningAudio(false);
        DespawnOnMaster();
    }

    void ClearPendingDelivery()
    {
        pendingDeliveryRequestId = 0;
        pendingDeliveryActorNumber = 0;
        pendingDeliveryItemId = null;
    }

    [PunRPC]
    void SetDrillBeamRpc(int targetId, bool active)
    {
        targetViewId = targetId;
        if (active)
        {
            EnsureBeam();
            SetMiningAudio(true);
        }
        else if (beam != null)
        {
            beam.enabled = false;
            SetMiningAudio(false);
        }
        else
        {
            SetMiningAudio(false);
        }
    }

    void EnsureBeam()
    {
        if (beam != null)
        {
            beam.enabled = true;
            return;
        }

        GameObject beamObject = new GameObject("SpaceDrillBeam");
        beamObject.transform.SetParent(transform, false);
        beam = beamObject.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 9;
        beam.widthMultiplier = 0.08f;
        beam.numCapVertices = 8;
        beam.material = new Material(Shader.Find("Sprites/Default"));
        beam.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        beam.sortingOrder = 76;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.86f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.96f, 0.55f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
        beam.colorGradient = gradient;
    }

    void UpdateBeamVisual()
    {
        if (beam == null || !beam.enabled)
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        if (target == null)
        {
            beam.enabled = false;
            return;
        }

        Vector2 start = transform.position;
        Vector2 end = target.transform.position;
        Vector2 direction = end - start;
        Vector2 perpendicular = direction.sqrMagnitude > 0.001f ? new Vector2(-direction.y, direction.x).normalized : Vector2.right;
        for (int i = 0; i < beam.positionCount; i++)
        {
            float t = i / (float)(beam.positionCount - 1);
            float wave = Mathf.Sin(Time.time * 13f + t * Mathf.PI * 5f) * 0.04f;
            Vector2 point = Vector2.Lerp(start, end, t) + perpendicular * wave;
            beam.SetPosition(i, new Vector3(point.x, point.y, -0.36f));
        }
    }

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("SpaceDrillYellowTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.22f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.42f;
        engineTrail.minVertexDistance = 0.025f;
        engineTrail.widthMultiplier = 0.08f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 30;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.82f, 0.18f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = gradient;
    }

    void SetupMiningAudio()
    {
        if (miningAudioSource != null || AudioManager.Instance.SpaceDrillMiningClip == null)
            return;

        GameObject audioObject = new GameObject("SpaceDrillMiningAudio");
        audioObject.transform.SetParent(transform, false);
        miningAudioSource = audioObject.AddComponent<AudioSource>();
        miningAudioSource.clip = AudioManager.Instance.SpaceDrillMiningClip;
        miningAudioSource.playOnAwake = false;
        AudioManager.Instance.ConfigureSpatialSource(miningAudioSource, 0.42f);
        miningAudioSource.loop = true;
    }

    void SetMiningAudio(bool active)
    {
        if (active)
            SetupMiningAudio();

        if (miningAudioSource == null || miningAudioSource.clip == null)
            return;

        if (active)
        {
            if (!miningAudioSource.isPlaying)
                miningAudioSource.Play();
        }
        else if (miningAudioSource.isPlaying)
        {
            miningAudioSource.Stop();
        }
    }

    void BroadcastDeliverySound(PhotonView ownerView, Vector3 position)
    {
        if (ownerView != null && ownerView.GetComponent<PlayerShooting>() != null)
        {
            ownerView.RPC("PlaySpaceDrillDeliverySoundRpc", RpcTarget.All, position.x, position.y, position.z);
            return;
        }

        AudioManager.Instance.PlaySpaceDrillDeliveryAt(position);
    }

    protected override void OnDestroyedByDamage()
    {
        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (treasure != null && string.IsNullOrWhiteSpace(heldItemId))
            treasure.isBeingCollected = false;
    }

    protected override void OnDestroy()
    {
        SetMiningAudio(false);
        base.OnDestroy();
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        PlayDeployableDestroyedFeedback();
    }
}
