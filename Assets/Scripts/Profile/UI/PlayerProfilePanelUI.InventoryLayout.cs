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
    void LayoutInventoryScreen()
    {
        if (currentScreen != ProfileScreen.Inventory)
            return;

        if (storageViewRootObject != null && storageViewRootObject.activeSelf)
            LayoutCraftingStoragePanel();

        if (shipPreviewTitleText != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform rect = shipPreviewTitleText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(548f, -82f);
            rect.sizeDelta = new Vector2(620f, 44f);
            shipPreviewTitleText.fontSize = 34f;
        }

        if (shipPreviewRootRect != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            shipPreviewRootRect.anchorMin = new Vector2(0.5f, 1f);
            shipPreviewRootRect.anchorMax = new Vector2(0.5f, 1f);
            shipPreviewRootRect.pivot = new Vector2(0.5f, 1f);
            shipPreviewRootRect.anchoredPosition = new Vector2(548f, -128f);
            shipPreviewRootRect.sizeDelta = new Vector2(1340f, 880f);
        }

        if (shipPreviewImage != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform imageRect = shipPreviewImage.rectTransform;
            imageRect.anchoredPosition = new Vector2(0f, 10f);
            imageRect.sizeDelta = new Vector2(980f, 756f);
        }

        Transform hitbox = shipPreviewRootRect != null ? shipPreviewRootRect.transform.Find("ShipPreviewHitbox") : null;
        if (hitbox != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform hitboxRect = hitbox.GetComponent<RectTransform>();
            if (hitboxRect != null)
                hitboxRect.sizeDelta = new Vector2(1140f, 780f);
        }

        if (shipStatsPanelObject != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform rect = shipStatsPanelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(548f, -128f);
            rect.sizeDelta = new Vector2(1340f, 880f);
        }

        LayoutEquipmentSlotsColumn(-520f, -386f, -28f, 116f, 104f);
        LayoutShipStatsVertical(486f, -52f, new Vector2(278f, 72f), 12f);
        ApplyEquipmentSlotPreviewSizing();
    }

    void LayoutStoragePanel(float centerX, float playerScrollWidth = 830f)
    {
        ConfigureStorageBackdrop(false, 0f, 0f, 0f, 0f);

        const float slotSize = 96f;
        const float slotSpacing = 8f;
        float shipWidth = (slotSize * 5f) + (slotSpacing * 4f);
        float shipLeftEdge = centerX - (shipWidth * 0.5f);
        LayoutShipInventoryHeader(shipLeftEdge, shipWidth, -180f + InventoryUtilityButtonLift);

        if (shipInventoryButtons != null)
        {
            float firstSlotX = centerX - (((slotSize * 5f) + (slotSpacing * 4f)) * 0.5f) + (slotSize * 0.5f);
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] == null)
                    continue;

                int row = i / 5;
                int col = i % 5;
                Vector2 position = new Vector2(firstSlotX + col * (slotSize + slotSpacing), -212f - row * (slotSize + slotSpacing));
                SetAnchoredRect(shipInventoryButtons[i].GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(slotSize, slotSize));
            }
        }

        float playerInventoryFilterButtonX = centerX - 320f;
        float playerInventoryExtendButtonX = centerX + 322f + InventoryExtendButtonOffsetX;
        float playerInventorySortButtonX = playerInventoryExtendButtonX - ((PlayerInventoryExtendButtonSize.x + PlayerInventorySortButtonSize.x) * 0.5f) - InventoryUtilityButtonGap;
        LayoutPlayerInventoryLabel(playerInventoryFilterButtonX, playerInventorySortButtonX);
        if (playerInventoryFilterButton != null)
            SetAnchoredRect(playerInventoryFilterButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryFilterButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryFilterButtonSize);
        if (playerInventorySortButton != null)
            SetAnchoredRect(playerInventorySortButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventorySortButtonX, -544f + InventoryUtilityButtonLift), PlayerInventorySortButtonSize);
        if (playerInventoryExtendButton != null)
            SetAnchoredRect(playerInventoryExtendButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryExtendButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryExtendButtonSize);

        if (playerInventoryScrollRect != null)
            SetAnchoredRect(playerInventoryScrollRect.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX - 10f, -578f), new Vector2(playerScrollWidth, 362f));
        if (playerInventoryScrollbarObject != null)
            SetAnchoredRect(playerInventoryScrollbarObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX + (playerScrollWidth * 0.5f) + 28f, -578f), new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), 362f));
    }

    void LayoutPlayerInventoryLabel(float filterButtonCenterX, float sortButtonCenterX)
    {
        if (playerInventoryLabelText == null && playerInventoryCountText == null)
            return;

        float leftEdge = filterButtonCenterX + PlayerInventoryFilterButtonSize.x * 0.5f + InventoryUtilityLabelGap;
        float rightEdge = sortButtonCenterX - PlayerInventorySortButtonSize.x * 0.5f - InventoryUtilityLabelGap;
        float totalWidth = Mathf.Max(96f, rightEdge - leftEdge);
        float countWidth = Mathf.Min(102f, Mathf.Max(84f, totalWidth * 0.42f));
        float labelWidth = Mathf.Max(44f, totalWidth - countWidth - 6f);
        float y = -544f + InventoryUtilityButtonLift;

        if (playerInventoryLabelText != null)
        {
            SetAnchoredRect(
                playerInventoryLabelText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(leftEdge + labelWidth * 0.5f, y),
                new Vector2(labelWidth, PlayerInventoryFilterButtonSize.y));
            ConfigurePlayerInventoryLabelText();
        }

        if (playerInventoryCountText != null)
        {
            SetAnchoredRect(
                playerInventoryCountText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(rightEdge - countWidth * 0.5f, y),
                new Vector2(countWidth, PlayerInventoryFilterButtonSize.y));
            ConfigurePlayerInventoryCountText();
        }
    }

    void LayoutShipInventoryHeader(float shipLeftEdge, float shipWidth, float y)
    {
        float safeShipWidth = Mathf.Max(ShipInventoryHeaderButtonSize.x + 220f, shipWidth);
        float labelWidth = Mathf.Max(260f, safeShipWidth - ShipInventoryHeaderButtonSize.x - ShipInventoryHeaderGap);
        float labelCenterX = shipLeftEdge + (labelWidth * 0.5f);
        float buttonCenterX = shipLeftEdge + safeShipWidth - (ShipInventoryHeaderButtonSize.x * 0.5f);

        if (shipInventoryLabelText != null)
        {
            SetAnchoredRect(
                shipInventoryLabelText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(labelCenterX, y),
                new Vector2(labelWidth, ShipInventoryHeaderButtonSize.y));
            ConfigureShipInventoryLabelText();
        }

        if (shipInventoryUnloadButton != null)
        {
            SetAnchoredRect(
                shipInventoryUnloadButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(buttonCenterX, y),
                ShipInventoryHeaderButtonSize);
            StyleReadableBackLikeButton(shipInventoryUnloadButton, ShipInventoryHeaderButtonFontSize);
        }
    }

    void LayoutCraftingStoragePanel()
    {
        const float leftEdge = -1018f;
        const float playerScrollWidth = 770f;
        const float shipSlotSize = 96f;
        const float shipSlotSpacing = 8f;
        float shipWidth = (shipSlotSize * 5f) + (shipSlotSpacing * 4f);
        float playerCenterX = leftEdge + (playerScrollWidth * 0.5f);
        float playerRightEdge = leftEdge + playerScrollWidth;

        ConfigureStorageBackdrop(false, 0f, 0f, 0f, 0f);

        LayoutShipInventoryHeader(leftEdge, shipWidth, -180f + InventoryUtilityButtonLift);

        if (shipInventoryButtons != null)
        {
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] == null)
                    continue;

                int row = i / 5;
                int col = i % 5;
                Vector2 position = new Vector2(leftEdge + (shipSlotSize * 0.5f) + col * (shipSlotSize + shipSlotSpacing), -212f - row * (shipSlotSize + shipSlotSpacing));
                SetAnchoredRect(shipInventoryButtons[i].GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(shipSlotSize, shipSlotSize));
            }
        }

        float playerInventoryFilterButtonX = leftEdge + 83f;
        float playerInventoryExtendButtonX = playerRightEdge - 66f + InventoryExtendButtonOffsetX;
        float playerInventorySortButtonX = playerInventoryExtendButtonX - ((PlayerInventoryExtendButtonSize.x + PlayerInventorySortButtonSize.x) * 0.5f) - InventoryUtilityButtonGap;
        LayoutPlayerInventoryLabel(playerInventoryFilterButtonX, playerInventorySortButtonX);

        if (playerInventoryFilterButton != null)
            SetAnchoredRect(playerInventoryFilterButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryFilterButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryFilterButtonSize);
        if (playerInventorySortButton != null)
            SetAnchoredRect(playerInventorySortButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventorySortButtonX, -544f + InventoryUtilityButtonLift), PlayerInventorySortButtonSize);

        if (playerInventoryExtendButton != null)
            SetAnchoredRect(playerInventoryExtendButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryExtendButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryExtendButtonSize);

        if (playerInventoryScrollRect != null)
            SetAnchoredRect(playerInventoryScrollRect.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerCenterX, -578f), new Vector2(playerScrollWidth, 362f));

        if (playerInventoryScrollbarObject != null)
            SetAnchoredRect(playerInventoryScrollbarObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerRightEdge + 28f, -578f), new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), 362f));
    }

    void ConfigureStorageBackdrop(bool visible, float centerX, float topY, float width, float height)
    {
        if (storageViewRootObject == null)
            return;

        Transform existing = storageViewRootObject.transform.Find("StorageBackdrop");
        Image backdrop = existing != null
            ? existing.GetComponent<Image>()
            : null;

        if (backdrop == null)
        {
            GameObject backdropObject = new GameObject("StorageBackdrop", typeof(RectTransform), typeof(Image));
            backdropObject.transform.SetParent(storageViewRootObject.transform, false);
            backdropObject.transform.SetAsFirstSibling();
            backdrop = backdropObject.GetComponent<Image>();
            backdrop.raycastTarget = false;
        }

        backdrop.gameObject.SetActive(visible);
        if (!visible)
            return;

        RectTransform rect = backdrop.rectTransform;
        SetAnchoredRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX, topY), new Vector2(width, height));
        backdrop.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);
        backdrop.transform.SetAsFirstSibling();
    }

    void LayoutCraftingScreen()
    {
        if (currentScreen != ProfileScreen.Crafting)
            return;

        if (storageViewRootObject != null && storageViewRootObject.activeSelf)
            LayoutCraftingStoragePanel();

        LayoutAmbientShipBackdrop();
        if (shipWorkspaceRootObject != null)
            shipWorkspaceRootObject.transform.SetAsFirstSibling();
        if (craftingViewRootObject != null)
            craftingViewRootObject.transform.SetAsLastSibling();
        if (storageViewRootObject != null)
            storageViewRootObject.transform.SetAsLastSibling();
        if (craftingRecipeBrowserObject != null)
            craftingRecipeBrowserObject.transform.SetAsLastSibling();
        if (craftingPanelObject != null)
            craftingPanelObject.transform.SetAsLastSibling();

        if (craftingPanelObject != null)
        {
            RectTransform rect = craftingPanelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -470f);
            rect.sizeDelta = new Vector2(360f, 492f);
        }
    }

    void ConfigureEmbeddedCraftingRecipeBrowser()
    {
        if (craftingRecipeBrowserObject == null)
            return;

        Image overlay = craftingRecipeBrowserObject.GetComponent<Image>();
        if (overlay != null)
        {
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.raycastTarget = false;
        }

        Transform panel = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel");
        if (panel != null)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(566f, -172f);
            panelRect.sizeDelta = new Vector2(812f, 698f);
        }

        TMP_Text title = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeBrowserTitle")?.GetComponent<TMP_Text>();
        if (title != null)
            title.gameObject.SetActive(false);

        Transform hint = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeBrowserHint");
        if (hint != null)
            hint.gameObject.SetActive(false);

        RectTransform viewportRect = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeViewport")?.GetComponent<RectTransform>();
        if (viewportRect != null)
        {
            viewportRect.anchoredPosition = new Vector2(-22f, -6f);
            viewportRect.sizeDelta = new Vector2(708f, 610f);
        }

        RectTransform scrollbarRect = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeScrollbar")?.GetComponent<RectTransform>();
        if (scrollbarRect != null)
        {
            scrollbarRect.anchoredPosition = new Vector2(386f, -6f);
            scrollbarRect.sizeDelta = new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), 610f);
        }

        if (craftingRecipeAvailabilityButton != null)
        {
            RectTransform rect = craftingRecipeAvailabilityButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(-286f, 42f);
                rect.sizeDelta = new Vector2(255f, 63f);
            }
        }

        if (craftingRecipeBlueprintsButton != null)
        {
            RectTransform rect = craftingRecipeBlueprintsButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(0f, 42f);
                rect.sizeDelta = new Vector2(285f, 68f);
            }

            ConfigureBlueprintsTabButtonText();
        }

        if (craftingRecipeCloseButton != null)
            craftingRecipeCloseButton.gameObject.SetActive(false);
    }
}
