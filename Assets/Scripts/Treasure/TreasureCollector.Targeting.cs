using Photon.Pun;
using UnityEngine;

public partial class TreasureCollector
{
    void RefreshClosestCollectible()
    {
        TryResolveClosestCollectible(out Treasure nextTreasure, out ShipWreck nextWreck, out DroppedCargoCrate nextDroppedCargo);

        if (currentTreasure == nextTreasure && currentWreck == nextWreck && currentDroppedCargo == nextDroppedCargo)
        {
            if (currentTreasure != null)
                currentTreasure.Highlight();
            else if (currentWreck != null)
                currentWreck.Highlight();
            else if (currentDroppedCargo != null)
                currentDroppedCargo.Highlight();

            return;
        }

        ClearCurrentHighlight();

        currentTreasure = nextTreasure;
        currentWreck = nextWreck;
        currentDroppedCargo = nextDroppedCargo;

        if (currentTreasure != null)
        {
            currentTreasure.Highlight();
        }
        else if (currentWreck != null)
        {
            currentWreck.Highlight();
        }
        else if (currentDroppedCargo != null)
        {
            currentDroppedCargo.Highlight();
        }
    }

    bool HasLockedCollectibleTarget()
    {
        if (!isCollecting)
            return false;

        return currentTreasure != null || currentWreck != null || currentDroppedCargo != null;
    }


    void ForceResolveCollectibleAtUsePress()
    {
        if (!TryResolveClosestCollectible(out Treasure nextTreasure, out ShipWreck nextWreck, out DroppedCargoCrate nextDroppedCargo))
            return;

        ClearCurrentHighlight();
        currentTreasure = nextTreasure;
        currentWreck = nextWreck;
        currentDroppedCargo = nextDroppedCargo;

        if (currentTreasure != null)
            currentTreasure.Highlight();
        else if (currentWreck != null)
            currentWreck.Highlight();
        else if (currentDroppedCargo != null)
            currentDroppedCargo.Highlight();
    }

    bool TryResolveClosestCollectible(out Treasure nextTreasure, out ShipWreck nextWreck, out DroppedCargoCrate nextDroppedCargo)
    {
        Vector2 tipPosition = GetShipTipPosition();
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(tipPosition, GetCollectibleSearchRange(Treasure.CollectRange + 0.55f), out Collider2D[] hits);
        nextTreasure = null;
        nextWreck = null;
        nextDroppedCargo = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Treasure treasure = hit.GetComponent<Treasure>() ?? hit.GetComponentInParent<Treasure>();
            if (treasure != null)
            {
                if (treasure.isBeingCollected)
                    continue;

                float distance = GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, tipPosition);
                if (distance <= GetTreasureCollectRange(treasure) && distance < bestDistance)
                {
                    bestDistance = distance;
                    nextTreasure = treasure;
                    nextWreck = null;
                    nextDroppedCargo = null;
                }

                continue;
            }

            ShipWreck wreck = hit.GetComponent<ShipWreck>() ?? hit.GetComponentInParent<ShipWreck>();
            if (wreck != null && wreck.HasLoot)
            {
                if (wreck.isBeingCollected)
                    continue;

                float distance = GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, tipPosition);
                if (distance <= GetWreckCollectRange(wreck) && distance < bestDistance)
                {
                    bestDistance = distance;
                    nextTreasure = null;
                    nextWreck = wreck;
                    nextDroppedCargo = null;
                }

                continue;
            }

            DroppedCargoCrate crate = hit.GetComponent<DroppedCargoCrate>() ?? hit.GetComponentInParent<DroppedCargoCrate>();
            if (crate != null && crate.HasLoot)
            {
                if (crate.isBeingCollected)
                    continue;

                float distance = GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, tipPosition);
                if (distance <= Treasure.CollectRange && distance < bestDistance)
                {
                    bestDistance = distance;
                    nextTreasure = null;
                    nextWreck = null;
                    nextDroppedCargo = crate;
                }
            }
        }

        return nextTreasure != null || nextWreck != null || nextDroppedCargo != null;
    }

    void ClearCurrentHighlight()
    {
        if (currentTreasure != null)
            currentTreasure.Unhighlight();

        if (currentWreck != null)
            currentWreck.Unhighlight();

        if (currentDroppedCargo != null)
            currentDroppedCargo.Unhighlight();
    }

    bool IsTreasureInCollectRange(Treasure treasure, bool useKeepAliveRange = false)
    {
        if (treasure == null)
            return false;

        return GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, GetShipTipPosition()) <= GetTreasureCollectRange(treasure, useKeepAliveRange);
    }

    bool IsWreckInCollectRange(ShipWreck wreck, bool useKeepAliveRange = false)
    {
        if (wreck == null || !wreck.HasLoot)
            return false;

        return GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, GetShipTipPosition()) <= GetWreckCollectRange(wreck, useKeepAliveRange);
    }

    bool IsDroppedCargoInCollectRange(DroppedCargoCrate crate, bool useKeepAliveRange = false)
    {
        if (crate == null || !crate.HasLoot)
            return false;

        return GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, GetShipTipPosition()) <= GetCollectRange(Treasure.CollectRange, useKeepAliveRange);
    }

    float GetCollectRange(float baseRange, bool useKeepAliveRange)
    {
        if (!useKeepAliveRange)
            return baseRange;

        return baseRange * (1f + RoomSettings.GetCollectKeepAliveRangeBonusPercent() / 100f);
    }

    float GetTreasureCollectRange(Treasure treasure, bool useKeepAliveRange = false)
    {
        float baseRange = Treasure.CollectRange;
        if (treasure != null && InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
            baseRange = ApplySalvageMagnetRange(baseRange);

        return GetCollectRange(baseRange, useKeepAliveRange);
    }

    float GetWreckCollectRange(ShipWreck wreck, bool useKeepAliveRange = false)
    {
        float baseRange = wreck != null && wreck.SourceShipSkinIndex < 0 ? Treasure.CollectRange + 0.45f : Treasure.CollectRange;
        return GetCollectRange(ApplySalvageMagnetRange(baseRange), useKeepAliveRange);
    }

    float GetCollectibleSearchRange(float baseRange)
    {
        return HasSalvageMagnetArrayEquipped() ? baseRange * 2f : baseRange;
    }

    float ApplySalvageMagnetRange(float baseRange)
    {
        return HasSalvageMagnetArrayEquipped() ? baseRange * 2f : baseRange;
    }

    bool HasSalvageMagnetArrayEquipped()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        RefreshLoadoutCacheIfNeeded(owner);
        return cachedHasSalvageMagnetArray;
    }

    float GetDistanceFromTipToCollider(Collider2D collider, Vector2 fallbackPosition, Vector2 tipPosition)
    {
        if (collider != null)
        {
            Vector2 closestPoint = collider.ClosestPoint(tipPosition);
            return Vector2.Distance(tipPosition, closestPoint);
        }

        return Vector2.Distance(tipPosition, fallbackPosition);
    }

    Vector2 GetShipTipPosition()
    {
        float forwardOffset = 0.55f;
        SpriteRenderer renderer = GetCachedSpriteRenderer();
        if (renderer != null)
        {
            forwardOffset = Mathf.Max(0.4f, renderer.bounds.extents.y * 0.9f);
        }

        return (Vector2)transform.position + (Vector2)transform.up * forwardOffset;
    }

}
