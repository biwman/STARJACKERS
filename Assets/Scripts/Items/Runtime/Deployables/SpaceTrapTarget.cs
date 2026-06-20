using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class SpaceTrapTarget : MonoBehaviourPun
{
    const int TrapDamage = 100;
    const float TrapRadius = 3.12f;
    const float TrapOwnerDamagePadding = 0.75f;
    const float TrapCollectorDamagePadding = 0.75f;
    static readonly Dictionary<int, int> ArmedOwnerByTargetView = new Dictionary<int, int>();
    static readonly Collider2D[] TrapDamageHits = new Collider2D[96];

    int ownerViewId;
    bool armed;
    SpriteRenderer markerRenderer;

    public bool IsArmed => armed;

    public static void ResetForSessionTransition()
    {
        ArmedOwnerByTargetView.Clear();
        SpaceTrapTarget[] targets = FindObjectsByType<SpaceTrapTarget>(FindObjectsInactive.Exclude);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].HideMarker();
        }
    }

    public static SpaceTrapTarget Attach(GameObject target)
    {
        if (target == null)
            return null;

        SpaceTrapTarget trap = target.GetComponent<SpaceTrapTarget>();
        if (trap == null)
            trap = target.AddComponent<SpaceTrapTarget>();

        return trap;
    }

    public static bool TryArmTarget(PhotonView target, int owner)
    {
        if (!PhotonNetwork.IsMasterClient || target == null || owner <= 0 || IsTargetArmed(target.ViewID))
            return false;

        ArmedOwnerByTargetView[target.ViewID] = owner;
        SpaceTrapTarget trap = Attach(target.gameObject);
        if (trap != null)
            trap.Arm(owner);

        return true;
    }

    public static PhotonView FindClosestTrapTarget(Vector2 origin, float range)
    {
        PhotonView best = null;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected)
                continue;

            ConsiderTrapCandidate(treasure.GetComponent<PhotonView>(), treasure.transform.position, origin, range, ref best, ref bestDistance);
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            ConsiderTrapCandidate(wreck.GetComponent<PhotonView>(), wreck.transform.position, origin, range, ref best, ref bestDistance);
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            ConsiderTrapCandidate(crate.GetComponent<PhotonView>(), crate.transform.position, origin, range, ref best, ref bestDistance);
        }

        return best;
    }

    static void ConsiderTrapCandidate(PhotonView view, Vector2 position, Vector2 origin, float range, ref PhotonView best, ref float bestDistance)
    {
        if (view == null || IsTargetArmed(view.ViewID))
            return;

        float distance = Vector2.Distance(origin, position);
        if (distance > range || distance >= bestDistance)
            return;

        bestDistance = distance;
        best = view;
    }

    public static bool DetonateIfArmed(int targetViewId)
    {
        return DetonateIfArmed(targetViewId, 0);
    }

    public static bool DetonateIfArmed(int targetViewId, int collectorViewId)
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        PhotonView view = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = view != null ? view.GetComponent<SpaceTrapTarget>() : null;
        if (!TryGetArmedOwner(targetViewId, trap, out int armedOwnerViewId))
            return false;

        if (view == null)
        {
            ArmedOwnerByTargetView.Remove(targetViewId);
            return false;
        }

        ArmedOwnerByTargetView.Remove(targetViewId);
        if (trap != null)
            trap.HideMarker();

        BroadcastMarkerClear(armedOwnerViewId, collectorViewId, targetViewId);
        Vector3 point = view.transform.position;
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(point, TrapRadius);
        ApplyExplosionDamage(point, armedOwnerViewId, collectorViewId);
        return true;
    }

    static bool TryGetArmedOwner(int targetViewId, SpaceTrapTarget trap, out int armedOwnerViewId)
    {
        if (ArmedOwnerByTargetView.TryGetValue(targetViewId, out armedOwnerViewId) && armedOwnerViewId > 0)
            return true;

        if (trap != null && trap.armed && trap.ownerViewId > 0)
        {
            armedOwnerViewId = trap.ownerViewId;
            return true;
        }

        armedOwnerViewId = 0;
        return false;
    }

    static bool IsTargetArmed(int targetViewId)
    {
        if (targetViewId <= 0)
            return false;

        if (ArmedOwnerByTargetView.ContainsKey(targetViewId))
            return true;

        PhotonView view = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = view != null ? view.GetComponent<SpaceTrapTarget>() : null;
        return trap != null && trap.IsArmed;
    }

    static void BroadcastMarkerClear(int owner, int collectorViewId, int targetViewId)
    {
        PhotonView broadcaster = ResolvePlayerShootingView(owner);
        if (broadcaster == null)
            broadcaster = ResolvePlayerShootingView(collectorViewId);

        if (broadcaster != null)
            broadcaster.RPC("ClearSpaceTrapTargetRpc", RpcTarget.All, targetViewId);
    }

    static PhotonView ResolvePlayerShootingView(int viewId)
    {
        if (viewId <= 0)
            return null;

        PhotonView view = PhotonView.Find(viewId);
        return view != null && view.GetComponent<PlayerShooting>() != null ? view : null;
    }

    public static void ClearLocalMarker(int targetViewId)
    {
        ArmedOwnerByTargetView.Remove(targetViewId);
        PhotonView view = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = view != null ? view.GetComponent<SpaceTrapTarget>() : null;
        if (trap != null)
            trap.HideMarker();
    }

    public void Arm(int owner)
    {
        ownerViewId = owner;
        armed = true;
        if (photonView != null)
            ArmedOwnerByTargetView[photonView.ViewID] = ownerViewId;
        EnsureMarker();
    }

    [PunRPC]
    void ArmTrapRpc(int owner)
    {
        Arm(owner);
    }

    void Update()
    {
    }

    void OnDestroy()
    {
        if (photonView != null)
            ArmedOwnerByTargetView.Remove(photonView.ViewID);
    }

    void HideMarker()
    {
        armed = false;
        if (markerRenderer != null)
            markerRenderer.enabled = false;
    }

    void EnsureMarker()
    {
        if (markerRenderer != null)
        {
            markerRenderer.enabled = true;
            return;
        }

        GameObject marker = new GameObject("SpaceTrapMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = RuntimeSpriteUtility.LoadSprite("space_trap_top_down_resource", "Assets/space_trap_top_down.png");
        markerRenderer.color = new Color(1f, 0.88f, 0.18f, 0.78f);
        markerRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        markerRenderer.sortingOrder = 180;
        RuntimeSpriteUtility.FitRenderer(markerRenderer, 0.5f);
    }

    void Detonate()
    {
        if (!PhotonNetwork.IsMasterClient || !armed || photonView == null)
            return;

        DetonateIfArmed(photonView.ViewID);
    }

    [PunRPC]
    void PlayTrapExplosionRpc(float x, float y, float radius)
    {
        armed = false;
        if (markerRenderer != null)
            markerRenderer.enabled = false;

        EnemyBot.SpawnSpaceMineDetonationEffects(new Vector3(x, y, transform.position.z), radius);
    }

    static void ApplyExplosionDamage(Vector2 center, int attackerViewId, int collectorViewId)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
        int hitCount = Physics2D.OverlapCircle(center, TrapRadius, filter, TrapDamageHits);
        HashSet<int> damagedViews = new HashSet<int>();
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.Trap,
            WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed,
            PlayerHealth.DamageSourceExplosive);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = TrapDamageHits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    TrapDamage,
                    TrapDamage,
                    attackerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            {
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    TrapDamage,
                    TrapDamage,
                    attackerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }
        }

        TryDamageViewIfClose(attackerViewId, center, attackerViewId, damagedViews, TrapOwnerDamagePadding, hitContext);
        TryDamageViewIfClose(collectorViewId, center, attackerViewId, damagedViews, TrapCollectorDamagePadding, hitContext);
    }

    static void TryDamageViewIfClose(int targetViewId, Vector2 center, int attackerViewId, HashSet<int> damagedViews, float padding, WeaponHitContext hitContext)
    {
        if (targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || Vector2.Distance(center, targetView.transform.position) > TrapRadius + padding)
            return;

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
        {
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                TrapDamage,
                TrapDamage,
                attackerViewId,
                center.x,
                center.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
            return;
        }

        PlayerHealth health = targetView.GetComponent<PlayerHealth>();
        if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            health.photonView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                TrapDamage,
                TrapDamage,
                attackerViewId,
                center.x,
                center.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
    }
}
