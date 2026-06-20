using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class ValuableCargoCarrierUtility
{
    public static int CountCargoItem(Player player, string itemId)
    {
        if (player == null || string.IsNullOrWhiteSpace(itemId))
            return 0;

        string[] slots = PlayerProfileService.GetPlayerShipInventorySlots(player);
        int capacity = PlayerProfileService.GetPlayerShipInventoryCapacity(player);
        int count = 0;
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            if (string.Equals(slots[i], itemId, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    public static bool TryGetTrackedCargoMarkerColor(string itemId, out Color color)
    {
        if (string.Equals(itemId, InventoryItemCatalog.PirateCaseId, StringComparison.Ordinal))
        {
            color = new Color(1f, 0.46f, 0.08f, 0.96f);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.CashSuitcaseId, StringComparison.Ordinal))
        {
            color = new Color(1f, 0.86f, 0.18f, 0.96f);
            return true;
        }

        color = Color.white;
        return false;
    }

    public static PlayerHealth FindActiveHumanShipForPlayer(Player player)
    {
        if (player == null)
            return null;

        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsActiveHumanShip(candidate))
                continue;

            if (candidate.photonView != null && candidate.photonView.OwnerActorNr == player.ActorNumber)
                return candidate;
        }

        return null;
    }

    public static bool IsPirateCaseCarrier(PlayerHealth candidate)
    {
        return IsActiveHumanShip(candidate) &&
               CountCargoItem(candidate.photonView.Owner, InventoryItemCatalog.PirateCaseId) > 0;
    }

    public static PlayerHealth FindBestPirateCaseCarrier(Vector2 origin, float maxDistance, PlayerHealth observer = null)
    {
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PlayerHealth best = null;
        int bestCaseCount = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsActiveHumanShip(candidate) || candidate == observer)
                continue;

            int caseCount = CountCargoItem(candidate.photonView.Owner, InventoryItemCatalog.PirateCaseId);
            if (caseCount <= 0)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxDistance)
                continue;

            if (best == null || caseCount > bestCaseCount || (caseCount == bestCaseCount && distance < bestDistance))
            {
                best = candidate;
                bestCaseCount = caseCount;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static int FindBestPirateCaseCarrierViewId(Vector2 origin, float maxDistance, PlayerHealth observer = null)
    {
        PlayerHealth target = FindBestPirateCaseCarrier(origin, maxDistance, observer);
        return target != null && target.photonView != null ? target.photonView.ViewID : 0;
    }

    static bool IsActiveHumanShip(PlayerHealth candidate)
    {
        return candidate != null &&
               candidate.photonView != null &&
               !candidate.IsWreck &&
               !candidate.IsBotControlled &&
               !candidate.IsNeutralRiderControlled &&
               !candidate.IsAstronautControlled &&
               !candidate.IsEvacuationAnimating;
    }
}
