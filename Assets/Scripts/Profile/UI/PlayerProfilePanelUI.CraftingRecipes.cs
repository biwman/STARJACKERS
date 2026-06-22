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
    void RefreshCraftingRecipeBrowser(bool forceRebuild = false)
    {
        using var marker = CraftingRecipeRefreshMarker.Auto();

        if (craftingRecipeBrowserObject == null || craftingRecipeContentRect == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        PlayerInventoryData inventory = profile != null && profile.Inventory != null
            ? profile.Inventory.Clone()
            : PlayerInventoryData.Default();
        inventory.Normalize();

        string signature = BuildCraftingRecipeBrowserSignature(inventory);
        if (!forceRebuild &&
            craftingRecipeBrowserSignatureValid &&
            string.Equals(craftingRecipeBrowserSignature, signature, StringComparison.Ordinal))
        {
            RefreshCraftingRecipeAvailabilityButton();
            SetCraftingRecipeRowsInteractable(inventoryControlsInteractable);
            return;
        }

        float previousScrollPosition = craftingRecipeScrollRect != null
            ? craftingRecipeScrollRect.verticalNormalizedPosition
            : 1f;

        craftingRecipeRowObjects.Clear();
        craftingRecipeResultButtons.Clear();
        craftingRecipeBrowserSignature = signature;
        craftingRecipeBrowserSignatureValid = true;
        HashSet<string> visibleRecipeIds = new HashSet<string>(StringComparer.Ordinal);

        IReadOnlyList<PlayerProfileCraftingRecipe> recipes = PlayerProfileCraftingCatalog.GetAllRecipes();
        if (recipes == null)
            return;

        List<PlayerProfileCraftingRecipe> orderedRecipes = new List<PlayerProfileCraftingRecipe>(recipes.Count);
        for (int pass = 0; pass < 2; pass++)
        {
            bool unlockedPass = pass == 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                PlayerProfileCraftingRecipe recipe = recipes[i];
                if (recipe == null)
                    continue;

                if (IsCraftingRecipeUnlocked(recipe) == unlockedPass)
                    orderedRecipes.Add(recipe);
            }
        }

        for (int i = 0; i < orderedRecipes.Count; i++)
        {
            PlayerProfileCraftingRecipe recipe = orderedRecipes[i];
            if (recipe == null)
                continue;

            bool craftable = CanPrepareCraftingRecipe(inventory, recipe);
            if (craftingRecipeShowAvailableOnly && !craftable)
                continue;

            if (!craftingRecipeRowsById.TryGetValue(recipe.Id, out CraftingRecipeRowView rowView) || rowView == null || rowView.Root == null)
            {
                rowView = CreateCraftingRecipeRow(recipe, craftable, inventory);
                if (rowView != null)
                    craftingRecipeRowsById[recipe.Id] = rowView;
            }

            if (rowView == null || rowView.Root == null)
                continue;

            UpdateCraftingRecipeRowView(rowView, recipe, craftable, inventory);
            rowView.Root.SetActive(true);
            rowView.Root.transform.SetSiblingIndex(craftingRecipeRowObjects.Count);
            visibleRecipeIds.Add(recipe.Id);
            craftingRecipeRowObjects.Add(rowView.Root);
            if (rowView.ResultButton != null)
                craftingRecipeResultButtons.Add(rowView.ResultButton);
        }

        foreach (KeyValuePair<string, CraftingRecipeRowView> entry in craftingRecipeRowsById)
        {
            CraftingRecipeRowView rowView = entry.Value;
            if (rowView != null && rowView.Root != null && !visibleRecipeIds.Contains(entry.Key))
                rowView.Root.SetActive(false);
        }

        RefreshCraftingRecipeAvailabilityButton();
        Canvas.ForceUpdateCanvases();
        if (craftingRecipeScrollRect != null)
        {
            craftingRecipeScrollRect.verticalNormalizedPosition = resetCraftingRecipeScrollOnNextRefresh
                ? 1f
                : Mathf.Clamp01(previousScrollPosition);
            resetCraftingRecipeScrollOnNextRefresh = false;
        }
    }

    string BuildCraftingRecipeBrowserSignature(PlayerInventoryData inventory)
    {
        StringBuilder builder = new StringBuilder(512);
        builder.Append(craftingRecipeShowAvailableOnly ? "available|" : "all|");
        AppendInventorySlotsSignature(builder, inventory != null ? inventory.PlayerSlots : null);
        builder.Append('|');
        AppendInventorySlotsSignature(builder, inventory != null ? inventory.ShipSlots : null);
        builder.Append('|');
        AppendInventorySlotsSignature(builder, inventory != null ? inventory.CraftingSlots : null);
        builder.Append('|');
        AppendInventorySlotsSignature(builder, PlayerProfileService.Instance.CurrentProfile != null ? PlayerProfileService.Instance.CurrentProfile.UnlockedBlueprintIds : null);
        return builder.ToString();
    }

    void AppendInventorySlotsSignature(StringBuilder builder, string[] slots)
    {
        if (builder == null)
            return;

        if (slots == null)
        {
            builder.Append("null");
            return;
        }

        builder.Append(slots.Length);
        builder.Append(':');
        for (int i = 0; i < slots.Length; i++)
        {
            builder.Append(i);
            builder.Append('=');
            builder.Append(slots[i] ?? string.Empty);
            builder.Append(';');
        }
    }

    void SetCraftingRecipeRowsInteractable(bool interactable)
    {
        bool canInteract = interactable && (preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress);
        for (int i = 0; i < craftingRecipeResultButtons.Count; i++)
        {
            Button button = craftingRecipeResultButtons[i];
            if (button != null)
                button.interactable = canInteract;
        }
    }

    CraftingRecipeRowView CreateCraftingRecipeRow(PlayerProfileCraftingRecipe recipe, bool craftable, PlayerInventoryData inventory)
    {
        if (craftingRecipeContentRect == null || recipe == null)
            return null;

        GameObject rowObject = new GameObject("CraftingRecipeRow_" + recipe.Id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(craftingRecipeContentRect, false);
        CraftingRecipeRowView rowView = new CraftingRecipeRowView
        {
            RecipeId = recipe.Id,
            Root = rowObject
        };

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 196f);

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 196f;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        Button resultButton = CreateButton(rowObject.transform, "RecipeResultButton", string.Empty, new Vector2(-228f, -10f), new Vector2(174f, 174f), () => OnCraftingRecipeSelected(recipe.Id));
        ConfigureNoBlinkInventoryActionButton(resultButton);
        resultButton.interactable = inventoryControlsInteractable && (preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress);
        rowView.ResultButton = resultButton;

        Image resultButtonImage = resultButton.GetComponent<Image>();
        rowView.ResultButtonImage = resultButtonImage;
        if (resultButtonImage != null)
        {
            Color rarityColor = InventoryItemCatalog.GetRarityColor(recipe.OutputItemId);
            resultButtonImage.type = Image.Type.Simple;
            resultButtonImage.color = craftable
                ? rarityColor
                : Color.Lerp(rarityColor, new Color(0.16f, 0.18f, 0.22f, 1f), 0.58f);
        }

        Outline resultOutline = resultButton.GetComponent<Outline>();
        if (resultOutline == null)
            resultOutline = resultButton.gameObject.AddComponent<Outline>();
        rowView.ResultOutline = resultOutline;

        resultOutline.effectColor = craftable
            ? new Color(0.23f, 0.92f, 0.49f, 0.95f)
            : new Color(0.28f, 0.36f, 0.44f, 0.8f);
        resultOutline.effectDistance = craftable ? new Vector2(4f, 4f) : new Vector2(2f, 2f);

        Shadow resultShadow = resultButton.GetComponent<Shadow>();
        if (resultShadow == null)
            resultShadow = resultButton.gameObject.AddComponent<Shadow>();
        rowView.ResultShadow = resultShadow;

        resultShadow.effectColor = craftable
            ? new Color(0.07f, 0.38f, 0.18f, 0.55f)
            : new Color(0f, 0f, 0f, 0.22f);
        resultShadow.effectDistance = new Vector2(0f, -3f);

        Color frameColor = craftable
            ? new Color(0.24f, 0.94f, 0.5f, 1f)
            : new Color(0.18f, 0.24f, 0.3f, 0.92f);
        float frameThickness = craftable ? 6f : 4f;

        GameObject resultFrame = new GameObject("RecipeResultFrame", typeof(RectTransform));
        resultFrame.transform.SetParent(resultButton.transform, false);
        RectTransform resultFrameRect = resultFrame.GetComponent<RectTransform>();
        resultFrameRect.anchorMin = Vector2.zero;
        resultFrameRect.anchorMax = Vector2.one;
        resultFrameRect.pivot = new Vector2(0.5f, 0.5f);
        resultFrameRect.offsetMin = Vector2.zero;
        resultFrameRect.offsetMax = Vector2.zero;

        rowView.ResultFrameRects = new RectTransform[4];
        rowView.ResultFrameImages = new Image[4];

        void CreateFrameBar(int index, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(resultFrame.transform, false);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = anchorMin;
            barRect.anchorMax = anchorMax;
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.offsetMin = offsetMin;
            barRect.offsetMax = offsetMax;

            Image barImage = bar.GetComponent<Image>();
            barImage.color = frameColor;
            barImage.raycastTarget = false;
            if (index >= 0 && index < rowView.ResultFrameRects.Length)
            {
                rowView.ResultFrameRects[index] = barRect;
                rowView.ResultFrameImages[index] = barImage;
            }
        }

        CreateFrameBar(0, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -frameThickness), new Vector2(0f, 0f));
        CreateFrameBar(1, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, frameThickness));
        CreateFrameBar(2, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(frameThickness, 0f));
        CreateFrameBar(3, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-frameThickness, 0f), new Vector2(0f, 0f));

        GameObject resultInner = new GameObject("RecipeResultInner", typeof(RectTransform), typeof(Image));
        resultInner.transform.SetParent(resultButton.transform, false);
        RectTransform resultInnerRect = resultInner.GetComponent<RectTransform>();
        resultInnerRect.anchorMin = new Vector2(0.5f, 0.5f);
        resultInnerRect.anchorMax = new Vector2(0.5f, 0.5f);
        resultInnerRect.pivot = new Vector2(0.5f, 0.5f);
        resultInnerRect.anchoredPosition = Vector2.zero;
        resultInnerRect.sizeDelta = new Vector2(164f, 164f);

        Image resultInnerImage = resultInner.GetComponent<Image>();
        resultInnerImage.color = new Color(0f, 0f, 0f, 0f);
        resultInnerImage.raycastTarget = false;

        GameObject resultIconObject = new GameObject("RecipeResultIcon", typeof(RectTransform), typeof(Image));
        resultIconObject.transform.SetParent(resultInner.transform, false);
        RectTransform resultIconRect = resultIconObject.GetComponent<RectTransform>();
        resultIconRect.anchorMin = new Vector2(0f, 0.5f);
        resultIconRect.anchorMax = new Vector2(0f, 0.5f);
        resultIconRect.pivot = new Vector2(0f, 0.5f);
        resultIconRect.anchorMin = new Vector2(0.5f, 1f);
        resultIconRect.anchorMax = new Vector2(0.5f, 1f);
        resultIconRect.pivot = new Vector2(0.5f, 1f);
        resultIconRect.anchoredPosition = new Vector2(0f, -14f);
        resultIconRect.sizeDelta = new Vector2(108f, 108f);

        Image resultIcon = resultIconObject.GetComponent<Image>();
        resultIcon.color = new Color(0f, 0f, 0f, 0f);
        resultIcon.raycastTarget = false;

        GameObject resultIconSpriteObject = new GameObject("RecipeResultIconSprite", typeof(RectTransform), typeof(Image));
        resultIconSpriteObject.transform.SetParent(resultIconObject.transform, false);
        RectTransform resultIconSpriteRect = resultIconSpriteObject.GetComponent<RectTransform>();
        resultIconSpriteRect.anchorMin = new Vector2(0.5f, 0.5f);
        resultIconSpriteRect.anchorMax = new Vector2(0.5f, 0.5f);
        resultIconSpriteRect.pivot = new Vector2(0.5f, 0.5f);
        resultIconSpriteRect.anchoredPosition = Vector2.zero;
        resultIconSpriteRect.sizeDelta = new Vector2(84f, 84f);

        Image resultIconSprite = resultIconSpriteObject.GetComponent<Image>();
        resultIconSprite.sprite = InventoryItemCatalog.GetIcon(recipe.OutputItemId);
        resultIconSprite.preserveAspect = true;
        resultIconSprite.raycastTarget = false;

        TMP_Text resultName = CreateText(resultInner.transform, "RecipeResultName", InventoryItemCatalog.GetDisplayName(recipe.OutputItemId).ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(146f, 28f), 18f, TextAlignmentOptions.Center);
        resultName.fontStyle = FontStyles.Bold;
        resultName.textWrappingMode = TextWrappingModes.Normal;
        resultName.enableAutoSizing = true;
        resultName.fontSizeMin = 11f;
        resultName.fontSizeMax = 18f;
        resultName.overflowMode = TextOverflowModes.Truncate;

        TMP_Text resultType = CreateText(resultInner.transform, "RecipeResultType", InventoryItemCatalog.GetCategoryLabel(recipe.OutputItemId), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(146f, 22f), 15f, TextAlignmentOptions.Center);
        resultType.fontStyle = FontStyles.Bold;
        resultType.color = new Color(0.92f, 0.96f, 1f, 0.96f);

        TMP_Text resultPrice = CreateText(resultInner.transform, "RecipeResultPrice", InventoryItemCatalog.GetSellValueAstrons(recipe.OutputItemId) + " Astrons", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(146f, 22f), 13f, TextAlignmentOptions.Center);
        resultPrice.fontStyle = FontStyles.Normal;
        resultPrice.color = new Color(0.94f, 0.98f, 1f, 0.96f);
        rowView.ResultPriceText = resultPrice;

        TMP_Text resultLock = CreateText(resultInner.transform, "RecipeResultLock", "BLUEPRINT\nREQUIRED", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(146f, 38f), 12f, TextAlignmentOptions.Center);
        resultLock.fontStyle = FontStyles.Bold;
        resultLock.color = new Color(1f, 0.66f, 0.36f, 0.98f);
        resultLock.textWrappingMode = TextWrappingModes.Normal;
        resultLock.gameObject.SetActive(false);
        rowView.ResultLockText = resultLock;

        bool[] missingIngredients = ResolveMissingRecipeIngredients(inventory, recipe);
        rowView.MissingIngredientFrames = new GameObject[recipe.Inputs.Length];
        float ingredientStartX = 16f;
        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string itemId = recipe.Inputs[i];
            bool missingIngredient = missingIngredients != null && i < missingIngredients.Length && missingIngredients[i];
            GameObject ingredientObject = new GameObject("RecipeIngredient_" + i, typeof(RectTransform), typeof(Image));
            ingredientObject.transform.SetParent(rowObject.transform, false);

            RectTransform ingredientRect = ingredientObject.GetComponent<RectTransform>();
            ingredientRect.anchorMin = new Vector2(0.5f, 1f);
            ingredientRect.anchorMax = new Vector2(0.5f, 1f);
            ingredientRect.pivot = new Vector2(0.5f, 1f);
            ingredientRect.anchoredPosition = new Vector2(ingredientStartX + (i * 110f), -34f);
            ingredientRect.sizeDelta = new Vector2(104f, 104f);

            Image ingredientBg = ingredientObject.GetComponent<Image>();
            ingredientBg.color = InventoryItemCatalog.GetRarityColor(itemId);

            GameObject ingredientIconObject = new GameObject("IngredientIcon", typeof(RectTransform), typeof(Image));
            ingredientIconObject.transform.SetParent(ingredientObject.transform, false);
            RectTransform ingredientIconRect = ingredientIconObject.GetComponent<RectTransform>();
            ingredientIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            ingredientIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            ingredientIconRect.pivot = new Vector2(0.5f, 0.5f);
            ingredientIconRect.anchoredPosition = new Vector2(0f, -8f);
            ingredientIconRect.sizeDelta = new Vector2(68f, 68f);

            Image ingredientIcon = ingredientIconObject.GetComponent<Image>();
            ingredientIcon.sprite = InventoryItemCatalog.GetIcon(itemId);
            ingredientIcon.preserveAspect = true;
            ingredientIcon.raycastTarget = false;

            TMP_Text ingredientLabel = CreateText(ingredientObject.transform, "IngredientLabel", InventoryItemCatalog.GetShortLabel(itemId), Vector2.zero, Vector2.one, new Vector2(0f, 26f), Vector2.zero, 15f, TextAlignmentOptions.Bottom);
            ingredientLabel.fontStyle = FontStyles.Bold;
            ingredientLabel.raycastTarget = false;

            GameObject missingFrame = CreateRecipeIngredientMissingFrame(ingredientObject.transform);
            if (missingFrame != null)
            {
                missingFrame.SetActive(missingIngredient);
                rowView.MissingIngredientFrames[i] = missingFrame;
            }
        }

        UpdateCraftingRecipeRowView(rowView, recipe, craftable, inventory);
        return rowView;
    }

    void UpdateCraftingRecipeRowView(CraftingRecipeRowView rowView, PlayerProfileCraftingRecipe recipe, bool craftable, PlayerInventoryData inventory)
    {
        if (rowView == null || recipe == null)
            return;

        bool unlocked = IsCraftingRecipeUnlocked(recipe);

        if (rowView.ResultButton != null)
            rowView.ResultButton.interactable = inventoryControlsInteractable && (preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress);

        if (rowView.ResultButtonImage != null)
        {
            Color rarityColor = InventoryItemCatalog.GetRarityColor(recipe.OutputItemId);
            rowView.ResultButtonImage.color = !unlocked
                ? Color.Lerp(rarityColor, new Color(0.08f, 0.1f, 0.13f, 1f), 0.72f)
                : craftable
                ? rarityColor
                : Color.Lerp(rarityColor, new Color(0.16f, 0.18f, 0.22f, 1f), 0.58f);
        }

        if (rowView.ResultOutline != null)
        {
            rowView.ResultOutline.effectColor = !unlocked
                ? new Color(0.82f, 0.5f, 0.22f, 0.86f)
                : craftable
                ? new Color(0.23f, 0.92f, 0.49f, 0.95f)
                : new Color(0.28f, 0.36f, 0.44f, 0.8f);
            rowView.ResultOutline.effectDistance = craftable || !unlocked ? new Vector2(4f, 4f) : new Vector2(2f, 2f);
        }

        if (rowView.ResultShadow != null)
        {
            rowView.ResultShadow.effectColor = craftable
                ? new Color(0.07f, 0.38f, 0.18f, 0.55f)
                : new Color(0f, 0f, 0f, 0.22f);
            rowView.ResultShadow.effectDistance = new Vector2(0f, -3f);
        }

        if (rowView.ResultPriceText != null)
            rowView.ResultPriceText.gameObject.SetActive(unlocked);
        if (rowView.ResultLockText != null)
            rowView.ResultLockText.gameObject.SetActive(!unlocked);

        UpdateCraftingRecipeResultFrame(rowView, craftable, unlocked);

        bool[] missingIngredients = ResolveMissingRecipeIngredients(inventory, recipe);
        if (rowView.MissingIngredientFrames == null)
            return;

        for (int i = 0; i < rowView.MissingIngredientFrames.Length; i++)
        {
            GameObject frame = rowView.MissingIngredientFrames[i];
            if (frame == null)
                continue;

            bool missing = missingIngredients != null && i < missingIngredients.Length && missingIngredients[i];
            frame.SetActive(missing);
        }
    }

    void UpdateCraftingRecipeResultFrame(CraftingRecipeRowView rowView, bool craftable, bool unlocked)
    {
        if (rowView == null || rowView.ResultFrameRects == null || rowView.ResultFrameImages == null)
            return;

        Color frameColor = !unlocked
            ? new Color(0.9f, 0.54f, 0.22f, 1f)
            : craftable
            ? new Color(0.24f, 0.94f, 0.5f, 1f)
            : new Color(0.18f, 0.24f, 0.3f, 0.92f);
        float thickness = craftable || !unlocked ? 6f : 4f;

        SetCraftingRecipeFrameBar(rowView, 0, frameColor, new Vector2(0f, -thickness), new Vector2(0f, 0f));
        SetCraftingRecipeFrameBar(rowView, 1, frameColor, new Vector2(0f, 0f), new Vector2(0f, thickness));
        SetCraftingRecipeFrameBar(rowView, 2, frameColor, new Vector2(0f, 0f), new Vector2(thickness, 0f));
        SetCraftingRecipeFrameBar(rowView, 3, frameColor, new Vector2(-thickness, 0f), new Vector2(0f, 0f));
    }

    void SetCraftingRecipeFrameBar(CraftingRecipeRowView rowView, int index, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rowView == null || index < 0)
            return;

        if (rowView.ResultFrameRects != null && index < rowView.ResultFrameRects.Length)
        {
            RectTransform rect = rowView.ResultFrameRects[index];
            if (rect != null)
            {
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
        }

        if (rowView.ResultFrameImages != null && index < rowView.ResultFrameImages.Length)
        {
            Image image = rowView.ResultFrameImages[index];
            if (image != null)
                image.color = color;
        }
    }

    bool[] ResolveMissingRecipeIngredients(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe)
    {
        if (inventory == null || recipe == null || recipe.Inputs == null)
            return Array.Empty<bool>();

        bool[] missing = new bool[recipe.Inputs.Length];
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        PlayerInventoryData normalized = inventory.Clone();
        normalized.Normalize();
        CountCombinedInventoryItems(normalized.PlayerSlots, counts);
        CountCombinedInventoryItems(normalized.ShipSlots, counts);
        CountCombinedInventoryItems(normalized.CraftingSlots, counts);

        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string itemId = recipe.Inputs[i];
            if (string.IsNullOrWhiteSpace(itemId) ||
                !counts.TryGetValue(itemId, out int available) ||
                available <= 0)
            {
                missing[i] = true;
                continue;
            }

            counts[itemId] = available - 1;
        }

        return missing;
    }

    GameObject CreateRecipeIngredientMissingFrame(Transform parent)
    {
        if (parent == null)
            return null;

        Color frameColor = new Color(1f, 0.18f, 0.18f, 0.88f);
        float thickness = 4f;
        GameObject frameRoot = new GameObject("MissingFrame", typeof(RectTransform));
        frameRoot.transform.SetParent(parent, false);
        RectTransform frameRect = frameRoot.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        void CreateBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(frameRoot.transform, false);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = bar.GetComponent<Image>();
            image.color = frameColor;
            image.raycastTarget = false;
        }

        CreateBar("MissingTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        CreateBar("MissingBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        CreateBar("MissingLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        CreateBar("MissingRight", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
        return frameRoot;
    }

    void CreateRecipeArrow(Transform parent, Vector2 anchoredPosition, bool pointLeft)
    {
        GameObject arrowRoot = new GameObject("RecipeArrow", typeof(RectTransform));
        arrowRoot.transform.SetParent(parent, false);

        RectTransform rootRect = arrowRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(82f, 56f);

        Color shadowColor = new Color(0f, 0f, 0f, 0.28f);
        Color arrowColor = new Color(0.9f, 0.94f, 0.99f, 0.98f);
        float direction = pointLeft ? -1f : 1f;

        CreateArrowSegment(arrowRoot.transform, "ShaftShadow", new Vector2(-6f * direction, -30f), new Vector2(38f, 8f), 0f, shadowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadTopShadow", new Vector2(18f * direction, -21f), new Vector2(22f, 8f), -38f * direction, shadowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadBottomShadow", new Vector2(18f * direction, -39f), new Vector2(22f, 8f), 38f * direction, shadowColor);

        CreateArrowSegment(arrowRoot.transform, "Shaft", new Vector2(-6f * direction, -28f), new Vector2(38f, 8f), 0f, arrowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadTop", new Vector2(18f * direction, -19f), new Vector2(22f, 8f), -38f * direction, arrowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadBottom", new Vector2(18f * direction, -37f), new Vector2(22f, 8f), 38f * direction, arrowColor);
    }

    void CreateArrowSegment(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float rotationZ, Color color)
    {
        GameObject segmentObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        segmentObject.transform.SetParent(parent, false);

        RectTransform rect = segmentObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        Image image = segmentObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    async void OnCraftingRecipeSelected(string recipeId)
    {
        if (inventoryActionInProgress || dragInProgress)
            return;

        PlayerProfileCraftingRecipe recipe = FindCraftingRecipe(recipeId);
        if (recipe == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        ShowItemPreview(ProfileItemSource.None, -1, recipe.OutputItemId);

        if (!IsCraftingRecipeUnlocked(recipe))
        {
            SetStatus("Blueprint required for " + InventoryItemCatalog.GetDisplayName(recipe.OutputItemId) + ".");
            return;
        }

        PlayerInventoryData workingInventory = profile.Inventory != null ? profile.Inventory.Clone() : PlayerInventoryData.Default();
        if (!TryPrepareCraftingRecipe(workingInventory, recipe, out string failureMessage))
        {
            SetStatus(string.IsNullOrWhiteSpace(failureMessage) ? "Missing ingredients for this recipe." : failureMessage);
            return;
        }

        inventoryActionInProgress = true;
        preserveInventoryButtonVisualsDuringSave = true;
        SetInteractable(false);

        try
        {
            suppressNextProfileChangedRefresh = true;
            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            ShowItemPreview(ProfileItemSource.None, -1, recipe.OutputItemId);
            SetStatus("Crafting slots prepared for " + InventoryItemCatalog.GetDisplayName(recipe.OutputItemId) + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("Crafting recipe preparation failed: " + ex);
            SetStatus("Could not prepare crafting recipe.");
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

    PlayerProfileCraftingRecipe FindCraftingRecipe(string recipeId)
    {
        IReadOnlyList<PlayerProfileCraftingRecipe> recipes = PlayerProfileCraftingCatalog.GetAllRecipes();
        if (recipes == null || string.IsNullOrWhiteSpace(recipeId))
            return null;

        for (int i = 0; i < recipes.Count; i++)
        {
            if (recipes[i] != null && string.Equals(recipes[i].Id, recipeId, StringComparison.Ordinal))
                return recipes[i];
        }

        return null;
    }

    bool CanPrepareCraftingRecipe(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe)
    {
        if (inventory == null || recipe == null || recipe.Inputs == null || recipe.Inputs.Length == 0)
            return false;

        if (!IsCraftingRecipeUnlocked(recipe))
            return false;

        if (recipe.Inputs.Length > PlayerInventoryData.CraftingSlotCount)
            return false;

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        inventory.Normalize();

        CountCombinedInventoryItems(inventory.PlayerSlots, counts);
        CountCombinedInventoryItems(inventory.ShipSlots, counts);
        CountCombinedInventoryItems(inventory.CraftingSlots, counts);

        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string itemId = recipe.Inputs[i];
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            if (!counts.TryGetValue(itemId, out int currentCount) || currentCount <= 0)
                return false;

            counts[itemId] = currentCount - 1;
        }

        return true;
    }

    void CountCombinedInventoryItems(string[] slots, Dictionary<string, int> counts)
    {
        if (slots == null || counts == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            string itemId = slots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int currentCount);
            counts[itemId] = currentCount + 1;
        }
    }

    bool TryPrepareCraftingRecipe(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe, out string failureMessage)
    {
        failureMessage = null;

        if (inventory == null || recipe == null || recipe.Inputs == null || recipe.Inputs.Length == 0)
        {
            failureMessage = "Crafting recipe is unavailable.";
            return false;
        }

        if (recipe.Inputs.Length > PlayerInventoryData.CraftingSlotCount)
        {
            failureMessage = "Recipe uses more crafting slots than are available.";
            return false;
        }

        inventory.Normalize();

        List<(bool isPlayerInventory, int slotIndex, string itemId)> sourcePlan = new List<(bool, int, string)>();
        if (!TryBuildCraftingSourcePlan(inventory, recipe, sourcePlan))
        {
            failureMessage = "Missing ingredients in player, ship or crafting inventory.";
            return false;
        }

        for (int i = 0; i < sourcePlan.Count; i++)
        {
            var entry = sourcePlan[i];
            string removed;
            if (entry.isPlayerInventory)
            {
                removed = inventory.RemoveFromPlayer(entry.slotIndex);
            }
            else if (entry.slotIndex < 0)
            {
                removed = inventory.RemoveFromCrafting(~entry.slotIndex);
            }
            else
            {
                removed = inventory.RemoveFromShip(entry.slotIndex);
            }

            if (string.IsNullOrWhiteSpace(removed))
            {
                failureMessage = "Could not reserve ingredients for crafting.";
                return false;
            }
        }

        List<string> previousCraftingItems = new List<string>(PlayerInventoryData.CraftingSlotCount);
        for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
        {
            string itemId = inventory.RemoveFromCrafting(i);
            if (!string.IsNullOrWhiteSpace(itemId))
                previousCraftingItems.Add(itemId);
        }

        for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
            inventory.SetCrafting(i, i < recipe.Inputs.Length ? recipe.Inputs[i] : null);

        int shipCapacity = GetActiveShipInventoryCapacity();
        for (int i = 0; i < previousCraftingItems.Count; i++)
        {
            string itemId = previousCraftingItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (inventory.TryAddToPlayer(itemId))
                continue;

            if (inventory.TryAddToShip(itemId, shipCapacity, GetActiveProfileShipSkinIndex()))
                continue;

            failureMessage = "Not enough free inventory space to clear current crafting slots.";
            return false;
        }

        return true;
    }

    bool TryBuildCraftingSourcePlan(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe, List<(bool isPlayerInventory, int slotIndex, string itemId)> sourcePlan)
    {
        if (inventory == null || recipe == null || recipe.Inputs == null || sourcePlan == null)
            return false;

        bool[] usedPlayerSlots = new bool[inventory.PlayerSlots.Length];
        bool[] usedShipSlots = new bool[inventory.ShipSlots.Length];
        bool[] usedCraftingSlots = new bool[inventory.CraftingSlots.Length];

        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string requiredItemId = recipe.Inputs[i];
            if (string.IsNullOrWhiteSpace(requiredItemId))
                return false;

            bool found = false;

            for (int slotIndex = 0; slotIndex < inventory.PlayerSlots.Length; slotIndex++)
            {
                if (usedPlayerSlots[slotIndex] || !string.Equals(inventory.PlayerSlots[slotIndex], requiredItemId, StringComparison.Ordinal))
                    continue;

                usedPlayerSlots[slotIndex] = true;
                sourcePlan.Add((true, slotIndex, requiredItemId));
                found = true;
                break;
            }

            if (found)
                continue;

            int shipCapacity = GetActiveShipInventoryCapacity();
            for (int slotIndex = 0; slotIndex < inventory.ShipSlots.Length && slotIndex < shipCapacity; slotIndex++)
            {
                if (usedShipSlots[slotIndex] || !string.Equals(inventory.ShipSlots[slotIndex], requiredItemId, StringComparison.Ordinal))
                    continue;

                usedShipSlots[slotIndex] = true;
                sourcePlan.Add((false, slotIndex, requiredItemId));
                found = true;
                break;
            }

            if (found)
                continue;

            for (int slotIndex = 0; slotIndex < inventory.CraftingSlots.Length; slotIndex++)
            {
                if (usedCraftingSlots[slotIndex] || !string.Equals(inventory.CraftingSlots[slotIndex], requiredItemId, StringComparison.Ordinal))
                    continue;

                usedCraftingSlots[slotIndex] = true;
                sourcePlan.Add((false, ~slotIndex, requiredItemId));
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }
}
