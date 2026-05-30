using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[Serializable]
public class ShipWreckLootSnapshot
{
    public string[] items;
}

[RequireComponent(typeof(PhotonView))]
public class ShipWreck : MonoBehaviourPun
{
    static int fallbackStableIdSequence;

    readonly List<string> lootItems = new List<string>();
    SpriteRenderer spriteRenderer;
    Color baseColor = Color.white;
    int sourceShipSkinIndex;
    int sourceEnemyKindValue = -1;
    bool isHighlighted;
    bool destroyWhenEmpty = true;

    public bool isBeingCollected;
    public bool HasLoot => lootItems.Count > 0;
    public int LootCount => lootItems.Count;
    public int SourceShipSkinIndex => sourceShipSkinIndex;
    public int SourceEnemyKindValue => sourceEnemyKindValue;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            spriteRenderer.sortingOrder = GameVisualTheme.WreckSortingOrder;
        }
    }

    public void InitializeFromLootJson(string rawLoot, int shipSkinIndex = 0, int enemyKindValue = -1)
    {
        sourceShipSkinIndex = shipSkinIndex;
        sourceEnemyKindValue = shipSkinIndex < 0 ? enemyKindValue : -1;
        if (shipSkinIndex >= 0)
            baseColor = Color.white;
        lootItems.Clear();
        string[] slots = PlayerProfileService.DeserializeShipInventorySlots(rawLoot);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(slots[i]))
                lootItems.Add(slots[i]);
        }

        RefreshVisualState();
        EnsureNetworkMotion();
    }

    void EnsureNetworkMotion()
    {
        MovingSpaceObject movingObject = GetComponent<MovingSpaceObject>();
        if (movingObject == null)
            movingObject = gameObject.AddComponent<MovingSpaceObject>();

        string stableId = photonView != null && photonView.ViewID > 0
            ? "wreck_" + photonView.ViewID
            : "wreck_local_" + (++fallbackStableIdSequence);
        movingObject.Configure(stableId, MovingSpaceObject.SpaceObjectType.Treasure);
    }

    public int GetFirstLootIndex()
    {
        return HasLoot ? 0 : -1;
    }

    public string GetLootItemAt(int index)
    {
        if (index < 0 || index >= lootItems.Count)
            return null;

        return lootItems[index];
    }

    public void Highlight()
    {
        if (!HasLoot || spriteRenderer == null)
            return;

        isHighlighted = true;
        RefreshVisualState();
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        RefreshVisualState();
    }

    public void SetDestroyWhenEmpty(bool value)
    {
        destroyWhenEmpty = value;
    }

    public void Unhighlight()
    {
        isHighlighted = false;
        RefreshVisualState();
    }

    [PunRPC]
    public void SetBeingCollectedRpc(bool value)
    {
        isBeingCollected = value;
        if (!value)
            isHighlighted = false;

        RefreshVisualState();
    }

    void RefreshVisualState()
    {
        if (spriteRenderer == null)
            return;

        if (!HasLoot)
        {
            spriteRenderer.color = new Color(0.28f, 0.29f, 0.32f, 0.78f);
            return;
        }

        spriteRenderer.color = isHighlighted
            ? new Color(0.08f, 1f, 0.22f, 1f)
            : baseColor;
    }

    [PunRPC]
    public void RemoveLootAtIndexRpc(int index)
    {
        if (index >= 0 && index < lootItems.Count)
        {
            lootItems.RemoveAt(index);
        }

        isBeingCollected = false;
        isHighlighted = false;

        if (!HasLoot && sourceShipSkinIndex < 0 && destroyWhenEmpty)
        {
            Destroy(gameObject);
            return;
        }

        RefreshVisualState();
    }
}
