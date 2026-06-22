using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class PlayerProfilePanelUI
{
    void RefreshInventoryView(PlayerInventoryData inventory)
    {
        PlayerInventoryData normalized = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        normalized.Normalize();
        visiblePlayerInventorySlotMap = BuildVisiblePlayerInventorySlotMap(normalized);

        if (shipInventoryLabelText != null)
        {
            int shipCapacity = GetActiveShipInventoryCapacity();
            int occupiedShipSlots = CountOccupiedShipInventorySlots(normalized, shipCapacity);
            shipInventoryLabelText.text = "SHIP INVENTORY " + occupiedShipSlots + "/" + shipCapacity;
            ConfigureShipInventoryLabelText();
        }

        RebuildPlayerInventoryGrid(GetDisplayedPlayerInventorySlotCount(normalized));

        if (playerInventoryLabelText != null)
            ConfigurePlayerInventoryLabelText();
        if (playerInventoryCountText != null)
        {
            int occupiedPlayerSlots = CountOccupiedPlayerInventorySlots(normalized);
            int playerSlotCount = normalized.PlayerSlots != null ? normalized.PlayerSlots.Length : PlayerInventoryData.DefaultPlayerSlotCount;
            playerInventoryCountText.text = occupiedPlayerSlots + "/" + playerSlotCount;
            ConfigurePlayerInventoryCountText();
        }

        RefreshPlayerInventoryFilterButton();
        RefreshPlayerInventorySortButton();

        RefreshInventoryButtons(shipInventoryButtons, shipInventoryTexts, shipInventoryIcons, normalized.ShipSlots, true);
        RefreshInventoryButtons(playerInventoryButtons, playerInventoryTexts, playerInventoryIcons, normalized.PlayerSlots, false);
        RefreshCraftingButtons(craftingSlotButtons, craftingSlotTexts, craftingSlotIcons, normalized.CraftingSlots);

        if (resetPlayerInventoryScrollOnNextRefresh && playerInventoryScrollRect != null)
        {
            playerInventoryScrollRect.verticalNormalizedPosition = 1f;
            resetPlayerInventoryScrollOnNextRefresh = false;
        }
    }

    void OnPlayerInventoryFilterClicked()
    {
        if (inventoryActionInProgress || panelObject == null || !panelObject.activeSelf)
            return;

        if (playerInventoryFilterMode == PlayerInventoryFilterMode.CustomEquipmentSlot)
        {
            SetPlayerInventoryFilter(PlayerInventoryFilterMode.All, -1);
        }
        else
        {
            SetPlayerInventoryFilter(
                playerInventoryFilterMode == PlayerInventoryFilterMode.Equipable
                    ? PlayerInventoryFilterMode.All
                    : PlayerInventoryFilterMode.Equipable,
                -1);
        }

        resetPlayerInventoryScrollOnNextRefresh = true;
        HideItemPreview();
        RefreshView();
    }

    void OnPlayerInventorySortClicked()
    {
        if (inventoryActionInProgress || panelObject == null || !panelObject.activeSelf)
            return;

        playerInventorySortMode = GetNextPlayerInventorySortMode(playerInventorySortMode);
        resetPlayerInventoryScrollOnNextRefresh = true;
        HideItemPreview();
        RefreshView();
    }

    void SetPlayerInventoryFilter(PlayerInventoryFilterMode mode, int equipmentSlotIndex)
    {
        playerInventoryFilterMode = mode;
        customPlayerInventoryEquipmentSlotIndex = mode == PlayerInventoryFilterMode.CustomEquipmentSlot ? equipmentSlotIndex : -1;
    }

    PlayerInventorySortMode GetNextPlayerInventorySortMode(PlayerInventorySortMode mode)
    {
        switch (mode)
        {
            case PlayerInventorySortMode.Alphabetical:
                return PlayerInventorySortMode.Price;
            case PlayerInventorySortMode.Price:
                return PlayerInventorySortMode.Rarity;
            case PlayerInventorySortMode.Rarity:
                return PlayerInventorySortMode.Type;
            default:
                return PlayerInventorySortMode.Alphabetical;
        }
    }

    string GetPlayerInventorySortLabel(PlayerInventorySortMode mode)
    {
        switch (mode)
        {
            case PlayerInventorySortMode.Price:
                return "PRICE";
            case PlayerInventorySortMode.Rarity:
                return "RARITY";
            case PlayerInventorySortMode.Type:
                return "TYPE";
            default:
                return "A-Z";
        }
    }

    void RefreshPlayerInventorySortButton()
    {
        if (playerInventorySortButton == null)
            return;

        TMP_Text text = playerInventorySortButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = "SORT: " + GetPlayerInventorySortLabel(playerInventorySortMode);

        Image image = playerInventorySortButton.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.14f, 0.19f, 0.28f, 0.98f);
    }

    void RefreshPlayerInventoryFilterButton()
    {
        if (playerInventoryFilterButton == null)
            return;

        TMP_Text text = playerInventoryFilterButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = playerInventoryFilterMode switch
            {
                PlayerInventoryFilterMode.Equipable => "EQUIPABLE",
                PlayerInventoryFilterMode.CustomEquipmentSlot => "CUSTOM",
                _ => "ALL"
            };
        }

        Image image = playerInventoryFilterButton.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.14f, 0.19f, 0.28f, 0.98f);
    }

    int[] BuildVisiblePlayerInventorySlotMap(PlayerInventoryData inventory)
    {
        string[] slots = inventory != null ? inventory.PlayerSlots : null;
        if (slots == null)
            return Array.Empty<int>();

        List<int> visibleSlots = new List<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            string itemId = slots[i];
            if (playerInventoryFilterMode == PlayerInventoryFilterMode.All || ShouldShowPlayerInventoryItem(itemId))
                visibleSlots.Add(i);
        }

        SortPlayerInventorySlotMap(visibleSlots, slots);
        return visibleSlots.ToArray();
    }

    void SortPlayerInventorySlotMap(List<int> slotIndices, string[] slots)
    {
        if (slotIndices == null || slotIndices.Count <= 1 || slots == null)
            return;

        slotIndices.Sort((a, b) => ComparePlayerInventorySlots(slots, a, b));
    }

    int ComparePlayerInventorySlots(string[] slots, int aIndex, int bIndex)
    {
        if (aIndex == bIndex)
            return 0;

        string aItemId = GetSlotItem(slots, aIndex);
        string bItemId = GetSlotItem(slots, bIndex);
        bool aOccupied = !string.IsNullOrWhiteSpace(aItemId);
        bool bOccupied = !string.IsNullOrWhiteSpace(bItemId);
        if (aOccupied != bOccupied)
            return aOccupied ? -1 : 1;

        if (!aOccupied)
            return aIndex.CompareTo(bIndex);

        int result;
        switch (playerInventorySortMode)
        {
            case PlayerInventorySortMode.Price:
                result = InventoryItemCatalog.GetSellValueAstrons(bItemId).CompareTo(InventoryItemCatalog.GetSellValueAstrons(aItemId));
                if (result != 0)
                    return result;
                break;
            case PlayerInventorySortMode.Rarity:
                result = ((int)InventoryItemCatalog.GetRarity(bItemId)).CompareTo((int)InventoryItemCatalog.GetRarity(aItemId));
                if (result != 0)
                    return result;
                break;
            case PlayerInventorySortMode.Type:
                result = InventoryItemCatalog.GetCategory(aItemId).CompareTo(InventoryItemCatalog.GetCategory(bItemId));
                if (result != 0)
                    return result;
                break;
        }

        result = string.Compare(InventoryItemCatalog.GetDisplayName(aItemId), InventoryItemCatalog.GetDisplayName(bItemId), StringComparison.OrdinalIgnoreCase);
        if (result != 0)
            return result;

        return aIndex.CompareTo(bIndex);
    }

    int GetDisplayedPlayerInventorySlotCount(PlayerInventoryData inventory)
    {
        if (playerInventoryFilterMode == PlayerInventoryFilterMode.All)
            return inventory != null && inventory.PlayerSlots != null ? inventory.PlayerSlots.Length : PlayerInventoryData.DefaultPlayerSlotCount;

        return Mathf.Max(1, visiblePlayerInventorySlotMap != null ? visiblePlayerInventorySlotMap.Length : 0);
    }

    bool ShouldShowPlayerInventoryItem(string itemId)
    {
        if (playerInventoryFilterMode == PlayerInventoryFilterMode.Equipable)
            return IsEquipableInventoryItem(itemId);

        if (playerInventoryFilterMode == PlayerInventoryFilterMode.CustomEquipmentSlot)
            return InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, customPlayerInventoryEquipmentSlotIndex);

        return true;
    }

    bool IsEquipableInventoryItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (InventoryItemCatalog.GetItemType(itemId) != InventoryItemType.Equipment)
            return false;

        return InventoryItemCatalog.GetCategory(itemId) switch
        {
            InventoryItemCategory.Weapon => true,
            InventoryItemCategory.Shield => true,
            InventoryItemCategory.Engine => true,
            InventoryItemCategory.Gadget => true,
            InventoryItemCategory.Support => true,
            InventoryItemCategory.Rescue => true,
            _ => false
        };
    }

    void RefreshInventoryButtons(Button[] buttons, TMP_Text[] labels, Image[] icons, string[] slots, bool isShipInventory)
    {
        if (buttons == null || labels == null || icons == null || slots == null)
            return;

        int shipCapacity = GetActiveShipInventoryCapacity();

        for (int i = 0; i < buttons.Length; i++)
        {
            int slotIndex = isShipInventory ? i : ResolveVisiblePlayerInventorySlotIndex(i);
            bool visiblePlayerSlot = isShipInventory || slotIndex >= 0;
            bool withinShipCapacity = !isShipInventory || i < shipCapacity;
            if (buttons[i] != null)
                buttons[i].gameObject.SetActive(withinShipCapacity && visiblePlayerSlot);

            if (!withinShipCapacity || !visiblePlayerSlot || slotIndex >= slots.Length)
            {
                ClearInventoryButtonVisual(buttons, labels, icons, i);
                continue;
            }

            string itemId = slots[slotIndex];
            bool occupied = !string.IsNullOrWhiteSpace(itemId);
            bool isRadioactiveCargo = isShipInventory && occupied && InventoryItemCatalog.IsRadioactiveTreasure(itemId);
            bool isSafePocket = isShipInventory && IsActiveShipSafePocketIndex(i);
            bool isAstronautSlot = isShipInventory && IsActiveShipAstronautCargoIndex(i);
            Image image = buttons[i] != null ? buttons[i].GetComponent<Image>() : null;
            Image icon = icons[i];
            Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;

            if (labels[i] != null)
            {
                bool useTextLabel = occupied && itemSprite == null;
                if (useTextLabel)
                {
                    labels[i].text = InventoryItemCatalog.GetShortLabel(itemId);
                    labels[i].color = new Color(0.97f, 0.99f, 1f, 1f);
                }
                else if (isSafePocket)
                {
                    labels[i].text = "SAFE";
                    labels[i].color = occupied ? new Color(0f, 0f, 0f, 0f) : new Color(0.56f, 1f, 0.95f, 0.86f);
                }
                else if (isAstronautSlot)
                {
                    labels[i].text = "ASTRO";
                    labels[i].color = occupied ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 0.79f, 0.42f, 0.88f);
                }
                else
                {
                    labels[i].text = string.Empty;
                    labels[i].color = new Color(0f, 0f, 0f, 0f);
                }
            }

            if (icon != null)
            {
                icon.sprite = itemSprite;
                icon.enabled = occupied && itemSprite != null;
            }

            if (image != null)
            {
                image.color = GetInventorySlotColor(itemId, occupied, isSafePocket, isAstronautSlot, isRadioactiveCargo);
            }

            if (buttons[i] != null)
            {
                Outline outline = buttons[i].GetComponent<Outline>();
                if (isRadioactiveCargo)
                {
                    if (outline == null)
                        outline = buttons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.34f, 1f, 0.2f, 0.98f);
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (isSafePocket)
                {
                    if (outline == null)
                        outline = buttons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.38f, 0.98f, 0.88f, 0.95f);
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (isAstronautSlot)
                {
                    if (outline == null)
                        outline = buttons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1f, 0.64f, 0.22f, 0.95f);
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (outline != null)
                {
                    outline.enabled = false;
                }
            }

            if (buttons[i] != null)
                buttons[i].interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
        }
    }

    void ClearInventoryButtonVisual(Button[] buttons, TMP_Text[] labels, Image[] icons, int index)
    {
        if (index < 0)
            return;

        if (labels != null && index < labels.Length && labels[index] != null)
        {
            labels[index].text = string.Empty;
            labels[index].color = new Color(0f, 0f, 0f, 0f);
        }

        if (icons != null && index < icons.Length && icons[index] != null)
        {
            icons[index].sprite = null;
            icons[index].enabled = false;
        }

        if (buttons != null && index < buttons.Length && buttons[index] != null)
        {
            Image image = buttons[index].GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.12f, 0.16f, 0.21f, 0.96f);

            Outline outline = buttons[index].GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }
    }

    Color GetInventorySlotColor(string itemId, bool occupied, bool isSafePocket, bool isAstronautSlot, bool isRadioactiveCargo)
    {
        if (occupied)
        {
            Color baseColor = InventoryItemCatalog.GetRarityColor(itemId);
            if (isRadioactiveCargo)
                return Color.Lerp(baseColor, new Color(0.18f, 0.95f, 0.24f, baseColor.a), 0.58f);

            if (isSafePocket)
                return Color.Lerp(baseColor, new Color(0.24f, 0.74f, 0.66f, baseColor.a), 0.32f);

            return isAstronautSlot
                ? Color.Lerp(baseColor, new Color(1f, 0.67f, 0.28f, baseColor.a), 0.28f)
                : baseColor;
        }

        if (isSafePocket)
            return new Color(0.09f, 0.23f, 0.22f, 0.98f);

        return isAstronautSlot
            ? new Color(0.28f, 0.18f, 0.08f, 0.98f)
            : new Color(0.12f, 0.16f, 0.21f, 0.96f);
    }

    int ResolveVisiblePlayerInventorySlotIndex(int displayedSlotIndex)
    {
        if (visiblePlayerInventorySlotMap == null || displayedSlotIndex < 0)
            return playerInventoryFilterMode == PlayerInventoryFilterMode.All ? displayedSlotIndex : -1;

        if (displayedSlotIndex >= visiblePlayerInventorySlotMap.Length)
            return playerInventoryFilterMode == PlayerInventoryFilterMode.All ? displayedSlotIndex : -1;

        return visiblePlayerInventorySlotMap[displayedSlotIndex];
    }

    void RefreshCraftingButtons(Button[] buttons, TMP_Text[] labels, Image[] icons, string[] slots)
    {
        if (buttons == null || labels == null || icons == null || slots == null)
            return;

        for (int i = 0; i < buttons.Length && i < slots.Length; i++)
        {
            string itemId = slots[i];
            bool occupied = !string.IsNullOrWhiteSpace(itemId);
            Image image = buttons[i] != null ? buttons[i].GetComponent<Image>() : null;
            Image icon = icons[i];
            Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;

            if (labels[i] != null)
            {
                bool useTextLabel = occupied && itemSprite == null;
                labels[i].text = useTextLabel ? InventoryItemCatalog.GetShortLabel(itemId) : string.Empty;
                labels[i].color = useTextLabel ? new Color(0.97f, 0.99f, 1f, 1f) : new Color(0f, 0f, 0f, 0f);
            }

            if (icon != null)
            {
                icon.sprite = itemSprite;
                icon.enabled = occupied && itemSprite != null;
            }

            if (image != null)
            {
                image.color = occupied
                    ? InventoryItemCatalog.GetRarityColor(itemId)
                    : new Color(0.12f, 0.16f, 0.21f, 0.96f);
            }

            if (buttons[i] != null)
                buttons[i].interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
        }

        if (craftButton != null)
            craftButton.interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
        if (clearCraftButton != null)
            clearCraftButton.interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
    }
}
