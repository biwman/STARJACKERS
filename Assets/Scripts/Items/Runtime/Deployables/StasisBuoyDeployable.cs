using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class StasisBuoyDeployable : PlayerDeployableBase
{
    const float Lifetime = 12.75f;
    const float PulseRadius = 4.25f;
    const float PulseInterval = 0.58f;
    const float ShockDuration = 0.85f;
    const float ShockSpeedMultiplier = 0.38f;
    const float ShockFireIntervalMultiplier = 1.75f;
    static readonly Collider2D[] PulseHits = new Collider2D[96];

    float deployedAt;
    float nextPulseTime;
    LineRenderer outerRing;
    LineRenderer innerRing;

    protected override int MaxHp => 55;
    protected override int MaxShield => 25;
    protected override float VisualTargetSize => 0.88f;
    protected override float CollisionRadius => 0.42f;
    protected override string SpriteResourcePath => "Items/stasis_buoy";
    protected override string EditorSpritePath => "Assets/Resources/Items/stasis_buoy.png";
    public override bool CanBeTargetedByEnemyBots => false;

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

        InitializeCommon();
        deployedAt = Time.time;
        nextPulseTime = Time.time + 0.12f;
        ConfigurePulseVisuals();
        AudioManager.Instance.PlayStasisBuoyAt(transform.position);
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || destroyed)
            return;

        UpdatePulseVisuals();
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (Time.time >= deployedAt + Lifetime)
        {
            DespawnOnMaster();
            return;
        }

        if (Time.time >= nextPulseTime)
        {
            nextPulseTime = Time.time + PulseInterval;
            PulseOnMaster();
        }
    }

    void PulseOnMaster()
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(transform.position, PulseRadius, filter, PulseHits);
        HashSet<int> shockedViews = new HashSet<int>();
        int ownerActorNumber = ResolveOwnerActorNumber();
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = PulseHits[i];
            PlayerHealth health = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
            if (!IsValidShockTarget(health, ownerActorNumber))
                continue;

            if (!shockedViews.Add(health.photonView.ViewID))
                continue;

            health.photonView.RPC(
                nameof(PlayerHealth.ApplyElectromagneticShockRpc),
                RpcTarget.All,
                ShockDuration,
                ShockSpeedMultiplier,
                ShockFireIntervalMultiplier);
        }

        photonView.RPC(nameof(PlayStasisPulseRpc), RpcTarget.All);
    }

    bool IsValidShockTarget(PlayerHealth health, int ownerActorNumber)
    {
        if (health == null ||
            health.photonView == null ||
            health.IsWreck ||
            health.IsEvacuationAnimating ||
            health.GetComponent<LureBeaconDecoy>() != null ||
            health.GetComponent<PlayerDeployableBase>() != null)
        {
            return false;
        }

        if (health.photonView.ViewID == ownerShipViewId)
            return false;

        bool computerTarget = health.IsBotControlled || health.IsNeutralRiderControlled || health.IsEnemyAstronautControlled;
        return computerTarget || ownerActorNumber <= 0 || health.photonView.OwnerActorNr != ownerActorNumber;
    }

    int ResolveOwnerActorNumber()
    {
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null)
            return 0;

        if (ownerView.Owner != null && ownerView.Owner.ActorNumber > 0)
            return ownerView.Owner.ActorNumber;

        return Mathf.Max(0, ownerView.OwnerActorNr);
    }

    [PunRPC]
    void PlayStasisPulseRpc()
    {
        AudioManager.Instance.PlayStasisPulseAt(transform.position);
    }

    void ConfigurePulseVisuals()
    {
        outerRing = CreateRing("StasisOuterRing", 0.055f, 42);
        innerRing = CreateRing("StasisInnerRing", 0.025f, 43);
        UpdatePulseVisuals();
    }

    LineRenderer CreateRing(string objectName, float width, int sortingOrder)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(transform, false);
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 72;
        ring.widthMultiplier = width;
        ring.numCapVertices = 4;
        ring.numCornerVertices = 4;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        ring.sortingOrder = sortingOrder;
        return ring;
    }

    void UpdatePulseVisuals()
    {
        float phase = Mathf.Repeat((Time.time - deployedAt) / PulseInterval, 1f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10.5f);
        SetRing(outerRing, Mathf.Lerp(0.78f, PulseRadius, phase), new Color(0.32f, 0.95f, 1f, Mathf.Lerp(0.28f, 0.05f, phase)));
        SetRing(innerRing, Mathf.Lerp(0.48f, 1.12f, pulse), new Color(0.92f, 0.98f, 1f, 0.44f + pulse * 0.18f));

        if (spriteRenderer != null)
            spriteRenderer.color = Color.Lerp(Color.white, new Color(0.48f, 0.92f, 1f, 1f), pulse * 0.35f);
    }

    void SetRing(LineRenderer ring, float radius, Color color)
    {
        if (ring == null)
            return;

        ring.startColor = color;
        ring.endColor = new Color(color.r, color.g, color.b, color.a * 0.25f);
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            Vector2 point = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            ring.SetPosition(i, new Vector3(point.x, point.y, -0.41f));
        }
    }

    protected override void OnDestroyedByDamage()
    {
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(transform.position, 1.45f);
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, 1.45f);
    }
}
