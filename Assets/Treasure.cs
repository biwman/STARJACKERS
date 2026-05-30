using UnityEngine;
using Photon.Pun;

public class Treasure : MonoBehaviourPun
{
    public const float CollectRange = 2.2f;
    public int value;
    public string itemId = InventoryItemCatalog.AsteroidResourceId;

    private SpriteRenderer sr;
    private Color originalColor;

    // blokada zbierania
    public bool isBeingCollected = false;

    void Start()
    {
        InitializeFromPhotonData();
        value = InventoryItemCatalog.GetSellValueAstrons(itemId);

        sr = GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.color = Color.white;
            sr.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            sr.sortingOrder = GameVisualTheme.TreasureSortingOrder;
            originalColor = Color.white;
        }

        BoxCollider2D bodyCollider = GetComponent<BoxCollider2D>();
        if (bodyCollider == null)
        {
            bodyCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        bodyCollider.isTrigger = false;

        MovingSpaceObject movingObject = GetComponent<MovingSpaceObject>();
        if (movingObject == null)
        {
            movingObject = gameObject.AddComponent<MovingSpaceObject>();
        }

        bool isContainer = InventoryItemCatalog.IsContainerItem(itemId) || InventoryItemCatalog.IsBlueprintScrapContainerItem(itemId);
        bool isRandomLootWreck = InventoryItemCatalog.IsRandomLootWreckItem(itemId);
        string stablePrefix = isRandomLootWreck ? "random_loot_wreck_" : isContainer ? "container_" : "treasure_";
        string stableId = photonView != null
            ? stablePrefix + photonView.ViewID
            : stablePrefix + gameObject.name;
        MovingSpaceObject.SpaceObjectType objectType = isContainer || isRandomLootWreck
            ? MovingSpaceObject.SpaceObjectType.Container
            : MovingSpaceObject.SpaceObjectType.Treasure;
        movingObject.Configure(stableId, objectType);
        GameVisualTheme.ApplyTreasureVisual(this);
        if (sr != null)
            originalColor = sr.color;
    }

    void InitializeFromPhotonData()
    {
        if (photonView != null &&
            photonView.InstantiationData != null &&
            photonView.InstantiationData.Length > 0 &&
            photonView.InstantiationData[0] is string instancedItemId &&
            !string.IsNullOrWhiteSpace(instancedItemId))
        {
            itemId = instancedItemId;
        }
    }

    public float GetColliderSizeMultiplier()
    {
        if (InventoryItemCatalog.IsRandomLootWreckItem(itemId))
            return 0.82f;

        if (InventoryItemCatalog.IsContainerItem(itemId) || InventoryItemCatalog.IsBlueprintScrapContainerItem(itemId))
            return 0.78f;

        if (itemId == InventoryItemCatalog.SpaceAnimalRemainsId)
            return 0.82f;

        switch (InventoryItemCatalog.GetRarity(itemId))
        {
            case InventoryItemRarity.Uncommon:
                return 0.72f;
            case InventoryItemRarity.Rare:
                return 0.76f;
            case InventoryItemRarity.VeryRare:
                return 0.8f;
            case InventoryItemRarity.Epic:
                return 0.84f;
            case InventoryItemRarity.Legendary:
                return 0.86f;
            default:
                return 0.68f;
        }
    }

    public void Highlight()
    {
        if (sr != null)
            sr.color = Color.green;
    }

    public void Unhighlight()
    {
        if (sr != null)
            sr.color = originalColor;
    }

    [PunRPC]
    public void SetBeingCollectedRpc(bool value)
    {
        isBeingCollected = value;
    }
}
