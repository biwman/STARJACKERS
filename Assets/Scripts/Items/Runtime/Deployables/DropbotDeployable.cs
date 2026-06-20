using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class DropbotDeployable : PlayerDeployableBase
{
    const float MoveSpeed = 5f / 3f;
    const float EscapeRange = 0.72f;
    const float DeliveryRetryDelay = 0.85f;
    const float RotationSpeedDegreesPerSecond = 760f;

    string heldItemId;
    Vector2 extractionPosition;
    bool deliveryRequested;
    float nextDeliveryAttemptTime;
    int deliveryRequestId;
    int pendingDeliveryRequestId;
    int pendingDeliveryActorNumber;
    string pendingDeliveryItemId;
    TrailRenderer engineTrail;

    protected override int MaxHp => 35;
    protected override int MaxShield => 0;
    protected override float VisualTargetSize => 0.72f;
    protected override float CollisionRadius => 0.32f;
    protected override string SpriteResourcePath => "dropbot_up_resource";
    protected override string EditorSpritePath => "Assets/Resources/dropbot_up_resource.png";
    public bool HasCargoInFlight => initialized && !destroyed && !string.IsNullOrWhiteSpace(heldItemId);

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
        heldItemId = data != null && data.Length > 2 ? data[2] as string : null;
        float targetX = data != null && data.Length > 3 ? ConvertToFloat(data[3]) : transform.position.x;
        float targetY = data != null && data.Length > 4 ? ConvertToFloat(data[4]) : transform.position.y;
        extractionPosition = new Vector2(targetX, targetY);
        InitializeCommon();
        ConfigureEngineTrail();
    }

    public static bool TryFindNearestExtraction(Vector2 origin, out Vector2 position)
    {
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        float bestDistance = float.MaxValue;
        position = Vector2.zero;
        bool found = false;
        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone == null)
                continue;

            float distance = Vector2.Distance(origin, zone.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            position = zone.transform.position;
            found = true;
        }

        return found;
    }

    void Update()
    {
        if (!initialized || destroyed)
            return;

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (string.IsNullOrWhiteSpace(heldItemId))
        {
            DespawnOnMaster();
            return;
        }

        MoveToward(extractionPosition);
        if (Vector2.Distance(transform.position, extractionPosition) <= EscapeRange)
            TryDeliverCargo();
    }

    void MoveToward(Vector2 target)
    {
        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, target, MoveSpeed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        Vector2 direction = target - current;
        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), RotationSpeedDegreesPerSecond * Time.deltaTime);
        }
    }

    void TryDeliverCargo()
    {
        if (deliveryRequested || Time.time < nextDeliveryAttemptTime)
            return;

        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null || ownerView.Owner == null)
        {
            DespawnOnMaster();
            return;
        }

        deliveryRequested = true;
        pendingDeliveryRequestId = ++deliveryRequestId;
        pendingDeliveryActorNumber = ownerView.Owner.ActorNumber;
        pendingDeliveryItemId = heldItemId;
        photonView.RPC(nameof(ReceiveDropbotRecoveredCargoRpc), ownerView.Owner, pendingDeliveryRequestId, heldItemId);
    }

    [PunRPC]
    async void ReceiveDropbotRecoveredCargoRpc(int requestId, string itemId)
    {
        PhotonView cachedView = photonView;
        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToPlayerDeferredSaveAsync(itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Dropbot cargo delivery failed: " + ex);
        }

        if (cachedView != null)
            cachedView.RPC(nameof(ResolveDropbotDeliveryRpc), RpcTarget.MasterClient, requestId, itemId, stored);
    }

    [PunRPC]
    void ResolveDropbotDeliveryRpc(int requestId, string itemId, bool stored, PhotonMessageInfo messageInfo)
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
            nextDeliveryAttemptTime = Time.time + DeliveryRetryDelay;
            ClearPendingDelivery();
            return;
        }

        heldItemId = null;
        Vector3 escapedAt = transform.position;
        photonView.RPC(nameof(PlayDropbotEscapedRpc), RpcTarget.All, escapedAt.x, escapedAt.y, escapedAt.z);
        ClearPendingDelivery();
        DespawnOnMaster();
    }

    void ClearPendingDelivery()
    {
        pendingDeliveryRequestId = 0;
        pendingDeliveryActorNumber = 0;
        pendingDeliveryItemId = null;
    }

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("DropbotEngineTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.24f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.34f;
        engineTrail.minVertexDistance = 0.025f;
        engineTrail.widthMultiplier = 0.075f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 31;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.25f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.1f, 0.38f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = gradient;
    }

    [PunRPC]
    void PlayDropbotEscapedRpc(float x, float y, float z)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayExtractionSequenceAt(new Vector3(x, y, z));
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
