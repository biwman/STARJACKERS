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
    void OnSaveAndRunClicked()
    {
        if (nicknameInput == null)
            return;

        if (IsCraftingGridOccupied())
        {
            SetStatus("Empty crafting slots before starting.");
            return;
        }

        int shipInventoryItemCount = CountShipInventoryItems();
        if (shipInventoryItemCount > 0)
        {
            ShowShipInventoryStartConfirm(shipInventoryItemCount);
            return;
        }

        ContinueSaveAndRun();
    }

    void ContinueSaveAndRun()
    {
        if (nicknameInput == null)
            return;

        SetStatus("Preparing lobby...");
        SetInteractable(false);

        try
        {
            PlayerProfileService.Instance.SaveProfileLocally(nicknameInput.text, selectedSkin);
            SetStatus("Loading active rounds...");
            SessionBrowserPanelUI.ShowBrowser();
            NetworkManager.RequestSessionStart();
            _ = SaveProfileToCloudAfterPlayAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("Profile save failed: " + ex);
            SetStatus("Save failed");
            SetInteractable(true);
        }
    }

    int CountShipInventoryItems()
    {
        if (!PlayerProfileService.HasInstance || PlayerProfileService.Instance.CurrentProfile == null)
            return 0;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile.Inventory;
        if (inventory == null || inventory.ShipSlots == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        for (int i = 0; i < inventory.ShipSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.ShipSlots[i]))
                count++;
        }

        return count;
    }

    void ShowShipInventoryStartConfirm(int itemCount)
    {
        if (shipInventoryStartConfirmText != null)
        {
            string itemLabel = itemCount == 1 ? "item" : "items";
            shipInventoryStartConfirmText.text = "Your ship inventory contains " + itemCount + " " + itemLabel + ".\nAre you sure you want to continue?";
        }

        if (shipInventoryStartConfirmObject != null)
        {
            shipInventoryStartConfirmObject.SetActive(true);
            EnsureProfileModalLayering();
        }
    }

    void HideShipInventoryStartConfirm()
    {
        if (shipInventoryStartConfirmObject != null)
            shipInventoryStartConfirmObject.SetActive(false);
    }

    void OnShipInventoryStartConfirmYesClicked()
    {
        HideShipInventoryStartConfirm();
        ContinueSaveAndRun();
    }

    void OnShipInventoryStartConfirmNoClicked()
    {
        HideShipInventoryStartConfirm();
        SetStatus("Review ship inventory before launch.");
        SwitchToScreen(ProfileScreen.Inventory, false);
        RefreshView();
    }

    async Task SaveProfileToCloudAfterPlayAsync()
    {
        try
        {
            await PlayerProfileService.Instance.SaveCurrentProfileToCloudAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("PLAY clicked: background cloud sync failed: " + ex.Message);
            if (currentScreen == ProfileScreen.Home && !SessionBrowserPanelUI.IsVisible)
                SetStatus("Cloud save delayed. Local profile kept.");
        }
    }

    void RefreshView()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        if (profile.Inventory != null)
            profile.Inventory.Normalize();

        selectedSkin = Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        selectedPilotId = PilotCatalog.NormalizePilotId(profile.SelectedPilotId);
        if (!PilotCatalog.IsPilotUnlocked(profile, selectedPilotId))
            selectedPilotId = PilotCatalog.JakeId;

        if (nicknameInput != null && !nicknameInput.isFocused)
        {
            nicknameInput.text = profile.Nickname;
        }

        if (gamesPlayedText != null)
        {
            gamesPlayedText.text = "Games: " + profile.GamesPlayed;
        }

        if (totalXpText != null)
        {
            totalXpText.text = "Level: " + RoundXpBalance.GetLevelForTotalXp(profile.TotalXp) + "  XP: " + profile.TotalXp;
        }

        if (astronsText != null)
        {
            astronsText.text = "Astrons: " + profile.Astrons;
        }

        string accountLine = string.Empty;
        if (accountText != null || sharedTopBar != null)
        {
            string playerId = PlayerProfileService.Instance.PlayerId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                accountLine = PlayerProfileService.Instance.IsInitialized ? "Cloud linked" : "Connecting...";
            }
            else
            {
                string suffix = playerId.Length <= 8 ? playerId : playerId.Substring(playerId.Length - 8);
                accountLine = "ID: " + suffix.ToUpperInvariant();
            }

            if (accountText != null)
                accountText.text = accountLine;
        }

        if (sharedTopBar != null)
            sharedTopBar.SetProfile(profile, profile.Nickname, accountLine, nicknameInput == null || !nicknameInput.isFocused);

        UpdateShipTypeButtonVisuals();
        UpdateSkinButtonsForSelectedShip();
        UpdateSkinButtonVisuals();
        ApplySaveAndRunButtonStyle();
        RefreshShipPreview();
        RefreshPilotPortrait();
        RefreshInventoryView(profile.Inventory);
        if (currentScreen == ProfileScreen.Projects)
            RefreshProjectsView();
        else
            projectsDirty = true;
        if (currentScreen == ProfileScreen.ProjectDetails)
            RefreshProjectDetailsView();
        else
            projectDetailsDirty = true;
        if (craftingRecipeBrowserObject != null && craftingRecipeBrowserObject.activeSelf)
            RefreshCraftingRecipeBrowser();
        if (craftingBlueprintBrowserObject != null && craftingBlueprintBrowserObject.activeSelf)
            RefreshCraftingBlueprintBrowser();
        if (shopBrowserObject != null && shopBrowserObject.activeSelf)
            RefreshShopBrowser(false);
        ApplyProfileScreenLayoutAfterRefresh();
    }

    void RefreshProfileSummaryAndInventory()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        if (profile.Inventory != null)
            profile.Inventory.Normalize();

        selectedSkin = Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        selectedPilotId = PilotCatalog.NormalizePilotId(profile.SelectedPilotId);
        if (!PilotCatalog.IsPilotUnlocked(profile, selectedPilotId))
            selectedPilotId = PilotCatalog.JakeId;

        if (gamesPlayedText != null)
            gamesPlayedText.text = "Games: " + profile.GamesPlayed;

        if (totalXpText != null)
            totalXpText.text = "Level: " + RoundXpBalance.GetLevelForTotalXp(profile.TotalXp) + "  XP: " + profile.TotalXp;

        if (astronsText != null)
            astronsText.text = "Astrons: " + profile.Astrons;

        string accountLine = accountText != null ? accountText.text : string.Empty;
        if (sharedTopBar != null)
            sharedTopBar.SetProfile(profile, profile.Nickname, accountLine, nicknameInput == null || !nicknameInput.isFocused);

        RefreshInventoryView(profile.Inventory);
        RefreshEquipmentSlotPreview();
        if (craftingRecipeBrowserObject != null && craftingRecipeBrowserObject.activeSelf)
            RefreshCraftingRecipeBrowser();
        if (craftingBlueprintBrowserObject != null && craftingBlueprintBrowserObject.activeSelf)
            RefreshCraftingBlueprintBrowser();
        if (shopBrowserObject != null && shopBrowserObject.activeSelf)
            RefreshShopBrowser(false);

        ApplyProfileScreenLayoutAfterRefresh();
    }

    void ApplyProfileScreenLayoutAfterRefresh()
    {
        ApplyProfileScreenLayout();
        profileLayoutDirty = false;
    }

    ShipType GetSelectedShipType()
    {
        return ShipCatalog.GetShipTypeFromSkinIndex(selectedSkin);
    }

    bool IsShipTypeUnlockedForUi(ShipType shipType)
    {
        return PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsShipUnlocked(shipType);
    }

    int GetActiveProfileShipSkinIndex()
    {
        PlayerProfileData profile = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.CurrentProfile : null;
        int skinIndex = profile != null ? profile.ShipSkinIndex : selectedSkin;
        return Mathf.Clamp(skinIndex, 0, ShipCatalog.MaxShipSkinIndex);
    }

    int GetActiveShipInventoryCapacity()
    {
        PlayerProfileData profile = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.CurrentProfile : null;
        string[] equipmentSlots = profile != null && profile.Inventory != null ? profile.Inventory.EquipmentSlots : null;
        if (PlayerProfileService.HasInstance)
            return PlayerProfileService.Instance.GetShipInventoryCapacityForProfile(GetActiveProfileShipSkinIndex(), equipmentSlots);

        return PlayerProfileService.GetEffectiveShipInventoryCapacity(GetActiveProfileShipSkinIndex(), equipmentSlots);
    }

    bool IsEquipmentSlotEnabledForSelectedSkin(int slotIndex)
    {
        if (!PlayerProfileService.HasInstance)
            return ShipCatalog.IsEquipmentSlotEnabled(slotIndex, selectedSkin);

        return PlayerProfileService.Instance.IsEquipmentSlotEnabledForProfile(slotIndex, selectedSkin);
    }

    bool IsActiveShipSafePocketIndex(int slotIndex)
    {
        return PlayerProfileService.IsSafePocketIndex(GetActiveProfileShipSkinIndex(), slotIndex);
    }

    bool IsActiveShipAstronautCargoIndex(int slotIndex)
    {
        return PlayerProfileService.IsAstronautCargoIndex(GetActiveProfileShipSkinIndex(), GetActiveShipInventoryCapacity(), slotIndex);
    }
}
