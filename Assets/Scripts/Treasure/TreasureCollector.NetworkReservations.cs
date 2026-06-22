using System.Collections.Generic;
using Photon.Pun;

public partial class TreasureCollector
{
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedTreasureCollections = new System.Collections.Generic.Dictionary<int, int>();
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedWreckLoot = new System.Collections.Generic.Dictionary<int, int>();
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedDroppedCargoLoot = new System.Collections.Generic.Dictionary<int, int>();
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedRandomLootWrecks = new System.Collections.Generic.Dictionary<int, int>();

    public static void ResetRoundReservations()
    {
        ReservedTreasureCollections.Clear();
        ReservedWreckLoot.Clear();
        ReservedDroppedCargoLoot.Clear();
        ReservedRandomLootWrecks.Clear();
    }


    void ReleaseMasterTreasureReservation(bool notifyMaster)
    {
        int viewId = activeTreasureReservationViewId > 0 ? activeTreasureReservationViewId : pendingTreasureReservationViewId;
        activeTreasureReservationViewId = 0;
        pendingTreasureReservationViewId = 0;

        if (!notifyMaster || viewId <= 0 || !PhotonNetwork.InRoom || photonView == null)
            return;

        photonView.RPC(nameof(RequestReleaseTreasureCollection), RpcTarget.MasterClient, viewId);
    }


    [PunRPC]
    void RequestReserveTreasureCollection(int viewID, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!IsRequestFromOwner(messageInfo))
            return;

        bool accepted = false;
        PhotonView pv = PhotonView.Find(viewID);
        Treasure treasure = pv != null ? pv.GetComponent<Treasure>() : null;
        if (treasure != null && !treasure.isBeingCollected && !IsTreasureTemporarilyLocked(treasure) && IsTreasureInCollectRange(treasure, true))
        {
            if (!ReservedTreasureCollections.TryGetValue(viewID, out int reservedActor) ||
                reservedActor == photonView.OwnerActorNr)
            {
                ReservedTreasureCollections[viewID] = photonView.OwnerActorNr;
                accepted = true;
            }
        }

        if (photonView != null && photonView.Owner != null)
            photonView.RPC(nameof(ConfirmTreasureCollectionReservationRpc), photonView.Owner, viewID, accepted);
    }

    [PunRPC]
    void ConfirmTreasureCollectionReservationRpc(int viewID, bool accepted)
    {
        if (!photonView.IsMine)
            return;

        if (pendingTreasureReservationViewId != viewID)
            return;

        if (!accepted)
        {
            CancelActiveCollection();
            return;
        }

        PhotonView targetView = PhotonView.Find(viewID);
        Treasure treasure = targetView != null ? targetView.GetComponent<Treasure>() : null;
        if (treasure == null || !CanStoreTreasure(treasure))
        {
            CancelActiveCollection();
            return;
        }

        BeginTreasureCollection(treasure, viewID);
    }

    [PunRPC]
    void RequestReleaseTreasureCollection(int viewID, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !IsRequestFromOwner(messageInfo))
            return;

        if (ReservedTreasureCollections.TryGetValue(viewID, out int reservedActor) &&
            reservedActor == photonView.OwnerActorNr)
        {
            ReservedTreasureCollections.Remove(viewID);
        }
    }

    [PunRPC]
    void RequestDestroyTreasure(int viewID, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!IsRequestFromOwner(messageInfo))
            return;

        if (ReservedTreasureCollections.TryGetValue(viewID, out int reservedActor) &&
            reservedActor != photonView.OwnerActorNr)
        {
            if (photonView != null && photonView.Owner != null)
                photonView.RPC(nameof(ForceCancelCollectibleUseRpc), photonView.Owner, viewID);

            return;
        }

        PhotonView pv = PhotonView.Find(viewID);
        if (pv == null)
        {
            ReservedTreasureCollections.Remove(viewID);
            return;
        }

        if (pv != null)
        {
            ReservedTreasureCollections.Remove(viewID);
            SpaceTrapTarget.DetonateIfArmed(viewID, photonView != null ? photonView.ViewID : 0);
            NotifyPirateBasesAboutCollectedTarget(viewID);
            PhotonNetwork.Destroy(pv.gameObject);
        }
    }

    [PunRPC]
    void ForceCancelCollectibleUseRpc(int viewID)
    {
        if (!photonView.IsMine)
            return;

        int currentViewId = 0;
        if (currentTreasure != null)
        {
            PhotonView treasureView = currentTreasure.GetComponent<PhotonView>();
            currentViewId = treasureView != null ? treasureView.ViewID : 0;
        }

        if (viewID <= 0 || currentViewId == viewID || pendingTreasureReservationViewId == viewID || activeTreasureReservationViewId == viewID)
            CancelActiveCollection();
    }

    void NotifyPirateBasesAboutCollectedTarget(int collectibleViewId)
    {
        int collectorViewId = photonView != null ? photonView.ViewID : 0;
        EnemyPirateBaseBehavior.NotifyCollectibleCollected(collectibleViewId, collectorViewId);
    }


    bool IsRequestFromOwner(PhotonMessageInfo messageInfo)
    {
        return photonView != null &&
               photonView.Owner != null &&
               messageInfo.Sender != null &&
               messageInfo.Sender.ActorNumber == photonView.Owner.ActorNumber;
    }

}
