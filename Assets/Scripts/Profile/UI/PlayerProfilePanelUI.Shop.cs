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
    void CreateShopBrowser(Transform parent)
    {
        shopBrowserObject = new GameObject("ShopBrowser", typeof(RectTransform), typeof(Image));
        shopBrowserObject.transform.SetParent(parent, false);

        RectTransform overlayRect = shopBrowserObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = shopBrowserObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

        GameObject panel = new GameObject("ShopBrowserPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(shopBrowserObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(1040f, 800f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

        TMP_Text title = CreateText(panel.transform, "ShopBrowserTitle", "SHOP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 34f), 30f, TextAlignmentOptions.Center);
        title.characterSpacing = 3f;

        shopAstronsText = CreateText(panel.transform, "ShopAstronsText", "Astrons: 0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(440f, 28f), 20f, TextAlignmentOptions.Center);
        shopAstronsText.fontStyle = FontStyles.Normal;
        shopAstronsText.color = new Color(0.94f, 0.84f, 0.44f, 1f);

        shopSortButton = CreateButton(panel.transform, "ShopSortButton", "SORT: A-Z", new Vector2(352f, -68f), new Vector2(246f, 52f), OnShopSortClicked);
        StyleButton(shopSortButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));
        TMP_Text sortText = shopSortButton.GetComponentInChildren<TMP_Text>(true);
        if (sortText != null)
        {
            sortText.fontSize = 18f;
            sortText.enableAutoSizing = true;
            sortText.fontSizeMin = 12f;
            sortText.fontSizeMax = 18f;
            sortText.margin = new Vector4(10f, 4f, 10f, 4f);
        }

        shopCloseButton = CreateButton(panel.transform, "ShopCloseButton", "CLOSE", new Vector2(0f, -722f), new Vector2(220f, 58f), HideShopBrowser);
        StyleButton(shopCloseButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        GameObject viewportObject = new GameObject("ShopViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(-18f, -22f);
        viewportRect.sizeDelta = new Vector2(920f, 578f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.11f, 0.15f, 0.2f, 0.82f);

        GameObject contentObject = new GameObject("ShopContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        shopContentRect = contentObject.GetComponent<RectTransform>();
        shopContentRect.anchorMin = new Vector2(0f, 1f);
        shopContentRect.anchorMax = new Vector2(1f, 1f);
        shopContentRect.pivot = new Vector2(0.5f, 1f);
        shopContentRect.anchoredPosition = Vector2.zero;
        shopContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject scrollbarObject = new GameObject("ShopScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(panel.transform, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarRect.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(472f, -22f);
        scrollbarRect.sizeDelta = new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), 578f);

        Scrollbar scrollbar = RuntimeScrollbarStyler.ApplyVertical(scrollbarObject, RuntimeScrollbarStyler.Size.Small, RuntimeScrollbarStyler.Tone.Gold);

        shopScrollRect = viewportObject.GetComponent<ScrollRect>();
        shopScrollRect.horizontal = false;
        shopScrollRect.vertical = true;
        shopScrollRect.movementType = ScrollRect.MovementType.Clamped;
        shopScrollRect.viewport = viewportRect;
        shopScrollRect.content = shopContentRect;
        shopScrollRect.verticalScrollbar = scrollbar;
        shopScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        shopScrollRect.scrollSensitivity = 30f;

        shopBrowserObject.SetActive(false);
    }

    void OnShopButtonClicked()
    {
        if (inventoryActionInProgress || dragInProgress || shopBrowserObject == null)
            return;

        HideCraftingRecipeBrowser();
        HideItemPreview();
        selectedTraderShop = TraderShopKind.IronJoe;
        RefreshTraderSelectionVisuals();
        RefreshShopBrowser();
        shopBrowserObject.SetActive(true);
        shopBrowserObject.transform.SetAsLastSibling();
    }

    void HideShopBrowser()
    {
        if (shopBrowserObject != null)
            shopBrowserObject.SetActive(false);
    }

    void OnShopSortClicked()
    {
        if (inventoryActionInProgress || dragInProgress || !TraderOpensShop(selectedTraderShop))
            return;

        shopSortModesByTrader[selectedTraderShop] = GetNextShopSortMode(GetShopSortMode(selectedTraderShop));
        HideItemPreview();
        RefreshShopBrowser();
    }

    ShopSortMode GetShopSortMode(TraderShopKind traderKind)
    {
        if (shopSortModesByTrader.TryGetValue(traderKind, out ShopSortMode mode))
            return mode;

        return ShopSortMode.Alphabetical;
    }

    ShopSortMode GetNextShopSortMode(ShopSortMode mode)
    {
        switch (mode)
        {
            case ShopSortMode.Alphabetical:
                return ShopSortMode.Price;
            case ShopSortMode.Price:
                return ShopSortMode.Type;
            case ShopSortMode.Type:
                return ShopSortMode.Rarity;
            default:
                return ShopSortMode.Alphabetical;
        }
    }

    string GetShopSortLabel(ShopSortMode mode)
    {
        switch (mode)
        {
            case ShopSortMode.Price:
                return "PRICE";
            case ShopSortMode.Type:
                return "TYPE";
            case ShopSortMode.Rarity:
                return "RARITY";
            default:
                return "A-Z";
        }
    }

    void RefreshShopSortButton()
    {
        if (shopSortButton == null)
            return;

        bool show = TraderOpensShop(selectedTraderShop);
        shopSortButton.gameObject.SetActive(show);
        shopSortButton.interactable = show && inventoryControlsInteractable && !inventoryActionInProgress && !dragInProgress;

        TMP_Text text = shopSortButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = "SORT: " + GetShopSortLabel(GetShopSortMode(selectedTraderShop));
    }

    void RefreshShopBrowser(bool resetScrollPosition = true)
    {
        using (ShopRefreshMarker.Auto())
        {
            if (shopBrowserObject == null || shopContentRect == null)
                return;

            float previousScrollPosition = shopScrollRect != null
                ? shopScrollRect.verticalNormalizedPosition
                : 1f;

            HideActiveShopRows();

            UpdateShopBrowserTitle();
            RefreshShopSortButton();
            if (!TraderOpensShop(selectedTraderShop))
            {
                shopBrowserObject.SetActive(false);
                return;
            }

            if (selectedTraderShop == TraderShopKind.MissEnigma)
            {
                RefreshMissEnigmaShopBrowser(resetScrollPosition, previousScrollPosition);
                return;
            }

            PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
            int astrons = profile != null ? profile.Astrons : 0;
            if (shopAstronsText != null)
                shopAstronsText.gameObject.SetActive(false);

            IReadOnlyList<InventoryItemDefinition> definitions = InventoryItemCatalog.GetAllDefinitions();
            List<ShopOfferViewModel> shopOffers = new List<ShopOfferViewModel>();

            for (int i = 0; i < definitions.Count; i++)
            {
                InventoryItemDefinition definition = definitions[i];
                if (!ShouldTraderSellDefinition(selectedTraderShop, definition))
                    continue;

                int price = PlayerProfileService.Instance.GetShopBuyPriceAstrons(definition.Id);
                if (price <= 0)
                    continue;

                shopOffers.Add(new ShopOfferViewModel
                {
                    Definition = definition,
                    Price = price,
                    CanAfford = astrons >= price
                });
            }

            SortShopOffers(shopOffers);

            for (int i = 0; i < shopOffers.Count; i += 2)
            {
                ShopOfferViewModel leftOffer = shopOffers[i];
                ShopOfferViewModel rightOffer = i + 1 < shopOffers.Count ? shopOffers[i + 1] : null;
                InventoryItemDefinition leftDefinition = leftOffer.Definition;
                int leftPrice = leftOffer.Price;
                bool leftCanAfford = leftOffer.CanAfford;
                InventoryItemDefinition rightDefinition = rightOffer != null ? rightOffer.Definition : null;
                int rightPrice = rightOffer != null ? rightOffer.Price : 0;
                bool rightCanAfford = rightOffer != null && rightOffer.CanAfford;

                GameObject row = CreateShopRow(shopRowObjects.Count, leftDefinition, leftPrice, leftCanAfford, rightDefinition, rightPrice, rightCanAfford);
                if (row != null)
                    shopRowObjects.Add(row);
            }

            ApplyShopScrollPosition(resetScrollPosition ? 1f : previousScrollPosition);
        }
    }

    void ApplyShopScrollPosition(float normalizedPosition)
    {
        Canvas.ForceUpdateCanvases();
        if (shopScrollRect == null)
            return;

        shopScrollRect.StopMovement();
        shopScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
    }

    void HideActiveShopRows()
    {
        for (int i = 0; i < shopRowObjects.Count; i++)
        {
            if (shopRowObjects[i] != null)
                shopRowObjects[i].SetActive(false);
        }

        shopRowObjects.Clear();
    }

    void RefreshMissEnigmaShopBrowser(bool resetScrollPosition, float previousScrollPosition)
    {
        BlueprintTradeOffer[] offers = BlueprintCatalog.GetMissEnigmaOffers();
        List<MissEnigmaOfferViewModel> visibleOffers = new List<MissEnigmaOfferViewModel>();
        InventoryItemDefinition avengerCodesDefinition = InventoryItemCatalog.GetDefinition(InventoryItemCatalog.AvengerStartingCodesId);
        bool showAvengerCodesOffer = avengerCodesDefinition != null &&
            PlayerProfileService.Instance.CanBuyAvengerStartingCodes();
        int avengerCodesPrice = showAvengerCodesOffer
            ? PlayerProfileService.Instance.GetShopBuyPriceAstrons(InventoryItemCatalog.AvengerStartingCodesId)
            : 0;

        for (int i = 0; i < offers.Length; i++)
        {
            BlueprintTradeOffer offer = offers[i];
            if (offer == null || string.IsNullOrWhiteSpace(offer.BlueprintItemId))
                continue;

            if (PlayerProfileService.Instance.IsBlueprintUnlocked(offer.BlueprintItemId))
                continue;

            if (PlayerProfileService.Instance.IsMissEnigmaBlueprintPurchased(offer.BlueprintItemId))
                continue;

            InventoryItemDefinition blueprintDefinition = InventoryItemCatalog.GetDefinition(offer.BlueprintItemId);
            if (blueprintDefinition == null)
                continue;

            string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(offer.BlueprintItemId);
            InventoryItemDefinition targetDefinition = InventoryItemCatalog.GetDefinition(targetItemId);
            visibleOffers.Add(new MissEnigmaOfferViewModel
            {
                Offer = offer,
                CanAfford = PlayerProfileService.Instance.CanAffordItemTrade(offer.CostItemIds),
                BlueprintDefinition = blueprintDefinition,
                TargetDefinition = targetDefinition,
                EstimatedTradeValue = GetMissEnigmaTradeValue(offer.CostItemIds)
            });
        }

        if (visibleOffers.Count == 0 && !showAvengerCodesOffer)
        {
            GameObject rowObject = GetOrCreateMissEnigmaEmptyRow();
            rowObject.transform.SetParent(shopContentRect, false);
            rowObject.transform.SetSiblingIndex(0);
            rowObject.SetActive(true);
            shopRowObjects.Add(rowObject);
            ApplyShopScrollPosition(resetScrollPosition ? 1f : previousScrollPosition);
            return;
        }

        if (showAvengerCodesOffer && avengerCodesPrice > 0)
        {
            PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
            bool canAffordCodes = profile != null && profile.Astrons >= avengerCodesPrice;
            GameObject codesRow = CreateShopRow(shopRowObjects.Count, avengerCodesDefinition, avengerCodesPrice, canAffordCodes, null, 0, false);
            if (codesRow != null)
                shopRowObjects.Add(codesRow);
        }

        SortMissEnigmaOffers(visibleOffers);

        for (int i = 0; i < visibleOffers.Count; i += 2)
        {
            MissEnigmaOfferViewModel leftOffer = visibleOffers[i];
            MissEnigmaOfferViewModel rightOffer = i + 1 < visibleOffers.Count ? visibleOffers[i + 1] : null;

            GameObject row = CreateMissEnigmaShopRow(
                shopRowObjects.Count,
                leftOffer.Offer,
                leftOffer.CanAfford,
                rightOffer != null ? rightOffer.Offer : null,
                rightOffer != null && rightOffer.CanAfford);
            if (row != null)
                shopRowObjects.Add(row);
        }

        ApplyShopScrollPosition(resetScrollPosition ? 1f : previousScrollPosition);
    }

    void SortShopOffers(List<ShopOfferViewModel> offers)
    {
        if (offers == null || offers.Count <= 1)
            return;

        ShopSortMode mode = GetShopSortMode(selectedTraderShop);
        offers.Sort((a, b) => CompareShopOffers(a, b, mode));
    }

    int CompareShopOffers(ShopOfferViewModel a, ShopOfferViewModel b, ShopSortMode mode)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int result;
        switch (mode)
        {
            case ShopSortMode.Price:
                result = a.Price.CompareTo(b.Price);
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Type:
                result = GetItemSortCategory(a.Definition).CompareTo(GetItemSortCategory(b.Definition));
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Rarity:
                result = ((int)GetItemSortRarity(b.Definition)).CompareTo((int)GetItemSortRarity(a.Definition));
                if (result != 0)
                    return result;
                result = GetItemSortCategory(a.Definition).CompareTo(GetItemSortCategory(b.Definition));
                if (result != 0)
                    return result;
                break;
        }

        return CompareItemDefinitionNames(a.Definition, b.Definition);
    }

    void SortMissEnigmaOffers(List<MissEnigmaOfferViewModel> offers)
    {
        if (offers == null || offers.Count <= 1)
            return;

        ShopSortMode mode = GetShopSortMode(selectedTraderShop);
        offers.Sort((a, b) => CompareMissEnigmaOffers(a, b, mode));
    }

    int CompareMissEnigmaOffers(MissEnigmaOfferViewModel a, MissEnigmaOfferViewModel b, ShopSortMode mode)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        InventoryItemDefinition aDefinition = GetMissEnigmaSortDefinition(a);
        InventoryItemDefinition bDefinition = GetMissEnigmaSortDefinition(b);

        int result;
        switch (mode)
        {
            case ShopSortMode.Price:
                result = a.EstimatedTradeValue.CompareTo(b.EstimatedTradeValue);
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Type:
                result = GetItemSortCategory(aDefinition).CompareTo(GetItemSortCategory(bDefinition));
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Rarity:
                result = ((int)GetItemSortRarity(bDefinition)).CompareTo((int)GetItemSortRarity(aDefinition));
                if (result != 0)
                    return result;
                result = GetItemSortCategory(aDefinition).CompareTo(GetItemSortCategory(bDefinition));
                if (result != 0)
                    return result;
                break;
        }

        return CompareItemDefinitionNames(aDefinition, bDefinition);
    }

    InventoryItemDefinition GetMissEnigmaSortDefinition(MissEnigmaOfferViewModel offer)
    {
        if (offer == null)
            return null;

        return offer.TargetDefinition ?? offer.BlueprintDefinition;
    }

    int GetMissEnigmaTradeValue(string[] costItemIds)
    {
        if (costItemIds == null || costItemIds.Length == 0)
            return 0;

        int value = 0;
        for (int i = 0; i < costItemIds.Length; i++)
        {
            string itemId = costItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int itemValue = InventoryItemCatalog.GetSellValueAstrons(itemId);
            if (itemValue <= 0)
                itemValue = InventoryItemCatalog.GetShopBuyValueAstrons(itemId);
            value += Mathf.Max(1, itemValue);
        }

        return value;
    }

    InventoryItemCategory GetItemSortCategory(InventoryItemDefinition definition)
    {
        return definition != null ? definition.Category : InventoryItemCategory.Misc;
    }

    InventoryItemRarity GetItemSortRarity(InventoryItemDefinition definition)
    {
        return definition != null ? definition.Rarity : InventoryItemRarity.Common;
    }

    int CompareItemDefinitionNames(InventoryItemDefinition a, InventoryItemDefinition b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int result = string.Compare(a.DisplayName ?? string.Empty, b.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (result != 0)
            return result;

        return string.CompareOrdinal(a.Id ?? string.Empty, b.Id ?? string.Empty);
    }

    GameObject GetOrCreateMissEnigmaEmptyRow()
    {
        if (missEnigmaEmptyRowObject != null)
            return missEnigmaEmptyRowObject;

        missEnigmaEmptyRowObject = new GameObject("ShopRow_MissEnigmaEmpty", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        missEnigmaEmptyRowObject.transform.SetParent(shopContentRect, false);
        missEnigmaEmptyRowObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 180f);
        missEnigmaEmptyRowObject.GetComponent<LayoutElement>().preferredHeight = 180f;
        missEnigmaEmptyRowObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 0.98f);
        missEnigmaEmptyText = CreateText(missEnigmaEmptyRowObject.transform, "MissEnigmaEmptyText", "ALL BLUEPRINTS SOLD", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24f, TextAlignmentOptions.Center);
        missEnigmaEmptyText.fontStyle = FontStyles.Bold;
        missEnigmaEmptyText.color = new Color(0.84f, 0.9f, 0.96f, 0.96f);
        return missEnigmaEmptyRowObject;
    }

    GameObject CreateMissEnigmaShopRow(
        int rowIndex,
        BlueprintTradeOffer leftOffer,
        bool leftCanAfford,
        BlueprintTradeOffer rightOffer,
        bool rightCanAfford)
    {
        if (shopContentRect == null || leftOffer == null)
            return null;

        MissEnigmaRowView rowView = GetOrCreateMissEnigmaShopRowView(rowIndex);
        if (rowView == null || rowView.Root == null)
            return null;

        GameObject rowObject = rowView.Root;
        rowObject.name = "ShopRow_" + leftOffer.BlueprintItemId;
        rowObject.transform.SetParent(shopContentRect, false);
        rowObject.transform.SetSiblingIndex(rowIndex);
        rowObject.SetActive(true);
        rowObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 326f);
        rowObject.GetComponent<LayoutElement>().preferredHeight = 326f;
        rowObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        UpdateMissEnigmaBlueprintCard(rowView.Left, leftOffer, leftCanAfford);
        UpdateMissEnigmaBlueprintCard(rowView.Right, rightOffer, rightCanAfford);

        return rowObject;
    }

    MissEnigmaRowView GetOrCreateMissEnigmaShopRowView(int rowIndex)
    {
        while (missEnigmaShopRowPool.Count <= rowIndex)
            missEnigmaShopRowPool.Add(null);

        MissEnigmaRowView rowView = missEnigmaShopRowPool[rowIndex];
        if (rowView != null && rowView.Root != null)
            return rowView;

        GameObject rowObject = new GameObject("ShopRow_MissEnigmaPool_" + rowIndex, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(shopContentRect, false);

        rowView = new MissEnigmaRowView
        {
            Root = rowObject,
            Left = CreateMissEnigmaBlueprintCardView(rowObject.transform, "Left", -130f),
            Right = CreateMissEnigmaBlueprintCardView(rowObject.transform, "Right", 130f)
        };
        missEnigmaShopRowPool[rowIndex] = rowView;
        return rowView;
    }

    MissEnigmaCardView CreateMissEnigmaBlueprintCardView(Transform parent, string suffix, float centerX)
    {
        MissEnigmaCardView view = new MissEnigmaCardView();
        view.CardButton = CreateButton(parent, "ShopItemCardButton_MissEnigma_" + suffix, string.Empty, new Vector2(centerX, -12f), new Vector2(214f, 214f), null);
        view.Root = view.CardButton.gameObject;
        view.CardImage = view.CardButton.GetComponent<Image>();

        Outline itemCardOutline = view.CardButton.GetComponent<Outline>();
        if (itemCardOutline == null)
            itemCardOutline = view.CardButton.gameObject.AddComponent<Outline>();
        itemCardOutline.effectColor = new Color(0.08f, 0.76f, 0.94f, 0.38f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

        GameObject iconObject = new GameObject("ShopItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(view.CardButton.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);
        iconRect.sizeDelta = new Vector2(108f, 108f);
        view.IconImage = iconObject.GetComponent<Image>();
        view.IconImage.preserveAspect = true;
        view.IconImage.raycastTarget = false;

        view.NameText = CreateText(view.CardButton.transform, "ShopItemName", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 44f), 18f, TextAlignmentOptions.Center);
        view.NameText.enableAutoSizing = true;
        view.NameText.fontSizeMin = 11f;
        view.NameText.fontSizeMax = 18f;
        view.NameText.textWrappingMode = TextWrappingModes.Normal;
        view.NameText.overflowMode = TextOverflowModes.Truncate;
        view.NameText.raycastTarget = false;

        view.CostText = CreateText(view.CardButton.transform, "ShopItemCost", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(198f, 58f), 20f, TextAlignmentOptions.Center);
        view.CostText.enableAutoSizing = true;
        view.CostText.fontSizeMin = 14f;
        view.CostText.fontSizeMax = 20f;
        view.CostText.textWrappingMode = TextWrappingModes.Normal;
        view.CostText.raycastTarget = false;

        view.TradeButton = CreateButton(parent, "ShopTradeButton_MissEnigma_" + suffix, "TRADE", new Vector2(centerX, -258f), new Vector2(150f, 50f), null);
        view.TradeText = view.TradeButton.GetComponentInChildren<TMP_Text>(true);
        StyleButton(view.TradeButton, new Color(0.18f, 0.36f, 0.5f, 1f), new Color(0.26f, 0.52f, 0.68f, 1f));

        return view;
    }

    void UpdateMissEnigmaBlueprintCard(MissEnigmaCardView view, BlueprintTradeOffer offer, bool canAfford)
    {
        if (view == null)
            return;

        InventoryItemDefinition definition = offer != null
            ? InventoryItemCatalog.GetDefinition(offer.BlueprintItemId)
            : null;
        bool active = definition != null;
        if (view.Root != null)
            view.Root.SetActive(active);
        if (view.TradeButton != null)
            view.TradeButton.gameObject.SetActive(active);
        if (!active)
            return;

        string blueprintItemId = offer.BlueprintItemId;
        view.CardButton.onClick.RemoveAllListeners();
        view.CardButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnShopItemPreviewClicked(blueprintItemId);
        });
        view.CardButton.interactable = !inventoryActionInProgress;

        if (view.CardImage != null)
            view.CardImage.color = InventoryItemCatalog.GetRarityColor(definition.Rarity);
        if (view.IconImage != null)
            view.IconImage.sprite = definition.GetIcon();
        if (view.NameText != null)
            view.NameText.text = definition.DisplayName.ToUpperInvariant();
        if (view.CostText != null)
        {
            view.CostText.text = FormatTradeCost(offer.CostItemIds);
            view.CostText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        }

        view.TradeButton.onClick.RemoveAllListeners();
        view.TradeButton.onClick.AddListener(() =>
        {
            OnMissEnigmaTradeClicked(blueprintItemId);
        });
        view.TradeButton.interactable = canAfford && !inventoryActionInProgress;
        if (view.TradeText != null)
            view.TradeText.text = "TRADE";
    }

    void CreateMissEnigmaBlueprintCard(Transform parent, BlueprintTradeOffer offer, bool canAfford, float centerX)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(offer.BlueprintItemId);
        if (parent == null || definition == null)
            return;

        Button itemCardButton = CreateButton(parent, "ShopItemCardButton_" + definition.Id, string.Empty, new Vector2(centerX, -12f), new Vector2(214f, 214f), () => OnShopItemPreviewClicked(definition.Id));
        itemCardButton.interactable = !inventoryActionInProgress;

        Image itemCardImage = itemCardButton.GetComponent<Image>();
        if (itemCardImage != null)
            itemCardImage.color = InventoryItemCatalog.GetRarityColor(definition.Rarity);

        Outline itemCardOutline = itemCardButton.GetComponent<Outline>();
        if (itemCardOutline == null)
            itemCardOutline = itemCardButton.gameObject.AddComponent<Outline>();
        itemCardOutline.effectColor = new Color(0.08f, 0.76f, 0.94f, 0.38f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

        GameObject iconObject = new GameObject("ShopItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(itemCardButton.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);
        iconRect.sizeDelta = new Vector2(108f, 108f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = definition.GetIcon();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text nameText = CreateText(itemCardButton.transform, "ShopItemName", definition.DisplayName.ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 44f), 18f, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 11f;
        nameText.fontSizeMax = 18f;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.raycastTarget = false;

        TMP_Text costText = CreateText(itemCardButton.transform, "ShopItemCost", FormatTradeCost(offer.CostItemIds), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(190f, 42f), 16f, TextAlignmentOptions.Center);
        costText.enableAutoSizing = true;
        costText.fontSizeMin = 11f;
        costText.fontSizeMax = 16f;
        costText.textWrappingMode = TextWrappingModes.Normal;
        costText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        costText.raycastTarget = false;

        Button tradeButton = CreateButton(parent, "ShopTradeButton_" + definition.Id, "TRADE", new Vector2(centerX, -258f), new Vector2(150f, 50f), () => OnMissEnigmaTradeClicked(offer.BlueprintItemId));
        StyleButton(tradeButton, new Color(0.18f, 0.36f, 0.5f, 1f), new Color(0.26f, 0.52f, 0.68f, 1f));
        tradeButton.interactable = canAfford && !inventoryActionInProgress;
    }

    string FormatTradeCost(string[] costItemIds)
    {
        if (costItemIds == null || costItemIds.Length == 0)
            return "FREE";

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < costItemIds.Length; i++)
        {
            string itemId = costItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int count);
            counts[itemId] = count + 1;
        }

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> entry in counts)
        {
            parts.Add(InventoryItemCatalog.GetDisplayName(entry.Key) + (entry.Value > 1 ? " x" + entry.Value : string.Empty));
        }

        parts.Sort(StringComparer.Ordinal);
        return string.Join("\n", parts);
    }

    void UpdateShopBrowserTitle()
    {
        TMP_Text title = shopBrowserObject?.transform.Find("ShopBrowserPanel/ShopBrowserTitle")?.GetComponent<TMP_Text>();
        if (title == null)
            return;

        title.text = TraderOpensShop(selectedTraderShop)
            ? GetTraderDisplayName(selectedTraderShop)
            : "TRADER";
    }

    GameObject CreateShopRow(
        int rowIndex,
        InventoryItemDefinition leftDefinition,
        int leftPrice,
        bool leftCanAfford,
        InventoryItemDefinition rightDefinition,
        int rightPrice,
        bool rightCanAfford)
    {
        if (shopContentRect == null || leftDefinition == null)
            return null;

        ShopRowView rowView = GetOrCreateShopRowView(rowIndex);
        if (rowView == null || rowView.Root == null)
            return null;

        GameObject rowObject = rowView.Root;
        rowObject.name = "ShopRow_" + leftDefinition.Id;
        rowObject.transform.SetParent(shopContentRect, false);
        rowObject.transform.SetSiblingIndex(rowIndex);
        rowObject.SetActive(true);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 294f);

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 294f;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        UpdateShopCard(rowView.Left, leftDefinition, leftPrice, leftCanAfford);
        UpdateShopCard(rowView.Right, rightDefinition, rightPrice, rightCanAfford);

        return rowObject;
    }

    ShopRowView GetOrCreateShopRowView(int rowIndex)
    {
        while (shopRowPool.Count <= rowIndex)
            shopRowPool.Add(null);

        ShopRowView rowView = shopRowPool[rowIndex];
        if (rowView != null && rowView.Root != null)
            return rowView;

        GameObject rowObject = new GameObject("ShopRowPool_" + rowIndex, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(shopContentRect, false);

        rowView = new ShopRowView
        {
            Root = rowObject,
            Left = CreateShopCardView(rowObject.transform, "Left", -130f),
            Right = CreateShopCardView(rowObject.transform, "Right", 130f)
        };
        shopRowPool[rowIndex] = rowView;
        return rowView;
    }

    ShopCardView CreateShopCardView(Transform parent, string suffix, float centerX)
    {
        ShopCardView view = new ShopCardView();
        view.CardButton = CreateButton(parent, "ShopItemCardButton_" + suffix, string.Empty, new Vector2(centerX, -12f), new Vector2(214f, 214f), null);
        view.Root = view.CardButton.gameObject;
        view.CardImage = view.CardButton.GetComponent<Image>();

        Outline itemCardOutline = view.CardButton.GetComponent<Outline>();
        if (itemCardOutline == null)
            itemCardOutline = view.CardButton.gameObject.AddComponent<Outline>();
        itemCardOutline.effectColor = new Color(0f, 0f, 0f, 0.28f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

        Shadow itemCardShadow = view.CardButton.GetComponent<Shadow>();
        if (itemCardShadow == null)
            itemCardShadow = view.CardButton.gameObject.AddComponent<Shadow>();
        itemCardShadow.effectColor = new Color(0f, 0f, 0f, 0.2f);
        itemCardShadow.effectDistance = new Vector2(0f, -3f);

        GameObject iconObject = new GameObject("ShopItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(view.CardButton.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);
        iconRect.sizeDelta = new Vector2(108f, 108f);
        view.IconImage = iconObject.GetComponent<Image>();
        view.IconImage.preserveAspect = true;
        view.IconImage.raycastTarget = false;

        view.NameText = CreateText(view.CardButton.transform, "ShopItemName", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 28f), 18f, TextAlignmentOptions.Center);
        view.NameText.enableAutoSizing = true;
        view.NameText.fontSizeMin = 12f;
        view.NameText.fontSizeMax = 18f;
        view.NameText.textWrappingMode = TextWrappingModes.Normal;
        view.NameText.overflowMode = TextOverflowModes.Truncate;
        view.NameText.raycastTarget = false;

        view.TypeText = CreateText(view.CardButton.transform, "ShopItemType", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(182f, 22f), 15f, TextAlignmentOptions.Center);
        view.TypeText.fontStyle = FontStyles.Bold;
        view.TypeText.color = new Color(0.92f, 0.96f, 1f, 0.96f);
        view.TypeText.raycastTarget = false;

        GameObject priceIconObject = new GameObject("ShopItemPriceIcon", typeof(RectTransform), typeof(Image));
        priceIconObject.transform.SetParent(view.CardButton.transform, false);
        RectTransform priceIconRect = priceIconObject.GetComponent<RectTransform>();
        priceIconRect.anchorMin = new Vector2(0.5f, 1f);
        priceIconRect.anchorMax = new Vector2(0.5f, 1f);
        priceIconRect.pivot = new Vector2(0.5f, 0.5f);
        priceIconRect.anchoredPosition = new Vector2(-42f, -176f);
        priceIconRect.sizeDelta = new Vector2(28f, 28f);
        view.PriceIcon = priceIconObject.GetComponent<Image>();
        view.PriceIcon.sprite = LoadSpriteFromResources("UI/icon_astrons_coin");
        view.PriceIcon.color = new Color(1f, 1f, 1f, 0.96f);
        view.PriceIcon.preserveAspect = true;
        view.PriceIcon.raycastTarget = false;

        view.PriceText = CreateText(view.CardButton.transform, "ShopItemPrice", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(28f, -176f), new Vector2(112f, 28f), 21f, TextAlignmentOptions.MidlineLeft);
        view.PriceText.fontStyle = FontStyles.Normal;
        view.PriceText.raycastTarget = false;

        view.BuyButton = CreateButton(parent, "ShopBuyButton_" + suffix, "BUY", new Vector2(centerX, -236f), new Vector2(150f, 50f), null);
        view.BuyText = view.BuyButton.GetComponentInChildren<TMP_Text>(true);
        StyleButton(view.BuyButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));

        return view;
    }

    void UpdateShopCard(ShopCardView view, InventoryItemDefinition definition, int price, bool canAfford)
    {
        if (view == null)
            return;

        bool active = definition != null;
        if (view.Root != null)
            view.Root.SetActive(active);
        if (view.BuyButton != null)
            view.BuyButton.gameObject.SetActive(active);
        if (!active)
            return;

        string itemId = definition.Id;
        view.CardButton.onClick.RemoveAllListeners();
        view.CardButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnShopItemPreviewClicked(itemId);
        });
        view.CardButton.interactable = !inventoryActionInProgress;

        if (view.CardImage != null)
            view.CardImage.color = InventoryItemCatalog.GetRarityColor(definition.Rarity);
        if (view.IconImage != null)
            view.IconImage.sprite = definition.GetIcon();
        if (view.NameText != null)
            view.NameText.text = definition.DisplayName.ToUpperInvariant();
        if (view.TypeText != null)
            view.TypeText.text = InventoryItemCatalog.GetCategoryLabel(definition.Id);
        if (view.PriceText != null)
        {
            view.PriceText.text = price.ToString();
            view.PriceText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        }

        view.BuyButton.onClick.RemoveAllListeners();
        view.BuyButton.onClick.AddListener(() =>
        {
            OnShopBuyClicked(itemId);
        });
        view.BuyButton.interactable = canAfford && !inventoryActionInProgress;
        if (view.BuyText != null)
            view.BuyText.text = "BUY";
    }

    void CreateShopCard(Transform parent, InventoryItemDefinition definition, int price, bool canAfford, float centerX)
    {
        if (parent == null || definition == null)
            return;

        Button itemCardButton = CreateButton(parent, "ShopItemCardButton_" + definition.Id, string.Empty, new Vector2(centerX, -12f), new Vector2(214f, 214f), () => OnShopItemPreviewClicked(definition.Id));
        itemCardButton.interactable = !inventoryActionInProgress;

        Image itemCardImage = itemCardButton.GetComponent<Image>();
        if (itemCardImage != null)
            itemCardImage.color = InventoryItemCatalog.GetRarityColor(definition.Rarity);

        Outline itemCardOutline = itemCardButton.GetComponent<Outline>();
        if (itemCardOutline == null)
            itemCardOutline = itemCardButton.gameObject.AddComponent<Outline>();
        itemCardOutline.effectColor = new Color(0f, 0f, 0f, 0.28f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

        Shadow itemCardShadow = itemCardButton.GetComponent<Shadow>();
        if (itemCardShadow == null)
            itemCardShadow = itemCardButton.gameObject.AddComponent<Shadow>();
        itemCardShadow.effectColor = new Color(0f, 0f, 0f, 0.2f);
        itemCardShadow.effectDistance = new Vector2(0f, -3f);

        GameObject iconObject = new GameObject("ShopItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(itemCardButton.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);
        iconRect.sizeDelta = new Vector2(108f, 108f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = definition.GetIcon();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text nameText = CreateText(itemCardButton.transform, "ShopItemName", definition.DisplayName.ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 28f), 18f, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12f;
        nameText.fontSizeMax = 18f;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.raycastTarget = false;

        TMP_Text typeText = CreateText(itemCardButton.transform, "ShopItemType", InventoryItemCatalog.GetCategoryLabel(definition.Id), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(182f, 22f), 15f, TextAlignmentOptions.Center);
        typeText.fontStyle = FontStyles.Bold;
        typeText.color = new Color(0.92f, 0.96f, 1f, 0.96f);
        typeText.raycastTarget = false;

        GameObject priceIconObject = new GameObject("ShopItemPriceIcon", typeof(RectTransform), typeof(Image));
        priceIconObject.transform.SetParent(itemCardButton.transform, false);
        RectTransform priceIconRect = priceIconObject.GetComponent<RectTransform>();
        priceIconRect.anchorMin = new Vector2(0.5f, 1f);
        priceIconRect.anchorMax = new Vector2(0.5f, 1f);
        priceIconRect.pivot = new Vector2(0.5f, 0.5f);
        priceIconRect.anchoredPosition = new Vector2(-42f, -176f);
        priceIconRect.sizeDelta = new Vector2(28f, 28f);

        Image priceIcon = priceIconObject.GetComponent<Image>();
        priceIcon.sprite = LoadSpriteFromResources("UI/icon_astrons_coin");
        priceIcon.color = new Color(1f, 1f, 1f, 0.96f);
        priceIcon.preserveAspect = true;
        priceIcon.raycastTarget = false;

        TMP_Text priceText = CreateText(itemCardButton.transform, "ShopItemPrice", price.ToString(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(28f, -176f), new Vector2(112f, 28f), 21f, TextAlignmentOptions.MidlineLeft);
        priceText.fontStyle = FontStyles.Normal;
        priceText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        priceText.raycastTarget = false;

        Button buyButton = CreateButton(parent, "ShopBuyButton_" + definition.Id, "BUY", new Vector2(centerX, -236f), new Vector2(150f, 50f), () => OnShopBuyClicked(definition.Id));
        StyleButton(buyButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        buyButton.interactable = canAfford && !inventoryActionInProgress;
    }

    void OnShopItemPreviewClicked(string itemId)
    {
        if (inventoryActionInProgress || dragInProgress || string.IsNullOrWhiteSpace(itemId))
            return;

        if (IsPreviewingSameItem(ProfileItemSource.ShopListing, -1, itemId))
        {
            HideItemPreview();
            SetStatus(string.Empty);
            return;
        }

        ShowItemPreview(ProfileItemSource.ShopListing, -1, itemId);
        SetStatus("Shop item selected.");
    }

    async void OnShopBuyClicked(string itemId)
    {
        if (inventoryActionInProgress || string.IsNullOrWhiteSpace(itemId))
            return;

        inventoryActionInProgress = true;
        SetInventoryInteractable(false);
        SetStatus("Buying " + InventoryItemCatalog.GetDisplayName(itemId) + "...");

        try
        {
            bool bought = await PlayerProfileService.Instance.TryBuyShopItemAsync(itemId);
            if (bought)
            {
                SetStatus("Bought " + InventoryItemCatalog.GetDisplayName(itemId) + ".");
                AudioManager.Instance?.PlayCash();
                RefreshView();
            }
            else
            {
                SetStatus("Not enough Astrons or inventory space.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Shop purchase failed: " + ex);
            SetStatus("Purchase failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInventoryInteractable(true);
            RefreshShopBrowser();
        }
    }

    async void OnMissEnigmaTradeClicked(string blueprintItemId)
    {
        if (inventoryActionInProgress || string.IsNullOrWhiteSpace(blueprintItemId))
            return;

        BlueprintTradeOffer offer = BlueprintCatalog.GetMissEnigmaOffer(blueprintItemId);
        if (offer == null)
            return;

        inventoryActionInProgress = true;
        SetInventoryInteractable(false);
        SetStatus("Trading for " + InventoryItemCatalog.GetDisplayName(blueprintItemId) + "...");

        try
        {
            bool traded = await PlayerProfileService.Instance.TryPurchaseMissEnigmaBlueprintAsync(blueprintItemId);
            if (traded)
            {
                SetStatus("Traded for " + InventoryItemCatalog.GetDisplayName(blueprintItemId) + ".");
                AudioManager.Instance?.PlayCash();
                RefreshView();
            }
            else
            {
                SetStatus("Missing trade items or inventory space.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Miss Enigma trade failed: " + ex);
            SetStatus("Trade failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInventoryInteractable(true);
            RefreshShopBrowser();
        }
    }
}
