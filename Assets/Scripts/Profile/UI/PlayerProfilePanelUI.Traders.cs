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
    void CreateTraderFuturePanel(Transform parent)
    {
        traderFuturePanelObject = new GameObject("TraderFuturePanel", typeof(RectTransform), typeof(Image));
        traderFuturePanelObject.transform.SetParent(parent, false);
        RectTransform rect = traderFuturePanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(632f, -262f);
        rect.sizeDelta = new Vector2(420f, 736f);

        Image image = traderFuturePanelObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        traderButtonsByKind.Clear();
        traderCardImagesByKind.Clear();
        traderOutlinesByKind.Clear();

        for (int i = 0; i < TraderDefinitions.Length; i++)
        {
            TraderDefinition definition = TraderDefinitions[i];
            float x = (i % 2 == 0) ? -104f : 104f;
            float y = -8f - ((i / 2) * 346f);
            CreateTraderPortraitButton(definition, new Vector2(x, y), new Vector2(196f, 330f));
        }

        RefreshTraderSelectionVisuals();
    }

    void CreateTraderPortraitButton(TraderDefinition definition, Vector2 anchoredPosition, Vector2 size)
    {
        if (definition == null || traderFuturePanelObject == null)
            return;

        TraderShopKind capturedKind = definition.Kind;
        Button button = CreateButton(
            traderFuturePanelObject.transform,
            "TraderButton_" + definition.Kind,
            string.Empty,
            anchoredPosition,
            size,
            () => OnTraderPortraitClicked(capturedKind));
        StyleButton(button, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.1f));

        Image cardImage = button.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = new Color(1f, 1f, 1f, 0f);
            cardImage.raycastTarget = true;
            traderCardImagesByKind[definition.Kind] = cardImage;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.34f, 0.48f, 0.62f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        traderOutlinesByKind[definition.Kind] = outline;

        GameObject portraitObject = new GameObject("TraderPortrait", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(button.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;

        Image portrait = portraitObject.GetComponent<Image>();
        Sprite portraitSprite = LoadTraderPortraitSprite(definition);
        portrait.sprite = portraitSprite;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.color = portraitSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);

        TMP_Text nameText = CreateText(button.transform, "TraderName", definition.DisplayName, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(size.x - 10f, 30f), 18f, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 11f;
        nameText.fontSizeMax = 18f;
        nameText.characterSpacing = 1.5f;
        nameText.raycastTarget = false;
        Shadow nameShadow = nameText.gameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
        nameShadow.effectDistance = new Vector2(2f, -2f);

        traderButtonsByKind[definition.Kind] = button;
    }

    Sprite LoadTraderPortraitSprite(TraderDefinition definition)
    {
        if (definition == null)
            return null;

        return LoadSpriteFromResourcesOrEditor(
            definition.ResourcePath,
            definition.EditorPreferredPath,
            definition.EditorFallbackPath);
    }

    TraderDefinition GetTraderDefinition(TraderShopKind kind)
    {
        for (int i = 0; i < TraderDefinitions.Length; i++)
        {
            TraderDefinition definition = TraderDefinitions[i];
            if (definition != null && definition.Kind == kind)
                return definition;
        }

        return null;
    }

    string GetTraderDisplayName(TraderShopKind kind)
    {
        TraderDefinition definition = GetTraderDefinition(kind);
        return definition != null ? definition.DisplayName : "TRADER";
    }

    bool TraderOpensShop(TraderShopKind kind)
    {
        TraderDefinition definition = GetTraderDefinition(kind);
        return definition != null && definition.OpensShop;
    }

    bool ShouldTraderSellDefinition(TraderShopKind kind, InventoryItemDefinition definition)
    {
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        switch (kind)
        {
            case TraderShopKind.IronJoe:
                return definition.Category == InventoryItemCategory.Weapon;
            case TraderShopKind.MissGadget:
                return definition.Category == InventoryItemCategory.Gadget ||
                    definition.Category == InventoryItemCategory.Support ||
                    definition.Category == InventoryItemCategory.Rescue;
            case TraderShopKind.DirtySam:
                return definition.Category == InventoryItemCategory.Shield ||
                    definition.Category == InventoryItemCategory.Engine;
            default:
                return false;
        }
    }

    void OnTraderPortraitClicked(TraderShopKind kind)
    {
        if (inventoryActionInProgress || dragInProgress)
            return;

        selectedTraderShop = kind;
        HideCraftingRecipeBrowser();
        HideItemPreview();
        RefreshTraderSelectionVisuals();

        if (!TraderOpensShop(selectedTraderShop))
        {
            RefreshShopBrowser();
            HideShopBrowser();
            SetStatus(GetTraderDisplayName(selectedTraderShop) + " coming later.");
            return;
        }

        SetStatus(string.Empty);
        RefreshShopBrowser();
        if (shopBrowserObject != null && currentScreen == ProfileScreen.Trader)
        {
            shopBrowserObject.SetActive(true);
            shopBrowserObject.transform.SetAsLastSibling();
        }

        if (traderFuturePanelObject != null)
            traderFuturePanelObject.transform.SetAsLastSibling();
    }

    void RefreshTraderSelectionVisuals()
    {
        for (int i = 0; i < TraderDefinitions.Length; i++)
        {
            TraderDefinition definition = TraderDefinitions[i];
            if (definition == null)
                continue;

            bool selected = definition.Kind == selectedTraderShop;
            bool interactable = inventoryControlsInteractable && !inventoryActionInProgress;

            if (traderButtonsByKind.TryGetValue(definition.Kind, out Button button) && button != null)
                button.interactable = interactable;

            if (traderCardImagesByKind.TryGetValue(definition.Kind, out Image cardImage) && cardImage != null)
            {
                cardImage.color = selected
                    ? new Color(1f, 0.82f, 0.22f, 0.1f)
                    : new Color(1f, 1f, 1f, 0f);
            }

            if (traderOutlinesByKind.TryGetValue(definition.Kind, out Outline outline) && outline != null)
            {
                outline.effectColor = selected
                    ? new Color(0.94f, 0.78f, 0.32f, 0.96f)
                    : new Color(0.34f, 0.48f, 0.62f, 0.72f);
                outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
            }
        }
    }
}
