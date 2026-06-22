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
    bool RefreshVisibility()
    {
        if (panelObject == null)
            return false;

        bool show = !PhotonNetwork.InRoom;
        bool changed = !profilePanelVisibilityInitialized || profilePanelVisible != show;
        profilePanelVisibilityInitialized = true;
        profilePanelVisible = show;

        if (!show)
        {
            if (panelObject.activeSelf)
                panelObject.SetActive(false);

            if (changed)
            {
                SetGameplayHudVisible(true);
                if (splashScreenObject != null)
                    splashScreenObject.SetActive(false);
                HideShipImageModal();
                HideCraftingRecipeBrowser();
                HideShopBrowser();
                HideShipInventoryStartConfirm();
                HideItemInfoOverlay();
            }

            return false;
        }

        if (!panelObject.activeSelf)
            panelObject.SetActive(true);

        if (changed)
        {
            SetGameplayHudVisible(false);
            MarkAllProfileUiDirty();
        }

        bool splashShowing = IsSplashShowing();
        if (splashScreenObject != null)
        {
            splashScreenObject.SetActive(splashShowing);
            if (splashShowing)
                splashScreenObject.transform.SetAsLastSibling();
        }

        bool browserVisible = SessionBrowserPanelUI.IsVisible;
        ApplyInteractableIfChanged(!NetworkManager.SessionRequested || !browserVisible);
        if (statusText != null &&
            (statusText.text == "Connecting..." || statusText.text == "Loading active rounds...") &&
            !NetworkManager.SessionRequested)
        {
            statusText.text = string.Empty;
        }

        return true;
    }

    bool IsCraftingRecipeUnlocked(PlayerProfileCraftingRecipe recipe)
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        return PlayerProfileCraftingCatalog.IsRecipeUnlocked(
            recipe,
            profile != null ? profile.UnlockedBlueprintIds : null);
    }

    void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
            EnsureStatusTextLayering();
        }
    }

    int GetPlayerInventorySlotCount()
    {
        if (!PlayerProfileService.HasInstance || PlayerProfileService.Instance.CurrentProfile == null)
            return PlayerInventoryData.DefaultPlayerSlotCount;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile.Inventory;
        if (inventory == null || inventory.PlayerSlots == null)
            return PlayerInventoryData.DefaultPlayerSlotCount;

        return Mathf.Max(PlayerInventoryData.DefaultPlayerSlotCount, inventory.PlayerSlots.Length);
    }
}
