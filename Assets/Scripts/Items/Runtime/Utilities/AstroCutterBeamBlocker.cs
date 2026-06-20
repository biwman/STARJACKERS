using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class AstroCutterBeamBlocker
{
    public static float ResolveClippedRange(RaycastHit2D[] hits, Transform ownerTransform, int ownerViewId, float maxRange)
    {
        return ResolveClippedRange(hits, hits != null ? hits.Length : 0, ownerTransform, ownerViewId, maxRange);
    }

    public static float ResolveClippedRange(RaycastHit2D[] hits, int hitCount, Transform ownerTransform, int ownerViewId, float maxRange)
    {
        float clippedRange = maxRange;
        if (hits == null)
            return clippedRange;

        int clampedHitCount = Mathf.Clamp(hitCount, 0, hits.Length);
        for (int i = 0; i < clampedHitCount; i++)
        {
            Collider2D hit = hits[i].collider;
            if (!IsBlockingHit(hit, ownerTransform, ownerViewId))
                continue;

            clippedRange = Mathf.Min(clippedRange, Mathf.Max(0.05f, hits[i].distance));
        }

        return clippedRange;
    }

    public static bool IsBlockingHit(Collider2D hit, Transform ownerTransform, int ownerViewId)
    {
        if (hit == null)
            return false;

        if (ownerTransform != null && (hit.transform == ownerTransform || hit.transform.IsChildOf(ownerTransform)))
            return false;

        PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
        if (health != null &&
            health.photonView != null &&
            !health.IsWreck &&
            health.photonView.ViewID != ownerViewId &&
            health.GetComponent<LureBeaconDecoy>() == null)
        {
            return true;
        }

        PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
        if (deployable != null && deployable.photonView != null)
            return true;

        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<MovingSpaceObject>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null ||
            hit.GetComponentInParent<ShipWreck>() != null ||
            hit.GetComponentInParent<DroppedCargoCrate>() != null)
        {
            return true;
        }

        return false;
    }
}
