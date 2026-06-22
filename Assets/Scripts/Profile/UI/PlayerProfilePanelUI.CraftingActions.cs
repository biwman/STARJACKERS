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
    async void OnCraftButtonClicked()
    {
        if (inventoryActionInProgress || !panelObject.activeSelf)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        PlayerInventoryData workingInventory = profile.Inventory != null ? profile.Inventory.Clone() : PlayerInventoryData.Default();
        if (!PlayerProfileCraftingCatalog.TryCraft(workingInventory.CraftingSlots, out PlayerProfileCraftingResult craftResult) || craftResult.Recipe == null)
        {
            SetStatus("No matching crafting recipe.");
            return;
        }

        if (!PlayerProfileCraftingCatalog.IsRecipeUnlocked(craftResult.Recipe, profile.UnlockedBlueprintIds))
        {
            SetStatus("Blueprint required for " + InventoryItemCatalog.GetDisplayName(craftResult.Recipe.OutputItemId) + ".");
            return;
        }

        inventoryActionInProgress = true;
        preserveInventoryButtonVisualsDuringSave = true;
        SetInteractable(false);

        try
        {
            int outputCount = Mathf.Max(1, craftResult.Recipe.OutputCount);
            if (CountFreePlayerInventorySlots(workingInventory) < outputCount)
            {
                SetStatus("No inventory space for crafted item.");
                return;
            }

            for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
                workingInventory.SetCrafting(i, null);

            int firstOutputSlot = -1;
            for (int i = 0; i < outputCount; i++)
            {
                int targetSlot = workingInventory.GetFirstEmptyPlayerSlot();
                if (targetSlot < 0)
                {
                    SetStatus("No inventory space for crafted item.");
                    return;
                }

                if (firstOutputSlot < 0)
                    firstOutputSlot = targetSlot;

                workingInventory.PlayerSlots[targetSlot] = craftResult.Recipe.OutputItemId;
            }

            if (!ShouldShowPlayerInventoryItem(craftResult.Recipe.OutputItemId))
            {
                SetPlayerInventoryFilter(PlayerInventoryFilterMode.All, -1);
                resetPlayerInventoryScrollOnNextRefresh = true;
            }

            suppressNextProfileChangedRefresh = true;
            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            ShowItemPreview(ProfileItemSource.PlayerInventory, firstOutputSlot, craftResult.Recipe.OutputItemId);
            SetStatus("Crafted " + InventoryItemCatalog.GetDisplayName(craftResult.Recipe.OutputItemId) + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("Crafting failed: " + ex);
            SetStatus("Crafting failed.");
        }
        finally
        {
            suppressNextProfileChangedRefresh = false;
            inventoryActionInProgress = false;
            SetInteractable(true);
            preserveInventoryButtonVisualsDuringSave = false;
            RefreshProfileSummaryAndInventory();
        }
    }

    async void OnClearCraftingSlotsClicked()
    {
        if (inventoryActionInProgress)
            return;

        await ClearCraftingSlotsAsync(true, false);
    }

    async Task<bool> ClearCraftingSlotsAsync(bool showSuccessStatus, bool silentIfEmpty)
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return true;

        bool hasCraftingItems = false;
        for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
        {
            if (!string.IsNullOrWhiteSpace(profile.Inventory.CraftingSlots[i]))
            {
                hasCraftingItems = true;
                break;
            }
        }

        if (!hasCraftingItems)
        {
            if (!silentIfEmpty)
                SetStatus("Crafting slots are already empty.");
            return true;
        }

        inventoryActionInProgress = true;
        preserveInventoryButtonVisualsDuringSave = true;
        SetInteractable(false);
        SetStatus("Clearing crafting slots...");

        try
        {
            PlayerInventoryData workingInventory = profile.Inventory.Clone();
            int shipCapacity = GetActiveShipInventoryCapacity();

            for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
            {
                string itemId = workingInventory.RemoveFromCrafting(i);
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (workingInventory.TryAddToPlayer(itemId))
                    continue;

                if (workingInventory.TryAddToShip(itemId, shipCapacity, GetActiveProfileShipSkinIndex()))
                    continue;

                workingInventory.SetCrafting(i, itemId);
                SetStatus("No inventory space to clear crafting slots.");
                return false;
            }

            suppressNextProfileChangedRefresh = true;
            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            if (showSuccessStatus)
                SetStatus("Crafting slots cleared.");
            else if (silentIfEmpty)
                SetStatus(string.Empty);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Clear crafting slots failed: " + ex);
            SetStatus("Could not clear crafting slots.");
            return false;
        }
        finally
        {
            suppressNextProfileChangedRefresh = false;
            inventoryActionInProgress = false;
            bool finalInteractable = !NetworkManager.SessionRequested || !SessionBrowserPanelUI.IsVisible;
            SetInteractable(finalInteractable);
            preserveInventoryButtonVisualsDuringSave = false;
            if (!finalInteractable)
                SetInteractable(false);
            RefreshProfileSummaryAndInventory();
        }
    }

    int CountFreePlayerInventorySlots(PlayerInventoryData inventory)
    {
        if (inventory == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        for (int i = 0; i < inventory.PlayerSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(inventory.PlayerSlots[i]))
                count++;
        }

        return count;
    }

    int CountOccupiedPlayerInventorySlots(PlayerInventoryData inventory)
    {
        if (inventory == null || inventory.PlayerSlots == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        for (int i = 0; i < inventory.PlayerSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.PlayerSlots[i]))
                count++;
        }

        return count;
    }

    int CountOccupiedShipInventorySlots(PlayerInventoryData inventory, int shipCapacity)
    {
        if (inventory == null || inventory.ShipSlots == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        int capacity = Mathf.Clamp(shipCapacity, 0, inventory.ShipSlots.Length);
        for (int i = 0; i < capacity; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.ShipSlots[i]))
                count++;
        }

        return count;
    }
}
