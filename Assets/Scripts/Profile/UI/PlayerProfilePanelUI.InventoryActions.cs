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
    void OnPlayerInventoryExtendClicked()
    {
        if (inventoryActionInProgress || PlayerProfileService.Instance.CurrentProfile == null)
            return;

        int price = PlayerProfileService.Instance.GetNextPlayerInventoryExtendPrice();
        if (playerInventoryExtendConfirmText != null)
        {
            int addedSlots = PlayerInventoryData.PlayerSlotExtensionSize;
            string slotLabel = addedSlots == 1 ? " slot" : " slots";
            playerInventoryExtendConfirmText.text = "Do you want to extend player inventory by " + addedSlots + slotLabel + " for " + price + " Astrons?";
        }

        if (playerInventoryExtendConfirmObject != null)
        {
            playerInventoryExtendConfirmObject.SetActive(true);
            EnsureProfileModalLayering();
        }
    }

    async void OnShipInventoryUnloadClicked()
    {
        if (inventoryActionInProgress || PlayerProfileService.Instance.CurrentProfile == null)
            return;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile.Inventory;
        if (!HasShipInventoryItems(inventory))
        {
            SetStatus("Ship inventory is empty.");
            return;
        }

        if (!HasFreePlayerInventorySlot(inventory))
        {
            SetStatus("No free player inventory slots.");
            return;
        }

        try
        {
            inventoryActionInProgress = true;
            SetInteractable(false);
            SetStatus("Unloading ship inventory...");

            int movedCount = await PlayerProfileService.Instance.UnloadShipInventoryToPlayerAsync();
            if (movedCount > 0)
            {
                HideItemPreview();
                SetStatus("Unloaded " + movedCount + (movedCount == 1 ? " item." : " items."));
            }
            else
            {
                SetStatus("No free player inventory slots.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Ship inventory unload failed: " + ex);
            SetStatus("Unload failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(!NetworkManager.SessionRequested);
            RefreshView();
        }
    }

    bool HasShipInventoryItems(PlayerInventoryData inventory)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        for (int i = 0; i < inventory.ShipSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.ShipSlots[i]))
                return true;
        }

        return false;
    }

    bool HasFreePlayerInventorySlot(PlayerInventoryData inventory)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        for (int i = 0; i < inventory.PlayerSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(inventory.PlayerSlots[i]))
                return true;
        }

        return false;
    }

    void HidePlayerInventoryExtendConfirm()
    {
        if (playerInventoryExtendConfirmObject != null)
            playerInventoryExtendConfirmObject.SetActive(false);
    }

    async void OnPlayerInventoryExtendConfirmClicked()
    {
        if (inventoryActionInProgress)
            return;

        int price = PlayerProfileService.Instance.GetNextPlayerInventoryExtendPrice();
        try
        {
            inventoryActionInProgress = true;
            SetInteractable(false);
            bool extended = await PlayerProfileService.Instance.TryExtendPlayerInventoryAsync();
            HidePlayerInventoryExtendConfirm();
            if (extended)
            {
                SetStatus("Player inventory extended by " + PlayerInventoryData.PlayerSlotExtensionSize + " slots.");
                AudioManager.Instance?.PlayCash();
            }
            else
                SetStatus("Not enough Astrons. Need " + price + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("Player inventory extend failed: " + ex);
            SetStatus("Inventory extension failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(!NetworkManager.SessionRequested);
            RefreshView();
        }
    }

    void ApplyInteractableIfChanged(bool interactable)
    {
        if (interactableStateInitialized &&
            lastAppliedInteractable == interactable &&
            lastAppliedInventoryActionInProgress == inventoryActionInProgress &&
            lastAppliedPreserveInventoryButtonVisualsDuringSave == preserveInventoryButtonVisualsDuringSave)
        {
            return;
        }

        SetInteractable(interactable);
    }

    void InvalidateInteractableState()
    {
        interactableStateInitialized = false;
    }

    void RememberInteractableState(bool interactable)
    {
        interactableStateInitialized = true;
        lastAppliedInteractable = interactable;
        lastAppliedInventoryActionInProgress = inventoryActionInProgress;
        lastAppliedPreserveInventoryButtonVisualsDuringSave = preserveInventoryButtonVisualsDuringSave;
    }

    void SetInteractable(bool interactable)
    {
        if (nicknameInput != null)
            nicknameInput.interactable = interactable;

        if (saveAndRunButton != null)
            saveAndRunButton.interactable = interactable;

        if (shopButton != null)
            shopButton.interactable = interactable;

        if (navCraftingButton != null)
            navCraftingButton.interactable = interactable;

        if (navInventoryButton != null)
            navInventoryButton.interactable = interactable;

        if (navPlayerButton != null)
            navPlayerButton.interactable = interactable;

        if (navBackButton != null)
            navBackButton.interactable = interactable;

        if (exitGameButton != null)
            exitGameButton.interactable = interactable;

        if (shipTypeButtons != null)
        {
            for (int i = 0; i < shipTypeButtons.Length; i++)
            {
                if (shipTypeButtons[i] != null)
                    shipTypeButtons[i].interactable = interactable;
            }
        }

        if (skinButtons != null)
        {
            for (int i = 0; i < skinButtons.Length; i++)
            {
                if (skinButtons[i] != null)
                    skinButtons[i].interactable = interactable;
            }
        }

        if (shipSelectionBackButton != null)
            shipSelectionBackButton.interactable = interactable;
        if (shipSelectionPrevButton != null)
            shipSelectionPrevButton.interactable = interactable;
        if (shipSelectionNextButton != null)
            shipSelectionNextButton.interactable = interactable;
        if (shipSelectionDetailsButton != null)
            shipSelectionDetailsButton.interactable = interactable && shipSelectionDetailsButton.gameObject.activeSelf;
        if (shipSelectionSkinButtons != null)
        {
            for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
            {
                if (shipSelectionSkinButtons[i] != null)
                    shipSelectionSkinButtons[i].interactable = interactable;
            }
        }

        if (pilotPortraitButton != null)
            pilotPortraitButton.interactable = interactable;
        if (projectsButton != null)
            projectsButton.interactable = interactable;
        SetProjectButtonsInteractable(interactable && !inventoryActionInProgress);
        if (pilotSelectionBackButton != null)
            pilotSelectionBackButton.interactable = interactable;
        if (pilotSelectionPrevButton != null)
            pilotSelectionPrevButton.interactable = interactable;
        if (pilotSelectionNextButton != null)
            pilotSelectionNextButton.interactable = interactable;

        SetInventoryInteractable(interactable && !inventoryActionInProgress);
        RememberInteractableState(interactable);
    }

    void SetProjectButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < projectTileButtons.Count; i++)
        {
            if (projectTileButtons[i] != null)
                projectTileButtons[i].interactable = interactable;
        }

        for (int i = 0; i < projectStageTabButtons.Count; i++)
        {
            if (projectStageTabButtons[i] != null)
                projectStageTabButtons[i].interactable = interactable;
        }

        for (int i = 0; i < projectStepButtons.Count; i++)
        {
            if (projectStepButtons[i] != null)
                projectStepButtons[i].interactable = interactable;
        }

        if (projectCommitMinusButton != null)
            projectCommitMinusButton.interactable = interactable;
        if (projectCommitPlusButton != null)
            projectCommitPlusButton.interactable = interactable;
        if (projectCommitButton != null)
            projectCommitButton.interactable = interactable;
        if (projectRewardClaimButton != null)
            projectRewardClaimButton.interactable = interactable;
    }

    void SetInventoryInteractable(bool interactable)
    {
        inventoryControlsInteractable = interactable;
        bool visualInteractable = interactable || preserveInventoryButtonVisualsDuringSave;
        SetInventoryButtonState(playerInventoryButtons, interactable);
        SetInventoryButtonState(shipInventoryButtons, interactable);
        SetInventoryButtonState(equipmentSlotButtons, interactable);
        SetInventoryButtonState(craftingSlotButtons, interactable);
        if (shipPreviewButton != null)
            shipPreviewButton.interactable = interactable && shipPreviewImage != null && shipPreviewImage.sprite != null;
        if (craftingCatalogButton != null)
            craftingCatalogButton.interactable = interactable;
        if (craftingRecipeCloseButton != null)
            craftingRecipeCloseButton.interactable = interactable;
        if (craftingRecipeAvailabilityButton != null)
            craftingRecipeAvailabilityButton.interactable = visualInteractable;
        if (craftingRecipeBlueprintsButton != null)
            craftingRecipeBlueprintsButton.interactable = visualInteractable;
        if (craftingBlueprintCloseButton != null)
            craftingBlueprintCloseButton.interactable = interactable;
        SetCraftingRecipeRowsInteractable(interactable);
        if (itemPreviewSellButton != null)
            itemPreviewSellButton.interactable = visualInteractable;
        if (itemPreviewSalvageButton != null)
            itemPreviewSalvageButton.interactable = visualInteractable;
        if (itemPreviewInfoButton != null)
            itemPreviewInfoButton.interactable = visualInteractable;
        if (itemInfoCloseButton != null)
            itemInfoCloseButton.interactable = interactable;
        if (craftButton != null)
            craftButton.interactable = visualInteractable;
        if (clearCraftButton != null)
            clearCraftButton.interactable = visualInteractable;
        if (playerInventoryExtendButton != null)
            playerInventoryExtendButton.interactable = visualInteractable;
        if (playerInventoryFilterButton != null)
            playerInventoryFilterButton.interactable = visualInteractable;
        if (playerInventorySortButton != null)
            playerInventorySortButton.interactable = visualInteractable;
        if (shipInventoryUnloadButton != null)
            shipInventoryUnloadButton.interactable = visualInteractable;
        if (playerInventoryExtendConfirmButton != null)
            playerInventoryExtendConfirmButton.interactable = interactable;
        if (playerInventoryExtendCancelButton != null)
            playerInventoryExtendCancelButton.interactable = interactable;
        if (shipInventoryStartConfirmYesButton != null)
            shipInventoryStartConfirmYesButton.interactable = interactable;
        if (shipInventoryStartConfirmNoButton != null)
            shipInventoryStartConfirmNoButton.interactable = interactable;
        if (shopCloseButton != null)
            shopCloseButton.interactable = interactable;
        RefreshShopSortButton();
        RefreshTraderSelectionVisuals();
    }

    void SetInventoryButtonState(Button[] buttons, bool interactable)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].interactable = interactable || preserveInventoryButtonVisualsDuringSave;
        }
    }

    void OnInventorySlotClicked(bool isPlayerInventory, int slotIndex)
    {
        if (suppressNextInventoryClick)
        {
            suppressNextInventoryClick = false;
            return;
        }

        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        int resolvedSlotIndex = ResolveInventorySlotIndex(isPlayerInventory, slotIndex);
        if (TryGetInventoryItemId(isPlayerInventory, slotIndex, out string itemId))
        {
            if (IsPreviewingSameItem(ProfileItemSourceFromInventory(isPlayerInventory), resolvedSlotIndex, itemId))
            {
                HideItemPreview();
                SetStatus(string.Empty);
                return;
            }

            ShowItemPreview(ProfileItemSourceFromInventory(isPlayerInventory), resolvedSlotIndex, itemId);
            SetStatus(isPlayerInventory ? "Player item selected." : "Ship item selected.");
        }
        else
        {
            if (currentScreen == ProfileScreen.Home && IsEquipmentSlotEnabledForSelectedSkin(slotIndex))
            {
                HideItemPreview();
                SetStatus(string.Empty);
                SwitchToScreen(ProfileScreen.Inventory);
                return;
            }

            HideItemPreview();
            SetStatus(string.Empty);
        }
    }

    void OnCraftingSlotClicked(int slotIndex)
    {
        if (suppressNextInventoryClick)
        {
            suppressNextInventoryClick = false;
            return;
        }

        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (TryGetCraftingItemId(slotIndex, out string itemId))
        {
            if (IsPreviewingSameItem(ProfileItemSource.CraftingSlot, slotIndex, itemId))
            {
                HideItemPreview();
                SetStatus(string.Empty);
                return;
            }

            ShowItemPreview(ProfileItemSource.CraftingSlot, slotIndex, itemId);
            SetStatus("Crafting item selected.");
        }
        else
        {
            HideItemPreview();
            SetStatus(string.Empty);
        }
    }

    public void BeginSlotDrag(bool isPlayerInventory, int slotIndex, PointerEventData eventData)
    {
        if (inventoryActionInProgress || !panelObject.activeSelf)
            return;

        int resolvedSlotIndex = ResolveInventorySlotIndex(isPlayerInventory, slotIndex);
        if (resolvedSlotIndex < 0 || !TryGetInventoryItemId(isPlayerInventory, slotIndex, out string itemId))
            return;

        dragInProgress = true;
        suppressNextInventoryClick = true;
        ShowItemPreview(ProfileItemSourceFromInventory(isPlayerInventory), resolvedSlotIndex, itemId);
        EnsureDragVisual();
        UpdateDragVisualContent(itemId);
        UpdateDragVisualPosition(eventData);
        dragVisualObject.SetActive(true);
    }

    public void BeginCraftingSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (!TryGetCraftingItemId(slotIndex, out string itemId))
            return;

        dragInProgress = true;
        suppressNextInventoryClick = true;
        ShowItemPreview(ProfileItemSource.CraftingSlot, slotIndex, itemId);
        EnsureDragVisual();
        UpdateDragVisualContent(itemId);
        UpdateDragVisualPosition(eventData);
        dragVisualObject.SetActive(true);
    }

    public void UpdateSlotDrag(bool isPlayerInventory, int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress || dragVisualObject == null)
            return;

        UpdateDragVisualPosition(eventData);
    }

    public void UpdateCraftingSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress || dragVisualObject == null)
            return;

        UpdateDragVisualPosition(eventData);
    }

    public async void EndSlotDrag(bool isPlayerInventory, int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress)
            return;

        dragInProgress = false;
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);

        ProfileItemSource source = ProfileItemSourceFromInventory(isPlayerInventory);
        int resolvedSourceIndex = ResolveProfileSlotIndex(source, slotIndex);
        if (!ResolveDropTarget(eventData, out ProfileItemSource targetSource, out int targetIndex))
            return;

        if (resolvedSourceIndex < 0 || targetIndex < 0)
        {
            if (targetSource != ProfileItemSource.None)
                SetStatus(GetMoveFailureMessage(targetSource));
            return;
        }

        await CompleteInventoryMoveAsync(source, resolvedSourceIndex, targetSource, targetIndex, "Inventory move failed: ");
    }

    public async void EndCraftingSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress)
            return;

        dragInProgress = false;
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);

        if (!ResolveDropTarget(eventData, out ProfileItemSource targetSource, out int targetIndex))
            return;

        if (targetIndex < 0)
        {
            SetStatus(GetMoveFailureMessage(targetSource));
            return;
        }

        await CompleteInventoryMoveAsync(ProfileItemSource.CraftingSlot, slotIndex, targetSource, targetIndex, "Crafting move failed: ");
    }

    public void BeginEquipmentSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (!TryGetEquipmentItemId(slotIndex, out string itemId))
            return;

        dragInProgress = true;
        suppressNextInventoryClick = true;
        ShowItemPreview(ProfileItemSource.EquipmentSlot, slotIndex, itemId);
        EnsureDragVisual();
        UpdateDragVisualContent(itemId);
        UpdateDragVisualPosition(eventData);
        dragVisualObject.SetActive(true);
    }

    bool TryGetInventoryItemId(bool isPlayerInventory, int slotIndex, out string itemId)
    {
        itemId = null;
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return false;

        int resolvedSlotIndex = ResolveInventorySlotIndex(isPlayerInventory, slotIndex);
        if (resolvedSlotIndex < 0)
            return false;

        string[] slots = isPlayerInventory ? profile.Inventory.PlayerSlots : profile.Inventory.ShipSlots;
        if (slots == null || resolvedSlotIndex < 0 || resolvedSlotIndex >= slots.Length)
            return false;

        itemId = slots[resolvedSlotIndex];
        return !string.IsNullOrWhiteSpace(itemId);
    }

    int ResolveInventorySlotIndex(bool isPlayerInventory, int slotIndex)
    {
        return isPlayerInventory ? ResolveVisiblePlayerInventorySlotIndex(slotIndex) : slotIndex;
    }

    int ResolveProfileSlotIndex(ProfileItemSource source, int slotIndex)
    {
        return source == ProfileItemSource.PlayerInventory
            ? ResolveVisiblePlayerInventorySlotIndex(slotIndex)
            : slotIndex;
    }

    bool TryGetEquipmentItemId(int slotIndex, out string itemId)
    {
        itemId = null;
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null || profile.Inventory.EquipmentSlots == null)
            return false;

        if (slotIndex < 0 || slotIndex >= profile.Inventory.EquipmentSlots.Length)
            return false;

        itemId = profile.Inventory.EquipmentSlots[slotIndex];
        return !string.IsNullOrWhiteSpace(itemId);
    }

    bool TryGetCraftingItemId(int slotIndex, out string itemId)
    {
        itemId = null;
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return false;

        profile.Inventory.Normalize();

        if (slotIndex < 0 || slotIndex >= profile.Inventory.CraftingSlots.Length)
            return false;

        itemId = profile.Inventory.CraftingSlots[slotIndex];
        return !string.IsNullOrWhiteSpace(itemId);
    }

    void OnEquipmentSlotClicked(int slotIndex)
    {
        if (suppressNextInventoryClick)
        {
            suppressNextInventoryClick = false;
            return;
        }

        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (TryGetEquipmentItemId(slotIndex, out string itemId))
        {
            ShowItemPreview(ProfileItemSource.EquipmentSlot, slotIndex, itemId);
            if (ApplyEquipmentSlotPlayerInventoryFilter(slotIndex))
                return;

            SetStatus("Equipment item selected.");
        }
        else
        {
            HideItemPreview();
            if (ApplyEquipmentSlotPlayerInventoryFilter(slotIndex))
                return;

            SetStatus(string.Empty);
        }
    }

    bool ApplyEquipmentSlotPlayerInventoryFilter(int slotIndex)
    {
        if (!IsEquipmentSlotEnabledForSelectedSkin(slotIndex))
            return false;

        SetPlayerInventoryFilter(PlayerInventoryFilterMode.CustomEquipmentSlot, slotIndex);
        resetPlayerInventoryScrollOnNextRefresh = true;
        InventoryItemCategory category = InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex);
        SetStatus("Showing " + FormatInventoryFilterCategory(category) + " items.");

        if (currentScreen != ProfileScreen.Inventory)
        {
            SwitchToScreen(ProfileScreen.Inventory, false);
            RefreshView();
            return true;
        }

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile != null)
            RefreshInventoryView(profile.Inventory);

        return true;
    }

    string FormatInventoryFilterCategory(InventoryItemCategory category)
    {
        return category switch
        {
            InventoryItemCategory.Weapon => "weapon",
            InventoryItemCategory.Shield => "shield",
            InventoryItemCategory.Engine => "engine",
            InventoryItemCategory.Gadget => "gadget",
            InventoryItemCategory.Support => "support",
            InventoryItemCategory.Rescue => "rescue",
            _ => "compatible"
        };
    }

    bool IsPreviewingSameItem(ProfileItemSource source, int slotIndex, string itemId)
    {
        return itemPreviewPanelObject != null &&
               itemPreviewPanelObject.activeSelf &&
               previewSource == source &&
               previewSlotIndex == slotIndex &&
               string.Equals(previewItemId, itemId, StringComparison.Ordinal);
    }

    public void UpdateEquipmentSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress || dragVisualObject == null)
            return;

        UpdateDragVisualPosition(eventData);
    }

    public async void EndEquipmentSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress)
            return;

        dragInProgress = false;
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);

        if (!ResolveDropTarget(eventData, out ProfileItemSource targetSource, out int targetIndex))
            return;

        if (targetIndex < 0)
        {
            SetStatus(GetMoveFailureMessage(targetSource));
            return;
        }

        await CompleteInventoryMoveAsync(ProfileItemSource.EquipmentSlot, slotIndex, targetSource, targetIndex, "Equipment move failed: ");
    }

    async Task CompleteInventoryMoveAsync(ProfileItemSource source, int sourceIndex, ProfileItemSource targetSource, int targetIndex, string errorPrefix)
    {
        inventoryActionInProgress = true;
        preserveInventoryButtonVisualsDuringSave = true;

        try
        {
            bool moved = await MoveItemToTargetAsync(source, sourceIndex, targetSource, targetIndex);
            SetStatus(moved ? GetMoveSuccessMessage(targetSource) : GetMoveFailureMessage(targetSource));
        }
        catch (Exception ex)
        {
            Debug.LogError(errorPrefix + ex);
            SetStatus("Inventory update failed.");
            RefreshProfileSummaryAndInventory();
        }
        finally
        {
            preserveInventoryButtonVisualsDuringSave = false;
            inventoryActionInProgress = false;
        }
    }

    void ShowItemPreview(ProfileItemSource source, int slotIndex, string itemId)
    {
        if (itemPreviewPanelObject == null || string.IsNullOrWhiteSpace(itemId))
            return;

        ApplyItemPreviewLayout();
        itemPreviewPanelObject.SetActive(true);
        itemPreviewPanelObject.transform.SetAsLastSibling();
        previewSource = source;
        previewSlotIndex = slotIndex;
        previewItemId = itemId;
        itemPreviewIcon.sprite = InventoryItemCatalog.GetIcon(itemId);
        itemPreviewIcon.enabled = itemPreviewIcon.sprite != null;
        itemPreviewNameText.text = InventoryItemCatalog.GetDisplayName(itemId).ToUpperInvariant();
        if (itemPreviewTypeText != null)
            itemPreviewTypeText.text = InventoryItemCatalog.GetCategoryLabel(itemId);
        itemPreviewPriceText.text = InventoryItemCatalog.GetSellValueAstrons(itemId) + " Astrons";

        Image bg = itemPreviewBackgroundImage != null ? itemPreviewBackgroundImage : itemPreviewPanelObject.GetComponent<Image>();
        if (bg != null)
        {
            Color rarityColor = InventoryItemCatalog.GetRarityColor(itemId);
            bg.color = new Color(
                Mathf.Clamp01(rarityColor.r * 0.55f),
                Mathf.Clamp01(rarityColor.g * 0.55f),
                Mathf.Clamp01(rarityColor.b * 0.55f),
                0.95f);
        }

        bool supportsInventoryActions = source == ProfileItemSource.PlayerInventory || source == ProfileItemSource.ShipInventory;
        bool isBlueprint = InventoryItemCatalog.IsBlueprintItem(itemId);
        bool blueprintUnlocked = isBlueprint && PlayerProfileService.Instance.IsBlueprintUnlocked(itemId);
        if (itemPreviewInfoButton != null)
            itemPreviewInfoButton.gameObject.SetActive(true);
        if (itemPreviewSellButton != null)
            itemPreviewSellButton.gameObject.SetActive(supportsInventoryActions);
        if (itemPreviewSalvageButton != null)
        {
            SetButtonLabel(itemPreviewSalvageButton, isBlueprint && !blueprintUnlocked ? "USE" : "SALVAGE");
            itemPreviewSalvageButton.gameObject.SetActive(supportsInventoryActions);
        }
    }

    void HideItemPreview()
    {
        if (itemPreviewPanelObject != null)
            itemPreviewPanelObject.SetActive(false);

        HideItemInfoOverlay();
        previewSource = ProfileItemSource.None;
        previewSlotIndex = -1;
        previewItemId = null;
    }

    void OnItemPreviewInfoClicked()
    {
        if (string.IsNullOrWhiteSpace(previewItemId))
            return;

        ShowItemInfoOverlay(previewItemId);
    }

    void ShowItemInfoOverlay(string itemId)
    {
        if (itemInfoOverlayObject == null || string.IsNullOrWhiteSpace(itemId))
            return;

        RefreshItemInfoOverlay(itemId);
        itemInfoOverlayObject.SetActive(true);
        EnsureProfileModalLayering();
    }

    void HideItemInfoOverlay()
    {
        if (itemInfoOverlayObject != null)
            itemInfoOverlayObject.SetActive(false);
    }

    void RefreshItemInfoOverlay(string itemId)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        string displayName = definition != null ? definition.DisplayName : InventoryItemCatalog.GetDisplayName(itemId);

        if (itemInfoTitleText != null)
            itemInfoTitleText.text = displayName.ToUpperInvariant();

        if (itemInfoTypeText != null)
            itemInfoTypeText.text = InventoryItemCatalog.GetCategoryLabel(itemId) + "  |  " + InventoryItemCatalog.GetRarity(itemId).ToString();

        if (itemInfoIcon != null)
        {
            itemInfoIcon.sprite = InventoryItemCatalog.GetIcon(itemId);
            itemInfoIcon.enabled = itemInfoIcon.sprite != null;
        }

        if (itemInfoPriceText != null)
            itemInfoPriceText.text = BuildItemInfoPriceText(itemId, definition);

        if (itemInfoSalvageText != null)
        {
            if (InventoryItemCatalog.IsBlueprintItem(itemId))
            {
                string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(itemId);
                bool unlocked = PlayerProfileService.Instance.IsBlueprintUnlocked(itemId);
                itemInfoSalvageText.text = "BLUEPRINT\n" + (unlocked
                    ? "Salvage into " + InventoryItemCatalog.GetDisplayName(InventoryItemCatalog.BlueprintScrapId) + "."
                    : "Use to unlock crafting for " + InventoryItemCatalog.GetDisplayName(targetItemId) + ".");
            }
            else
            {
                itemInfoSalvageText.text = "SALVAGE\n" + (InventoryItemCatalog.HasRandomSalvageOutputs(itemId)
                    ? InventoryItemCatalog.GetRandomSalvageDescription(itemId)
                    : FormatItemIdList(InventoryItemCatalog.GetSalvageOutputs(itemId), "No salvage output."));
            }
        }

        if (itemInfoRecipeText != null)
            itemInfoRecipeText.text = "RECIPE\n" + BuildItemInfoRecipeText(itemId);

        if (itemInfoDescriptionText != null)
            itemInfoDescriptionText.text = "DESCRIPTION\n" + BuildItemInfoDescription(itemId, definition);
    }

    string BuildItemInfoPriceText(string itemId, InventoryItemDefinition definition)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Sell value: ");
        builder.Append(InventoryItemCatalog.GetSellValueAstrons(itemId));
        builder.Append(" Astrons");

        if (definition != null && definition.ItemType == InventoryItemType.Equipment)
        {
            int traderPrice = PlayerProfileService.HasInstance
                ? PlayerProfileService.Instance.GetShopBuyPriceAstrons(itemId)
                : InventoryItemCatalog.GetShopBuyValueAstrons(itemId);
            if (traderPrice > 0)
            {
                builder.Append('\n');
                builder.Append("Trader price: ");
                builder.Append(traderPrice);
                builder.Append(" Astrons");
            }
        }

        return builder.ToString();
    }

    string BuildItemInfoRecipeText(string itemId)
    {
        PlayerProfileCraftingRecipe recipe = PlayerProfileCraftingCatalog.GetRecipeForOutput(itemId);
        if (recipe == null || recipe.Inputs == null || recipe.Inputs.Length == 0)
            return "No crafting recipe.";

        StringBuilder builder = new StringBuilder();
        builder.Append(FormatItemIdList(recipe.Inputs, "No ingredients."));
        int outputCount = Mathf.Max(1, recipe.OutputCount);
        if (outputCount > 1)
        {
            builder.Append('\n');
            builder.Append("Output: ");
            builder.Append(outputCount);
            builder.Append("x ");
            builder.Append(InventoryItemCatalog.GetDisplayName(itemId));
        }

        return builder.ToString();
    }

    string FormatItemIdList(string[] itemIds, string emptyText)
    {
        if (itemIds == null || itemIds.Length == 0)
            return emptyText;

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int count);
            counts[itemId] = count + 1;
        }

        if (counts.Count == 0)
            return emptyText;

        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<string, int> entry in counts)
        {
            if (builder.Length > 0)
                builder.Append('\n');

            if (entry.Value > 1)
            {
                builder.Append(entry.Value);
                builder.Append("x ");
            }

            builder.Append(InventoryItemCatalog.GetDisplayName(entry.Key));
        }

        return builder.ToString();
    }

    string BuildItemInfoDescription(string itemId, InventoryItemDefinition definition)
    {
        if (definition != null && definition.ItemType == InventoryItemType.Equipment)
            return AppendWeaponClassificationDescription(itemId, GetEquipmentGameplayDescription(itemId, definition.Category));

        string description = definition != null ? definition.Description : InventoryItemCatalog.GetDescription(itemId);
        return string.IsNullOrWhiteSpace(description) ? "No additional description." : description;
    }

    string AppendWeaponClassificationDescription(string itemId, string description)
    {
        string classification = WeaponAttackCatalog.BuildEquipmentClassificationSummary(itemId);
        if (string.IsNullOrWhiteSpace(classification))
            return description;

        if (string.IsNullOrWhiteSpace(description))
            return classification;

        return description + "\n\n" + classification;
    }

    string GetEquipmentGameplayDescription(string itemId, InventoryItemCategory category)
    {
        switch (itemId)
        {
            case InventoryItemCatalog.PlasmaGunId:
                return "A reliable combat cannon for direct pressure on hostile ships.";
            case InventoryItemCatalog.TripleGunId:
                return "A simple spread weapon that makes close and medium range aiming more forgiving.";
            case InventoryItemCatalog.GatlingGunId:
                return "A rotary burst weapon that rewards holding aim on one target through a full stream of tiny rounds.";
            case InventoryItemCatalog.ArtilleryGunId:
                return "A heavy launcher for hitting an area instead of a single precise target.";
            case InventoryItemCatalog.RocketLauncherId:
                return "An explosive launcher that can lock onto a target before firing a homing rocket.";
            case InventoryItemCatalog.DoubleRocketLauncherId:
                return "A twin launcher that fires paired rockets after the pilot locks a target.";
            case InventoryItemCatalog.RailGunId:
                return "A precision weapon for fast, piercing shots across open space.";
            case InventoryItemCatalog.DoubleIonizerId:
                return "A shield-focused weapon that pressures protected enemies with paired energy shots.";
            case InventoryItemCatalog.AstroCutterId:
                return "A cutting beam that helps carve through tough targets and space rock.";
            case InventoryItemCatalog.PulseDisruptorId:
                return "A slow shield disruptor that needs a short arming distance and releases a defensive EMP wave as its super.";
            case InventoryItemCatalog.PowerEngineId:
                return "A direct engine upgrade for better cruise speed and stronger booster top speed.";
            case InventoryItemCatalog.IonEngineId:
                return "A responsive engine that favors quick booster recovery and sharper turning.";
            case InventoryItemCatalog.FusionEngineId:
                return "A high-output engine that adds strong speed and faster booster recovery.";
            case InventoryItemCatalog.HybridEngineId:
                return "A balanced engine that blends speed, longer boost endurance, and quicker recovery.";
            case InventoryItemCatalog.DoubleEngineId:
                return "A twin-drive engine for extreme speed and boost output, with shorter boost endurance and heavier handling.";
            case InventoryItemCatalog.FuelTankId:
                return "An engine module for longer booster use during travel, chase, or escape.";
            case InventoryItemCatalog.SuperBoosterId:
                return "A burst engine system for sudden aggressive movement or emergency disengage.";
            case InventoryItemCatalog.AfterburnerStabilizerId:
                return "An engine stabilizer that sharpens turning and halves the delay before booster recovery starts.";
            case InventoryItemCatalog.BlackMarketThrusterId:
                return "An illegal thruster overdrive that adds strong speed and boost output, but reduces shield capacity and makes turning heavier.";
            case InventoryItemCatalog.GadgetMineId:
                return "A deployable trap for protecting an area or punishing pursuing enemies.";
            case InventoryItemCatalog.BatteryId:
                return "A support gadget that helps rebuild shields when the ship needs breathing room.";
            case InventoryItemCatalog.MagneticBeamId:
                return "A utility projector that pulls nearby resources toward the ship.";
            case InventoryItemCatalog.TractorBeamId:
                return "A focused beam for towing one collectible object while the ship keeps moving.";
            case InventoryItemCatalog.LootHookId:
                return "A short-range pirate hook that steals one cargo item from a nearby enemy ship without destroying it.";
            case InventoryItemCatalog.StasisBuoyId:
                return "A deployable buoy that pulses EMP shocks, heavily slowing enemy ships and delaying their fire rate inside its radius.";
            case InventoryItemCatalog.TetherHarpoonId:
                return "A combat tether that latches onto a nearby enemy ship, drags both ships toward tension range, and repeatedly shocks the target.";
            case InventoryItemCatalog.SpaceTorpedoId:
                return "A fast explosive gadget projectile for direct hits, small area bursts, and light asteroid damage.";
            case InventoryItemCatalog.BioTrapId:
                return "A capture net for hostile astronauts that converts one target into a valuable captive pod loot item.";
            case InventoryItemCatalog.AsteroidBreacherBombId:
                return "A breaching charge that detonates the nearest asteroid obstacle and damages ships caught around the blast.";
            case InventoryItemCatalog.LureBeaconId:
                return "A decoy gadget that draws enemy attention away from the pilot.";
            case InventoryItemCatalog.AutoTurretId:
                return "A deployable turret that supports the pilot by firing at nearby enemies.";
            case InventoryItemCatalog.RocketAutoTurretId:
                return "A deployable rocket turret that locks down an area with straight explosive shots.";
            case InventoryItemCatalog.GuidanceSystemId:
                return "A support system that points the pilot toward useful objectives and threats.";
            case InventoryItemCatalog.CloakDeviceId:
                return "A stealth gadget that hides the ship for a short time, but breaks immediately when the pilot fires.";
            case InventoryItemCatalog.LootingFriendId:
                return "A support drone that helps collect nearby loot while the pilot focuses on flying.";
            case InventoryItemCatalog.FiringFriendId:
                return "A support drone that follows the ship and fires short-range laser bursts at nearby enemies.";
            case InventoryItemCatalog.SpaceDrillId:
                return "A mining drone that extracts loot from a nearby asteroid and brings it back.";
            case InventoryItemCatalog.SpaceTrapId:
                return "A sabotage kit that turns a loot object into a dangerous surprise.";
            case InventoryItemCatalog.OverclockedMagazineId:
                return "An illegal ammo overclock that increases weapon capacity, but disables shields and lengthens reload downtime.";
            case InventoryItemCatalog.EmergencySuitBeaconId:
                return "A rescue beacon that gives the astronaut brief protection and a permanent speed boost after losing the ship.";
            case InventoryItemCatalog.EscapePodId:
                return "A rescue capsule that replaces the astronaut after losing the ship.";
            case InventoryItemCatalog.SalvageMagnetArrayId:
                return "A salvage aid that makes wreck loot and random salvage easier to collect.";
            case InventoryItemCatalog.ShieldReactorId:
                return "A defensive reactor that strengthens the ship's protective shield layer.";
            case InventoryItemCatalog.KineticDampenerId:
                return "A defensive module that halves kinetic and contact damage while softening explosive shocks.";
            case InventoryItemCatalog.PhaseShieldId:
                return "A last-moment defensive failsafe that gives the pilot a brief chance to recover.";
            case InventoryItemCatalog.CargoBayExtensionId:
                return "A cargo module installed in a shield slot to carry more ship inventory.";
            case InventoryItemCatalog.StrongPlatingId:
                return "A hull protection module for safer travel through dangerous environmental effects.";
            case InventoryItemCatalog.ShieldCapacitorId:
                return "A compact shield capacitor that adds a large protective energy reserve.";
            case InventoryItemCatalog.AegisBatteryId:
                return "A shield battery bank that makes Battery gadgets rebuild shields much faster.";
            case InventoryItemCatalog.RegenerativeShieldMatrixId:
                return "A regenerative shield module that slowly restores active shields after the ship avoids damage.";
            case InventoryItemCatalog.BulwarkProjectorId:
                return "A heavy shield projector that shrugs off laser and autocannon fire.";
            case InventoryItemCatalog.AlienAegisCoreId:
                return "An alien shield core that briefly hardens the ship after its shield collapses.";
        }

        return category switch
        {
            InventoryItemCategory.Weapon => "A weapon module that changes how the ship attacks enemies.",
            InventoryItemCategory.Engine => "An engine module that changes how the ship moves and escapes.",
            InventoryItemCategory.Shield => "A defensive module that changes how the ship survives damage.",
            InventoryItemCategory.Gadget => "A utility gadget that adds a special tactical action.",
            InventoryItemCategory.Support => "A support module that adds a tactical action or automatic helper system.",
            InventoryItemCategory.Rescue => "A rescue module that improves survival after the ship is lost.",
            _ => "An equipment module that changes the ship's capabilities."
        };
    }
}
