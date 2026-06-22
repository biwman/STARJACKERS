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
    void EnsureDragVisual()
    {
        if (dragVisualObject != null)
            return;

        dragVisualObject = new GameObject("ProfileDragVisual", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        dragVisualObject.transform.SetParent(panelObject.transform, false);

        RectTransform rect = dragVisualObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(72f, 72f);

        Image bg = dragVisualObject.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.14f, 0.18f, 0.92f);

        CanvasGroup group = dragVisualObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0.94f;

        GameObject iconObject = new GameObject("DragIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(dragVisualObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(48f, 48f);
        dragVisualIcon = iconObject.GetComponent<Image>();
        dragVisualIcon.preserveAspect = true;

        dragVisualLabel = CreateText(dragVisualObject.transform, "DragLabel", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13f, TextAlignmentOptions.Center);
        dragVisualLabel.fontStyle = FontStyles.Bold;
        dragVisualLabel.color = Color.white;

        dragVisualObject.SetActive(false);
    }

    void UpdateDragVisualContent(string itemId)
    {
        if (dragVisualObject == null)
            return;

        Image bg = dragVisualObject.GetComponent<Image>();
        if (bg != null)
            bg.color = InventoryItemCatalog.GetRarityColor(itemId);

        Sprite icon = InventoryItemCatalog.GetIcon(itemId);
        dragVisualIcon.sprite = icon;
        dragVisualIcon.enabled = icon != null;
        dragVisualLabel.text = icon == null ? InventoryItemCatalog.GetShortLabel(itemId) : string.Empty;
    }

    void UpdateDragVisualPosition(PointerEventData eventData)
    {
        if (dragVisualObject == null || panelObject == null || eventData == null)
            return;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        RectTransform dragRect = dragVisualObject.GetComponent<RectTransform>();
        if (panelRect == null || dragRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            dragRect.anchoredPosition = localPoint;
    }

    bool ResolveDropTarget(PointerEventData eventData, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        GameObject hoveredObject = eventData != null ? eventData.pointerEnter : null;
        Transform current = hoveredObject != null ? hoveredObject.transform : null;
        while (current != null)
        {
            ProfileInventorySlotDragHandler slot = current.GetComponent<ProfileInventorySlotDragHandler>();
            if (slot != null)
            {
                targetSource = ProfileItemSourceFromInventory(slot.isPlayerInventory);
                targetIndex = targetSource == ProfileItemSource.PlayerInventory
                    ? ResolveVisiblePlayerInventorySlotIndex(slot.slotIndex)
                    : slot.slotIndex;
                if (targetIndex >= 0)
                    return true;
            }

            ProfileEquipmentSlotDragHandler equipmentSlot = current.GetComponent<ProfileEquipmentSlotDragHandler>();
            if (equipmentSlot != null)
            {
                targetSource = ProfileItemSource.EquipmentSlot;
                targetIndex = equipmentSlot.slotIndex;
                return true;
            }

            ProfileCraftingSlotDragHandler craftingSlot = current.GetComponent<ProfileCraftingSlotDragHandler>();
            if (craftingSlot != null)
            {
                targetSource = ProfileItemSource.CraftingSlot;
                targetIndex = craftingSlot.slotIndex;
                return true;
            }

            current = current.parent;
        }

        if (TryResolveDropTargetFromScreenPosition(eventData, out targetSource, out targetIndex))
            return true;

        return false;
    }

    bool TryResolveDropTargetFromScreenPosition(PointerEventData eventData, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        if (eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera;
        Vector2 screenPosition = eventData.position;

        if (TryResolveInventoryButtonDrop(playerInventoryButtons, true, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        if (TryResolveInventoryButtonDrop(shipInventoryButtons, false, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        if (TryResolveIndexedButtonDrop(equipmentSlotButtons, ProfileItemSource.EquipmentSlot, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        if (TryResolveIndexedButtonDrop(craftingSlotButtons, ProfileItemSource.CraftingSlot, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        RectTransform craftingPanelRect = craftingPanelObject != null
            ? craftingPanelObject.GetComponent<RectTransform>()
            : null;
        if (craftingPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(craftingPanelRect, screenPosition, eventCamera))
        {
            targetSource = ProfileItemSource.CraftingSlot;
            targetIndex = FindFirstFreeCraftingSlot();
            return targetIndex >= 0;
        }

        RectTransform playerViewportRect = playerInventoryScrollRect != null
            ? playerInventoryScrollRect.GetComponent<RectTransform>()
            : null;
        if (playerViewportRect != null && RectTransformUtility.RectangleContainsScreenPoint(playerViewportRect, screenPosition, eventCamera))
        {
            targetSource = ProfileItemSource.PlayerInventory;
            targetIndex = FindFirstFreePlayerInventorySlot();
            return true;
        }

        return false;
    }

    bool TryResolveInventoryButtonDrop(Button[] buttons, bool isPlayerInventory, Vector2 screenPosition, Camera eventCamera, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        if (buttons == null)
            return false;

        int shipCapacity = GetActiveShipInventoryCapacity();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            if (!isPlayerInventory && i >= shipCapacity)
                continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (!ExpandedRectangleContainsScreenPoint(rect, screenPosition, eventCamera, InventoryDropTargetPadding))
                continue;

            targetSource = ProfileItemSourceFromInventory(isPlayerInventory);
            targetIndex = isPlayerInventory ? ResolveVisiblePlayerInventorySlotIndex(i) : i;
            if (targetIndex >= 0)
                return true;
        }

        return false;
    }

    bool TryResolveIndexedButtonDrop(Button[] buttons, ProfileItemSource source, Vector2 screenPosition, Camera eventCamera, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        if (buttons == null)
            return false;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (!ExpandedRectangleContainsScreenPoint(rect, screenPosition, eventCamera, InventoryDropTargetPadding))
                continue;

            targetSource = source;
            targetIndex = i;
            return true;
        }

        return false;
    }

    bool ExpandedRectangleContainsScreenPoint(RectTransform rect, Vector2 screenPosition, Camera eventCamera, float padding)
    {
        if (rect == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        Rect localRect = rect.rect;
        localRect.xMin -= padding;
        localRect.xMax += padding;
        localRect.yMin -= padding;
        localRect.yMax += padding;
        return localRect.Contains(localPoint);
    }

    int FindFirstFreePlayerInventorySlot()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null || profile.Inventory.PlayerSlots == null)
            return -1;

        profile.Inventory.Normalize();
        for (int i = 0; i < profile.Inventory.PlayerSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(profile.Inventory.PlayerSlots[i]))
                return i;
        }

        return -1;
    }

    int FindFirstFreeCraftingSlot()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null || profile.Inventory.CraftingSlots == null)
            return -1;

        profile.Inventory.Normalize();
        for (int i = 0; i < profile.Inventory.CraftingSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(profile.Inventory.CraftingSlots[i]))
                return i;
        }

        return -1;
    }

    async void OnItemPreviewSellClicked()
    {
        if (inventoryActionInProgress || previewSource == ProfileItemSource.None || previewSource == ProfileItemSource.EquipmentSlot)
            return;

        bool isShipInventory = previewSource == ProfileItemSource.ShipInventory;
        string itemId = previewItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            suppressNextProfileChangedRefresh = true;
            int value = InventoryItemCatalog.GetSellValueAstrons(itemId);
            bool sold = await PlayerProfileService.Instance.SellInventoryItemAsync(isShipInventory, previewSlotIndex);
            if (sold)
            {
                SetStatus("Sold for " + value + " Astrons.");
                AudioManager.Instance?.PlayCash();
                HideItemPreview();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Inventory sell failed: " + ex);
            SetStatus("Sell failed.");
        }
        finally
        {
            suppressNextProfileChangedRefresh = false;
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshProfileSummaryAndInventory();
        }
    }

    async void OnItemPreviewSalvageClicked()
    {
        if (inventoryActionInProgress || previewSource == ProfileItemSource.None || previewSource == ProfileItemSource.EquipmentSlot)
            return;

        bool isShipInventory = previewSource == ProfileItemSource.ShipInventory;
        string itemId = previewItemId;
        bool isBlueprint = InventoryItemCatalog.IsBlueprintItem(itemId);
        if (isBlueprint && !PlayerProfileService.Instance.IsBlueprintUnlocked(itemId))
        {
            await UsePreviewedBlueprintAsync(isShipInventory);
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool salvaged = await PlayerProfileService.Instance.SalvageInventoryItemAsync(isShipInventory, previewSlotIndex);
            if (salvaged)
            {
                SetStatus(isBlueprint
                    ? "Blueprint salvaged into " + InventoryItemCatalog.GetDisplayName(InventoryItemCatalog.BlueprintScrapId) + "."
                    : "Item salvaged.");
                HideItemPreview();
            }
            else
            {
                SetStatus("No more free inventory slots");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Inventory salvage failed: " + ex);
            SetStatus("Salvage failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshView();
        }
    }

    async Task UsePreviewedBlueprintAsync(bool isShipInventory)
    {
        string blueprintItemId = previewItemId;
        if (string.IsNullOrWhiteSpace(blueprintItemId))
            return;

        if (PlayerProfileService.Instance.IsBlueprintUnlocked(blueprintItemId))
        {
            SetStatus("Blueprint already learned.");
            HideItemPreview();
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool used = await PlayerProfileService.Instance.UseBlueprintItemAsync(isShipInventory, previewSlotIndex);
            if (used)
            {
                string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(blueprintItemId);
                SetStatus("Blueprint learned: " + InventoryItemCatalog.GetDisplayName(targetItemId) + ".");
                HideItemPreview();
            }
            else
            {
                SetStatus("Blueprint use failed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Blueprint use failed: " + ex);
            SetStatus("Blueprint use failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshView();
            RefreshCraftingRecipeBrowser(true);
        }
    }

    ProfileItemSource ProfileItemSourceFromInventory(bool isPlayerInventory)
    {
        return isPlayerInventory ? ProfileItemSource.PlayerInventory : ProfileItemSource.ShipInventory;
    }

    async System.Threading.Tasks.Task<bool> MoveItemToTargetAsync(ProfileItemSource source, int sourceIndex, ProfileItemSource targetSource, int targetIndex)
    {
        if (source == targetSource &&
            source != ProfileItemSource.PlayerInventory &&
            source != ProfileItemSource.ShipInventory)
        {
            return false;
        }

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return false;

        PlayerInventoryData workingInventory = profile.Inventory != null ? profile.Inventory.Clone() : PlayerInventoryData.Default();
        if (!TryMoveCraftingAwareItem(workingInventory, source, sourceIndex, targetSource, targetIndex))
            return false;

        suppressNextProfileChangedRefresh = true;
        try
        {
            Task saveTask = PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            RefreshProfileSummaryAndInventory();
            await saveTask;
            return true;
        }
        finally
        {
            suppressNextProfileChangedRefresh = false;
        }
    }

    string GetMoveSuccessMessage(ProfileItemSource targetSource)
    {
        return targetSource switch
        {
            ProfileItemSource.PlayerInventory => "Moved item to player inventory.",
            ProfileItemSource.ShipInventory => "Moved item to ship inventory.",
            ProfileItemSource.EquipmentSlot => "Moved item to loadout slot.",
            ProfileItemSource.CraftingSlot => "Moved item to crafting slot.",
            _ => "Item moved."
        };
    }

    string GetMoveFailureMessage(ProfileItemSource targetSource)
    {
        return targetSource switch
        {
            ProfileItemSource.PlayerInventory => "No free player slot for this item.",
            ProfileItemSource.ShipInventory => "No free ship slot for this item.",
            ProfileItemSource.EquipmentSlot => "No free compatible loadout slot.",
            ProfileItemSource.CraftingSlot => "Crafting slot is occupied.",
            _ => "Inventory update failed."
        };
    }

    bool TryMoveCraftingAwareItem(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, ProfileItemSource targetSource, int targetIndex)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        string candidateItem = PeekItemFromSource(inventory, source, sourceIndex);
        if (targetSource == ProfileItemSource.EquipmentSlot && !InventoryItemCatalog.IsCompatibleWithEquipmentSlot(candidateItem, targetIndex))
            return false;

        if (!TryTakeItemFromSource(inventory, source, sourceIndex, out string movedItem))
            return false;

        if (TryPlaceItemAtTarget(inventory, targetSource, targetIndex, movedItem, source, sourceIndex))
            return true;

        RestoreItemToSource(inventory, source, sourceIndex, movedItem);
        return false;
    }

    bool TryTakeItemFromSource(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, out string itemId)
    {
        itemId = source switch
        {
            ProfileItemSource.PlayerInventory => inventory.RemoveFromPlayer(sourceIndex),
            ProfileItemSource.ShipInventory => inventory.RemoveFromShip(sourceIndex),
            ProfileItemSource.EquipmentSlot => IsEquipmentSlotEnabledForSelectedSkin(sourceIndex) ? inventory.RemoveFromEquipment(sourceIndex) : null,
            ProfileItemSource.CraftingSlot => inventory.RemoveFromCrafting(sourceIndex),
            _ => null
        };

        return !string.IsNullOrWhiteSpace(itemId);
    }

    string PeekItemFromSource(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex)
    {
        if (inventory == null)
            return null;

        return source switch
        {
            ProfileItemSource.PlayerInventory => GetSlotItem(inventory.PlayerSlots, sourceIndex),
            ProfileItemSource.ShipInventory => GetSlotItem(inventory.ShipSlots, sourceIndex),
            ProfileItemSource.EquipmentSlot => IsEquipmentSlotEnabledForSelectedSkin(sourceIndex) ? GetSlotItem(inventory.EquipmentSlots, sourceIndex) : null,
            ProfileItemSource.CraftingSlot => GetSlotItem(inventory.CraftingSlots, sourceIndex),
            _ => null
        };
    }

    string GetSlotItem(string[] slots, int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    bool TryPlaceItemAtTarget(PlayerInventoryData inventory, ProfileItemSource targetSource, int targetIndex, string itemId, ProfileItemSource source, int sourceIndex)
    {
        switch (targetSource)
        {
            case ProfileItemSource.PlayerInventory:
                if (source == ProfileItemSource.EquipmentSlot)
                    return TryPlaceEquipmentItemIntoPlayerInventory(inventory, targetIndex, itemId, sourceIndex);

                return TryPlaceItemIntoIndexedSlot(inventory.PlayerSlots, inventory.PlayerSlots != null ? inventory.PlayerSlots.Length : 0, targetIndex, itemId, inventory, source, sourceIndex);

            case ProfileItemSource.ShipInventory:
                if (!PlayerProfileService.CanStoreItemInShipSlot(itemId, GetActiveProfileShipSkinIndex(), targetIndex))
                    return false;
                return TryPlaceItemIntoIndexedSlot(inventory.ShipSlots, GetActiveShipInventoryCapacity(), targetIndex, itemId, inventory, source, sourceIndex);

            case ProfileItemSource.CraftingSlot:
                if (targetIndex < 0 || targetIndex >= PlayerInventoryData.CraftingSlotCount)
                    return false;
                if (!string.IsNullOrWhiteSpace(inventory.CraftingSlots[targetIndex]))
                {
                    string replacedCraftingItem = inventory.CraftingSlots[targetIndex];
                    inventory.SetCrafting(targetIndex, itemId);
                    if (TryReturnItemToSourceSlot(inventory, source, sourceIndex, replacedCraftingItem))
                        return true;

                    inventory.SetCrafting(targetIndex, replacedCraftingItem);
                    return false;
                }
                inventory.SetCrafting(targetIndex, itemId);
                return true;

            case ProfileItemSource.EquipmentSlot:
                if (!IsEquipmentSlotEnabledForSelectedSkin(targetIndex))
                    return false;
                if (!InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, targetIndex))
                    return false;

                string replacedItem = inventory.RemoveFromEquipment(targetIndex);
                inventory.SetEquipment(targetIndex, itemId);

                if (string.IsNullOrWhiteSpace(replacedItem))
                    return true;

                if (TryReturnItemToSourceSlot(inventory, source, sourceIndex, replacedItem))
                    return true;

                inventory.SetEquipment(targetIndex, replacedItem);
                return false;
        }

        return false;
    }

    bool TryPlaceEquipmentItemIntoPlayerInventory(PlayerInventoryData inventory, int targetIndex, string itemId, int equipmentSlotIndex)
    {
        if (inventory == null ||
            inventory.PlayerSlots == null ||
            targetIndex < 0 ||
            targetIndex >= inventory.PlayerSlots.Length ||
            !IsEquipmentSlotEnabledForSelectedSkin(equipmentSlotIndex))
        {
            return false;
        }

        string targetItem = inventory.PlayerSlots[targetIndex];
        if (string.IsNullOrWhiteSpace(targetItem))
        {
            inventory.PlayerSlots[targetIndex] = itemId;
            return true;
        }

        if (TryReturnItemToSourceSlot(inventory, ProfileItemSource.EquipmentSlot, equipmentSlotIndex, targetItem))
        {
            inventory.PlayerSlots[targetIndex] = itemId;
            return true;
        }

        int fallbackIndex = inventory.GetFirstEmptyPlayerSlot();
        if (fallbackIndex < 0)
            return false;

        inventory.PlayerSlots[fallbackIndex] = itemId;
        return true;
    }

    bool TryPlaceItemIntoIndexedSlot(string[] slots, int capacity, int targetIndex, string itemId, PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex)
    {
        if (slots == null || targetIndex < 0 || targetIndex >= slots.Length || targetIndex >= capacity)
            return false;

        string replacedItem = slots[targetIndex];
        slots[targetIndex] = itemId;

        if (string.IsNullOrWhiteSpace(replacedItem))
            return true;

        if (TryReturnItemToSourceSlot(inventory, source, sourceIndex, replacedItem))
            return true;

        slots[targetIndex] = replacedItem;
        return false;
    }

    bool TryReturnItemToSourceSlot(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, string itemId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        switch (source)
        {
            case ProfileItemSource.PlayerInventory:
                if (sourceIndex < 0 || sourceIndex >= inventory.PlayerSlots.Length)
                    return false;
                inventory.RestorePlayer(sourceIndex, itemId);
                return true;

            case ProfileItemSource.ShipInventory:
                if (sourceIndex < 0 || sourceIndex >= GetActiveShipInventoryCapacity())
                    return false;
                if (!PlayerProfileService.CanStoreItemInShipSlot(itemId, GetActiveProfileShipSkinIndex(), sourceIndex))
                    return false;
                inventory.RestoreShip(sourceIndex, itemId);
                return true;

            case ProfileItemSource.CraftingSlot:
                if (sourceIndex < 0 || sourceIndex >= PlayerInventoryData.CraftingSlotCount)
                    return false;
                inventory.SetCrafting(sourceIndex, itemId);
                return true;

            case ProfileItemSource.EquipmentSlot:
                if (!IsEquipmentSlotEnabledForSelectedSkin(sourceIndex))
                    return false;
                if (!InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, sourceIndex))
                    return false;
                inventory.SetEquipment(sourceIndex, itemId);
                return true;
        }

        return false;
    }

    void RestoreItemToSource(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, string itemId)
    {
        switch (source)
        {
            case ProfileItemSource.PlayerInventory:
                inventory.RestorePlayer(sourceIndex, itemId);
                break;
            case ProfileItemSource.ShipInventory:
                inventory.RestoreShip(sourceIndex, itemId);
                break;
            case ProfileItemSource.EquipmentSlot:
                inventory.SetEquipment(sourceIndex, itemId);
                break;
            case ProfileItemSource.CraftingSlot:
                inventory.SetCrafting(sourceIndex, itemId);
                break;
        }
    }

    bool IsCraftingGridOccupied()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return false;

        profile.Inventory.Normalize();

        for (int i = 0; i < profile.Inventory.CraftingSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(profile.Inventory.CraftingSlots[i]))
                return true;
        }

        return false;
    }
}
