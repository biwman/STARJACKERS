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
    void CreateItemPreview(Transform parent)
    {
        itemPreviewPanelObject = new GameObject("ItemPreviewPanel", typeof(RectTransform), typeof(Image));
        itemPreviewPanelObject.transform.SetParent(parent, false);

        RectTransform rect = itemPreviewPanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 172f);
        rect.sizeDelta = new Vector2(304f, 326f);

        Image background = itemPreviewPanelObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = false;

        GameObject backgroundCardObject = new GameObject("ItemPreviewBackground", typeof(RectTransform), typeof(Image));
        backgroundCardObject.transform.SetParent(itemPreviewPanelObject.transform, false);
        RectTransform backgroundCardRect = backgroundCardObject.GetComponent<RectTransform>();
        backgroundCardRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundCardRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundCardRect.pivot = new Vector2(0.5f, 1f);
        backgroundCardRect.anchoredPosition = new Vector2(0f, -4f);
        backgroundCardRect.sizeDelta = new Vector2(250f, 250f);
        itemPreviewBackgroundImage = backgroundCardObject.GetComponent<Image>();
        itemPreviewBackgroundImage.color = new Color(0.08f, 0.12f, 0.16f, 0.92f);
        itemPreviewBackgroundImage.raycastTarget = false;

        GameObject iconObject = new GameObject("ItemPreviewIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(itemPreviewPanelObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -20f);
        iconRect.sizeDelta = new Vector2(128f, 128f);
        itemPreviewIcon = iconObject.GetComponent<Image>();
        itemPreviewIcon.preserveAspect = true;

        itemPreviewNameText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewNameText", "SELECT ITEM", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(228f, 30f), 20f, TextAlignmentOptions.Center);
        itemPreviewTypeText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewTypeText", "Misc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(228f, 26f), 20f, TextAlignmentOptions.Center);
        itemPreviewTypeText.fontStyle = FontStyles.Bold;
        itemPreviewTypeText.color = new Color(0.72f, 0.86f, 1f, 1f);
        itemPreviewPriceText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewPriceText", "0 Astrons", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(228f, 26f), 20f, TextAlignmentOptions.Center);
        itemPreviewPriceText.fontStyle = FontStyles.Normal;
        itemPreviewInfoButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewInfoButton", "INFO", new Vector2(0f, 54f), ItemPreviewInfoButtonSize, OnItemPreviewInfoClicked);
        ConfigureNoBlinkInventoryActionButton(itemPreviewInfoButton);
        StyleReadableBackLikeButton(itemPreviewInfoButton, ShipInventoryHeaderButtonFontSize);
        itemPreviewSellButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSellButton", "SELL", new Vector2(-76f, -270f), new Vector2(136f, 50f), OnItemPreviewSellClicked);
        itemPreviewSalvageButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSalvageButton", "SALVAGE", new Vector2(76f, -270f), new Vector2(136f, 50f), OnItemPreviewSalvageClicked);
        ConfigureNoBlinkInventoryActionButton(itemPreviewSellButton);
        StyleCompactBackLikeButton(itemPreviewSellButton);
        ConfigureNoBlinkInventoryActionButton(itemPreviewSalvageButton);
        StyleCompactBackLikeButton(itemPreviewSalvageButton);
        itemPreviewPanelObject.SetActive(false);
    }

    void CreateItemInfoOverlay(Transform parent)
    {
        itemInfoOverlayObject = new GameObject("ItemInfoOverlay", typeof(RectTransform), typeof(Image));
        itemInfoOverlayObject.transform.SetParent(parent, false);
        RectTransform overlayRect = itemInfoOverlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = itemInfoOverlayObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.015f, 0.026f, 0.88f);
        overlayImage.raycastTarget = true;

        GameObject cardObject = new GameObject("ItemInfoCard", typeof(RectTransform), typeof(Image));
        cardObject.transform.SetParent(itemInfoOverlayObject.transform, false);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(1160f, 820f);

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.06f, 0.085f, 0.12f, 0.99f);

        itemInfoTitleText = CreateText(cardObject.transform, "ItemInfoTitle", "ITEM", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(1030f, 52f), 38f, TextAlignmentOptions.Center);
        itemInfoTitleText.enableAutoSizing = true;
        itemInfoTitleText.fontSizeMin = 22f;
        itemInfoTitleText.fontSizeMax = 38f;

        itemInfoTypeText = CreateText(cardObject.transform, "ItemInfoType", "Type", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(1030f, 30f), 22f, TextAlignmentOptions.Center);
        itemInfoTypeText.fontStyle = FontStyles.Normal;
        itemInfoTypeText.color = new Color(0.74f, 0.88f, 1f, 1f);

        GameObject imageCardObject = new GameObject("ItemInfoImageCard", typeof(RectTransform), typeof(Image));
        imageCardObject.transform.SetParent(cardObject.transform, false);
        RectTransform imageCardRect = imageCardObject.GetComponent<RectTransform>();
        imageCardRect.anchorMin = new Vector2(0.5f, 1f);
        imageCardRect.anchorMax = new Vector2(0.5f, 1f);
        imageCardRect.pivot = new Vector2(0.5f, 1f);
        imageCardRect.anchoredPosition = new Vector2(-310f, -152f);
        imageCardRect.sizeDelta = new Vector2(430f, 430f);

        Image imageCard = imageCardObject.GetComponent<Image>();
        imageCard.color = new Color(0.09f, 0.13f, 0.18f, 0.96f);

        GameObject iconObject = new GameObject("ItemInfoIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(imageCardObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(342f, 342f);
        itemInfoIcon = iconObject.GetComponent<Image>();
        itemInfoIcon.preserveAspect = true;
        itemInfoIcon.raycastTarget = false;

        itemInfoPriceText = CreateText(cardObject.transform, "ItemInfoPrice", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-310f, -608f), new Vector2(430f, 78f), 24f, TextAlignmentOptions.Center);
        itemInfoPriceText.fontStyle = FontStyles.Normal;
        itemInfoPriceText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoSalvageText = CreateText(cardObject.transform, "ItemInfoSalvage", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, -214f), new Vector2(560f, 130f), 22f, TextAlignmentOptions.TopLeft);
        itemInfoSalvageText.fontStyle = FontStyles.Normal;
        itemInfoSalvageText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoRecipeText = CreateText(cardObject.transform, "ItemInfoRecipe", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, -382f), new Vector2(560f, 166f), 22f, TextAlignmentOptions.TopLeft);
        itemInfoRecipeText.fontStyle = FontStyles.Normal;
        itemInfoRecipeText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoDescriptionText = CreateText(cardObject.transform, "ItemInfoDescription", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, -574f), new Vector2(560f, 180f), 23f, TextAlignmentOptions.TopLeft);
        itemInfoDescriptionText.fontStyle = FontStyles.Normal;
        itemInfoDescriptionText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoCloseButton = CreateButton(cardObject.transform, "ItemInfoCloseButton", "CLOSE", new Vector2(0f, -738f), new Vector2(250f, 62f), HideItemInfoOverlay);
        StyleButton(itemInfoCloseButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.24f, 0.34f, 0.46f, 1f));

        itemInfoOverlayObject.SetActive(false);
    }

    void CreatePlayerInventoryExtendConfirm(Transform parent)
    {
        playerInventoryExtendConfirmObject = new GameObject("PlayerInventoryExtendConfirm", typeof(RectTransform), typeof(Image));
        playerInventoryExtendConfirmObject.transform.SetParent(parent, false);
        RectTransform overlayRect = playerInventoryExtendConfirmObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = playerInventoryExtendConfirmObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.02f, 0.035f, 0.72f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("PlayerInventoryExtendConfirmPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(playerInventoryExtendConfirmObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1120f, 480f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.07f, 0.1f, 0.14f, 0.98f);

        CreateText(panel.transform, "PlayerInventoryExtendTitle", "EXTEND INVENTORY", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 68f), 48f, TextAlignmentOptions.Center);
        playerInventoryExtendConfirmText = CreateText(panel.transform, "PlayerInventoryExtendText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(1000f, 140f), 38f, TextAlignmentOptions.Center);
        playerInventoryExtendConfirmText.fontStyle = FontStyles.Normal;
        playerInventoryExtendConfirmText.textWrappingMode = TextWrappingModes.Normal;

        playerInventoryExtendConfirmButton = CreateButton(panel.transform, "PlayerInventoryExtendConfirmButton", "EXTEND", new Vector2(-232f, -332f), new Vector2(360f, 104f), OnPlayerInventoryExtendConfirmClicked);
        StyleReadableBackLikeButton(playerInventoryExtendConfirmButton, 36f);
        StyleButton(playerInventoryExtendConfirmButton, new Color(0.1f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        playerInventoryExtendCancelButton = CreateButton(panel.transform, "PlayerInventoryExtendCancelButton", "CANCEL", new Vector2(232f, -332f), new Vector2(360f, 104f), HidePlayerInventoryExtendConfirm);
        StyleReadableBackLikeButton(playerInventoryExtendCancelButton, 36f);
        StyleButton(playerInventoryExtendCancelButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        playerInventoryExtendConfirmObject.SetActive(false);
    }

    void CreateShipInventoryStartConfirm(Transform parent)
    {
        const float dialogScale = 1.3f;

        shipInventoryStartConfirmObject = new GameObject("ShipInventoryStartConfirm", typeof(RectTransform), typeof(Image));
        shipInventoryStartConfirmObject.transform.SetParent(parent, false);
        RectTransform overlayRect = shipInventoryStartConfirmObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = shipInventoryStartConfirmObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.02f, 0.035f, 0.76f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("ShipInventoryStartConfirmPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(shipInventoryStartConfirmObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f * dialogScale, 330f * dialogScale);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.07f, 0.1f, 0.14f, 0.99f);

        CreateText(panel.transform, "ShipInventoryStartConfirmTitle", "SHIP INVENTORY", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f * dialogScale), new Vector2(660f * dialogScale, 44f * dialogScale), 32f * dialogScale, TextAlignmentOptions.Center);
        shipInventoryStartConfirmText = CreateText(panel.transform, "ShipInventoryStartConfirmText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f * dialogScale), new Vector2(660f * dialogScale, 118f * dialogScale), 28f * dialogScale, TextAlignmentOptions.Center);
        shipInventoryStartConfirmText.fontStyle = FontStyles.Normal;
        shipInventoryStartConfirmText.textWrappingMode = TextWrappingModes.Normal;

        shipInventoryStartConfirmNoButton = CreateButton(panel.transform, "ShipInventoryStartConfirmNoButton", "NO", new Vector2(-160f * dialogScale, -248f * dialogScale), new Vector2(260f * dialogScale, 72f * dialogScale), OnShipInventoryStartConfirmNoClicked);
        StyleButton(shipInventoryStartConfirmNoButton, new Color(0.18f, 0.25f, 0.34f, 1f), new Color(0.26f, 0.36f, 0.48f, 1f));
        SetButtonTextSize(shipInventoryStartConfirmNoButton, 28f * dialogScale);
        shipInventoryStartConfirmYesButton = CreateButton(panel.transform, "ShipInventoryStartConfirmYesButton", "YES", new Vector2(160f * dialogScale, -248f * dialogScale), new Vector2(260f * dialogScale, 72f * dialogScale), OnShipInventoryStartConfirmYesClicked);
        StyleButton(shipInventoryStartConfirmYesButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        SetButtonTextSize(shipInventoryStartConfirmYesButton, 28f * dialogScale);

        shipInventoryStartConfirmObject.SetActive(false);
    }
}
