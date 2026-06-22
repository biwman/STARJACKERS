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
    int GetAuthoritativeRemainingChargesOnMaster(int actorNumber, string itemId, int maxCharges)
    {
        if (actorNumber <= 0 || string.IsNullOrWhiteSpace(itemId))
            return 0;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(GetAuthoritativeGadgetChargeStateRaw());
        if (chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) &&
            actorCharges != null &&
            actorCharges.TryGetValue(itemId, out int remainingCharges))
        {
            return Mathf.Clamp(remainingCharges, 0, maxCharges);
        }

        return maxCharges;
    }

    void SetAuthoritativeRemainingChargesOnMaster(int actorNumber, string itemId, int remainingCharges, int maxCharges)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || actorNumber <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(GetAuthoritativeGadgetChargeStateRaw());
        if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null)
        {
            actorCharges = new Dictionary<string, int>(StringComparer.Ordinal);
            chargesByActor[actorNumber] = actorCharges;
        }

        if (remainingCharges >= maxCharges)
            actorCharges.Remove(itemId);
        else
            actorCharges[itemId] = Mathf.Max(0, remainingCharges);

        if (actorCharges.Count == 0)
            chargesByActor.Remove(actorNumber);

        string serializedState = SerializeAuthoritativeGadgetChargeState(chargesByActor);
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.GadgetChargesStateKey] = serializedState
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        lastAuthoritativeGadgetChargeStateRaw = null;
    }

    static void ParseAuthoritativeGadgetChargesForActor(string serializedState, int actorNumber, Dictionary<string, int> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        if (string.IsNullOrWhiteSpace(serializedState) || actorNumber <= 0)
            return;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(serializedState);
        if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null)
            return;

        foreach (KeyValuePair<string, int> pair in actorCharges)
            destination[pair.Key] = Mathf.Max(0, pair.Value);
    }

    static Dictionary<int, Dictionary<string, int>> DeserializeAuthoritativeGadgetChargeState(string serializedState)
    {
        Dictionary<int, Dictionary<string, int>> chargesByActor = new Dictionary<int, Dictionary<string, int>>();
        if (string.IsNullOrWhiteSpace(serializedState))
            return chargesByActor;

        string[] actorEntries = serializedState.Split(';');
        for (int i = 0; i < actorEntries.Length; i++)
        {
            string actorEntry = actorEntries[i];
            if (string.IsNullOrWhiteSpace(actorEntry))
                continue;

            int separatorIndex = actorEntry.IndexOf('#');
            if (separatorIndex <= 0)
                continue;

            string actorRaw = actorEntry.Substring(0, separatorIndex);
            if (!int.TryParse(actorRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int actorNumber) || actorNumber <= 0)
                continue;

            string itemsRaw = actorEntry.Substring(separatorIndex + 1);
            if (string.IsNullOrWhiteSpace(itemsRaw))
                continue;

            Dictionary<string, int> actorCharges = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] itemEntries = itemsRaw.Split(',');
            for (int itemIndex = 0; itemIndex < itemEntries.Length; itemIndex++)
            {
                string itemEntry = itemEntries[itemIndex];
                if (string.IsNullOrWhiteSpace(itemEntry))
                    continue;

                int itemSeparatorIndex = itemEntry.IndexOf('=');
                if (itemSeparatorIndex <= 0 || itemSeparatorIndex >= itemEntry.Length - 1)
                    continue;

                string itemId = itemEntry.Substring(0, itemSeparatorIndex);
                string remainingRaw = itemEntry.Substring(itemSeparatorIndex + 1);
                if (string.IsNullOrWhiteSpace(itemId) ||
                    !int.TryParse(remainingRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int remainingCharges))
                {
                    continue;
                }

                actorCharges[itemId] = Mathf.Max(0, remainingCharges);
            }

            if (actorCharges.Count > 0)
                chargesByActor[actorNumber] = actorCharges;
        }

        return chargesByActor;
    }

    static string SerializeAuthoritativeGadgetChargeState(Dictionary<int, Dictionary<string, int>> chargesByActor)
    {
        if (chargesByActor == null || chargesByActor.Count == 0)
            return string.Empty;

        List<int> actorNumbers = new List<int>(chargesByActor.Keys);
        actorNumbers.Sort();

        StringBuilder builder = new StringBuilder();
        for (int actorIndex = 0; actorIndex < actorNumbers.Count; actorIndex++)
        {
            int actorNumber = actorNumbers[actorIndex];
            if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null || actorCharges.Count == 0)
                continue;

            List<string> itemIds = new List<string>(actorCharges.Keys);
            itemIds.Sort(StringComparer.Ordinal);

            StringBuilder actorBuilder = new StringBuilder();
            for (int itemIndex = 0; itemIndex < itemIds.Count; itemIndex++)
            {
                string itemId = itemIds[itemIndex];
                if (string.IsNullOrWhiteSpace(itemId) || !actorCharges.TryGetValue(itemId, out int remainingCharges))
                    continue;

                if (actorBuilder.Length > 0)
                    actorBuilder.Append(',');

                actorBuilder.Append(itemId);
                actorBuilder.Append('=');
                actorBuilder.Append(Mathf.Max(0, remainingCharges).ToString(CultureInfo.InvariantCulture));
            }

            if (actorBuilder.Length == 0)
                continue;

            if (builder.Length > 0)
                builder.Append(';');

            builder.Append(actorNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append('#');
            builder.Append(actorBuilder);
        }

        return builder.ToString();
    }
}
