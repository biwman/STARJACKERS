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
    Button CreateEquipmentSlotButton(Transform parent, string name, Vector2 anchoredPosition, int slotIndex, string label, out TMP_Text text, out Image icon)
    {
        GameObject slotObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        slotObject.transform.SetParent(parent, false);
        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(120f, 120f);

        Image bg = slotObject.GetComponent<Image>();
        bg.color = GetShipSelectionSlotColor(slotIndex);
        bg.raycastTarget = true;

        Outline outline = slotObject.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = GetShipSelectionSlotOutlineColor(slotIndex);
            outline.effectDistance = new Vector2(2.2f, -2.2f);
            outline.useGraphicAlpha = true;
        }

        Button button = slotObject.GetComponent<Button>();
        ConfigureInventorySlotButtonTransition(button);
        button.onClick.AddListener(() => OnEquipmentSlotClicked(slotIndex));

        ProfileEquipmentSlotDragHandler dragHandler = slotObject.AddComponent<ProfileEquipmentSlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.slotIndex = slotIndex;

        GameObject iconObject = new GameObject(name + "Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(EquipmentSlotPreviewIconSize, EquipmentSlotPreviewIconSize);
        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;

        text = CreateText(slotObject.transform, name + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, EquipmentSlotPreviewFontSize, TextAlignmentOptions.Center);
        ApplyEquipmentSlotPreviewTextStyle(text);
        icon.transform.SetAsLastSibling();
        return button;
    }

    void CreateInventoryGrid(Transform parent, bool isPlayerInventory, Vector2 startPosition, int slotCount, int columns, out Button[] buttons, out TMP_Text[] labels, out Image[] icons)
    {
        buttons = new Button[slotCount];
        labels = new TMP_Text[slotCount];
        icons = new Image[slotCount];

        const float slotSize = 120f;
        const float slotSpacing = 12f;

        for (int index = 0; index < slotCount; index++)
        {
            int row = index / columns;
            int col = index % columns;
            Vector2 position = new Vector2(
                startPosition.x + col * (slotSize + slotSpacing),
                startPosition.y - row * (slotSize + slotSpacing));

            buttons[index] = CreateInventorySlot(parent, (isPlayerInventory ? "PlayerSlot" : "ShipSlot") + index, position, new Vector2(slotSize, slotSize), isPlayerInventory, index, out labels[index], out icons[index]);
        }
    }

    void RebuildPlayerInventoryGrid(int slotCount)
    {
        if (panelObject == null)
            return;

        int safeSlotCount = Mathf.Max(PlayerInventoryData.DefaultPlayerSlotCount, slotCount);
        if (builtPlayerInventorySlotCount == safeSlotCount && playerInventoryButtons != null)
            return;

        if (playerInventoryScrollRect != null)
        {
            Destroy(playerInventoryScrollRect.gameObject);
            playerInventoryScrollRect = null;
            playerInventoryContentRect = null;
        }

        if (playerInventoryScrollbarObject != null)
        {
            Destroy(playerInventoryScrollbarObject);
            playerInventoryScrollbarObject = null;
        }

        CreateScrollablePlayerInventoryGrid(
            panelObject.transform,
            PlayerInventoryGridPosition,
            PlayerInventoryViewportSize,
            safeSlotCount,
            PlayerInventoryGridColumns,
            out playerInventoryButtons,
            out playerInventoryTexts,
            out playerInventoryIcons);
        PlacePlayerInventoryGridInHierarchy();
        builtPlayerInventorySlotCount = safeSlotCount;
    }

    void PlacePlayerInventoryGridInHierarchy()
    {
        if (itemPreviewPanelObject == null || playerInventoryScrollRect == null)
            return;

        int targetIndex = itemPreviewPanelObject.transform.GetSiblingIndex();
        playerInventoryScrollRect.transform.SetSiblingIndex(targetIndex);
        if (playerInventoryScrollbarObject != null)
            playerInventoryScrollbarObject.transform.SetSiblingIndex(targetIndex + 1);
    }

    void CreateScrollablePlayerInventoryGrid(Transform parent, Vector2 anchoredPosition, Vector2 viewportSize, int slotCount, int columns, out Button[] buttons, out TMP_Text[] labels, out Image[] icons)
    {
        buttons = new Button[slotCount];
        labels = new TMP_Text[slotCount];
        icons = new Image[slotCount];

        const float slotSize = 120f;
        const float slotSpacing = 12f;
        int rows = Mathf.CeilToInt(slotCount / (float)columns);

        GameObject viewportObject = new GameObject("PlayerInventoryViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(parent, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 1f);
        viewportRect.anchorMax = new Vector2(0.5f, 1f);
        viewportRect.pivot = new Vector2(0f, 1f);
        viewportRect.anchoredPosition = anchoredPosition;
        viewportRect.sizeDelta = viewportSize;

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.08f, 0.11f, 0.15f, 0.26f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("PlayerInventoryContent", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        playerInventoryContentRect = contentObject.GetComponent<RectTransform>();
        playerInventoryContentRect.anchorMin = new Vector2(0f, 1f);
        playerInventoryContentRect.anchorMax = new Vector2(0f, 1f);
        playerInventoryContentRect.pivot = new Vector2(0f, 1f);
        playerInventoryContentRect.anchoredPosition = Vector2.zero;
        playerInventoryContentRect.sizeDelta = new Vector2(
            columns * slotSize + (columns - 1) * slotSpacing,
            rows * slotSize + (rows - 1) * slotSpacing);

        GameObject scrollbarObject = new GameObject("PlayerInventoryScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(parent, false);
        playerInventoryScrollbarObject = scrollbarObject;
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.5f, 1f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 1f);
        scrollbarRect.pivot = new Vector2(0f, 1f);
        scrollbarRect.anchoredPosition = new Vector2(anchoredPosition.x - 100f, anchoredPosition.y);
        scrollbarRect.sizeDelta = new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), viewportSize.y);

        Scrollbar scrollbar = RuntimeScrollbarStyler.ApplyVertical(scrollbarObject, RuntimeScrollbarStyler.Size.Small, RuntimeScrollbarStyler.Tone.Mint);

        playerInventoryScrollRect = viewportObject.GetComponent<ScrollRect>();
        playerInventoryScrollRect.horizontal = false;
        playerInventoryScrollRect.vertical = true;
        playerInventoryScrollRect.movementType = ScrollRect.MovementType.Clamped;
        playerInventoryScrollRect.viewport = viewportRect;
        playerInventoryScrollRect.content = playerInventoryContentRect;
        playerInventoryScrollRect.verticalScrollbar = scrollbar;
        playerInventoryScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        playerInventoryScrollRect.scrollSensitivity = 32f;

        for (int index = 0; index < slotCount; index++)
        {
            int row = index / columns;
            int col = index % columns;
            Vector2 position = new Vector2(col * (slotSize + slotSpacing), -row * (slotSize + slotSpacing));
            buttons[index] = CreateInventorySlotTopLeft(
                playerInventoryContentRect,
                "PlayerSlot" + index,
                position,
                new Vector2(slotSize, slotSize),
                true,
                index,
                out labels[index],
                out icons[index]);
        }
    }

    Button CreateInventorySlot(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, bool isPlayerInventory, int slotIndex, out TMP_Text label, out Image icon)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.21f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        ConfigureInventorySlotButtonTransition(button);
        button.onClick.AddListener(() => OnInventorySlotClicked(isPlayerInventory, slotIndex));

        ProfileInventorySlotDragHandler dragHandler = buttonObject.AddComponent<ProfileInventorySlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.isPlayerInventory = isPlayerInventory;
        dragHandler.slotIndex = slotIndex;

        GameObject iconObject = new GameObject(objectName + "Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(86f, 86f);

        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;

        label = CreateText(buttonObject.transform, objectName + "Text", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22f, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.margin = new Vector4(6f, 6f, 6f, 6f);

        return button;
    }

    Button CreateInventorySlotTopLeft(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, bool isPlayerInventory, int slotIndex, out TMP_Text label, out Image icon)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.21f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        ConfigureInventorySlotButtonTransition(button);
        button.onClick.AddListener(() => OnInventorySlotClicked(isPlayerInventory, slotIndex));

        ProfileInventorySlotDragHandler dragHandler = buttonObject.AddComponent<ProfileInventorySlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.isPlayerInventory = isPlayerInventory;
        dragHandler.slotIndex = slotIndex;

        GameObject iconObject = new GameObject(objectName + "Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(86f, 86f);

        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;

        label = CreateText(buttonObject.transform, objectName + "Text", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22f, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.margin = new Vector4(6f, 6f, 6f, 6f);

        return button;
    }

    void ConfigureInventorySlotButtonTransition(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.fadeDuration = 0f;
        button.colors = colors;
    }

    void ConfigureNoBlinkInventoryActionButton(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.fadeDuration = 0f;
        button.colors = colors;
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, Action onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.27f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            if (!ButtonClickSoundHook.ShouldSuppressClickSound(objectName, label))
                AudioManager.Instance?.PlayClick();
            onClick?.Invoke();
        });

        TMP_Text text = CreateText(buttonObject.transform, objectName + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        if (objectName.StartsWith("ShipSkinButton", StringComparison.Ordinal))
        {
            text.fontSize = 15f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = new Vector4(6f, 4f, 6f, 4f);
        }

        return button;
    }

    void StyleButton(Button button, Color normalColor, Color highlightedColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = normalColor;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.selectedColor = highlightedColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = Color.Lerp(highlightedColor, Color.black, 0.15f);
        colors.disabledColor = new Color(0.26f, 0.28f, 0.31f, 0.8f);
        button.colors = colors;
    }

    void StyleExitGameButton()
    {
        if (exitGameButton == null)
            return;

        Color normal = new Color(0.5f, 0.08f, 0.1f, 0.98f);
        Color highlighted = new Color(0.72f, 0.13f, 0.16f, 1f);
        StyleButton(exitGameButton, normal, highlighted);

        Outline outline = exitGameButton.GetComponent<Outline>();
        if (outline == null)
            outline = exitGameButton.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.Lerp(highlighted, Color.white, 0.25f) * new Color(1f, 1f, 1f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
    }

    void StyleCompactInventoryUtilityButton(Button button)
    {
        if (button == null)
            return;

        StyleButton(button, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.14f, 0.19f, 0.28f, 0.98f);
            image.raycastTarget = true;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.56f, 0.68f, 0.82f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        Shadow shadow = null;
        Shadow[] shadows = button.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && shadows[i] is not Outline)
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(3f, -3f);
        shadow.useGraphicAlpha = true;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.fontSize = 17f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.92f, 0.98f, 1f, 1f);
            text.margin = new Vector4(8f, 4f, 8f, 4f);
        }
    }

    void StyleCompactBackLikeButton(Button button)
    {
        if (button == null)
            return;

        Color normal = new Color(0.14f, 0.19f, 0.28f, 0.98f);
        Color highlighted = new Color(0.22f, 0.3f, 0.42f, 1f);
        Color pressed = new Color(0.1f, 0.14f, 0.2f, 1f);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = normal;
            image.raycastTarget = true;
        }

        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.selectedColor = highlighted;
        colors.pressedColor = pressed;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f);
        colors.fadeDuration = 0f;
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.34f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        Shadow shadow = null;
        Shadow[] shadows = button.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && shadows[i] is not Outline)
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
        shadow.effectDistance = new Vector2(3f, -3f);
        shadow.useGraphicAlpha = true;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.fontSize = 16f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.94f, 0.97f, 1f, 1f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = 1.6f;
            text.margin = new Vector4(8f, 4f, 8f, 4f);
        }
    }

    void StyleReadableBackLikeButton(Button button, float fontSize)
    {
        StyleCompactBackLikeButton(button);

        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, fontSize - 3f);
        text.fontSizeMax = fontSize;
        text.characterSpacing = 1.2f;
        text.margin = new Vector4(10f, 5f, 10f, 5f);
        text.overflowMode = TextOverflowModes.Truncate;
    }

    void StylePlayerInventoryUtilityButton(Button button)
    {
        StyleCompactBackLikeButton(button);

        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null)
            text.fontSize = PlayerInventoryUtilityButtonFontSize;
    }

    void ConfigureShipInventoryLabelText()
    {
        if (shipInventoryLabelText == null)
            return;

        shipInventoryLabelText.fontSize = ShipInventoryHeaderFontSize;
        shipInventoryLabelText.enableAutoSizing = true;
        shipInventoryLabelText.fontSizeMin = 18f;
        shipInventoryLabelText.fontSizeMax = ShipInventoryHeaderFontSize;
        shipInventoryLabelText.fontStyle = FontStyles.Bold;
        shipInventoryLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        shipInventoryLabelText.textWrappingMode = TextWrappingModes.NoWrap;
        shipInventoryLabelText.overflowMode = TextOverflowModes.Truncate;
        shipInventoryLabelText.characterSpacing = 0.6f;
        shipInventoryLabelText.margin = new Vector4(0f, 2f, 4f, 2f);
    }

    void ConfigureBlueprintsTabButtonText()
    {
        TMP_Text text = craftingRecipeBlueprintsButton != null
            ? craftingRecipeBlueprintsButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (text == null)
            return;

        text.text = "BLUEPRINTS";
        text.fontSize = BlueprintTabFontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = BlueprintTabFontSize;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.characterSpacing = 1.2f;
        text.margin = new Vector4(12f, 5f, 12f, 5f);
    }

    void ConfigurePlayerInventoryLabelText()
    {
        if (playerInventoryLabelText == null)
            return;

        playerInventoryLabelText.text = PlayerInventoryTitleText;
        playerInventoryLabelText.fontSize = 18f;
        playerInventoryLabelText.enableAutoSizing = true;
        playerInventoryLabelText.fontSizeMin = 12f;
        playerInventoryLabelText.fontSizeMax = 18f;
        playerInventoryLabelText.fontStyle = FontStyles.Bold;
        playerInventoryLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        playerInventoryLabelText.textWrappingMode = TextWrappingModes.NoWrap;
        playerInventoryLabelText.overflowMode = TextOverflowModes.Truncate;
        playerInventoryLabelText.margin = new Vector4(0f, 2f, 4f, 2f);
    }

    void ConfigurePlayerInventoryCountText()
    {
        if (playerInventoryCountText == null)
            return;

        playerInventoryCountText.fontSize = 20f;
        playerInventoryCountText.enableAutoSizing = true;
        playerInventoryCountText.fontSizeMin = 14f;
        playerInventoryCountText.fontSizeMax = 20f;
        playerInventoryCountText.fontStyle = FontStyles.Bold;
        playerInventoryCountText.alignment = TextAlignmentOptions.MidlineRight;
        playerInventoryCountText.textWrappingMode = TextWrappingModes.NoWrap;
        playerInventoryCountText.overflowMode = TextOverflowModes.Truncate;
        playerInventoryCountText.color = new Color(0.94f, 0.84f, 0.44f, 1f);
        playerInventoryCountText.margin = new Vector4(2f, 2f, 0f, 2f);
    }

    void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label ?? string.Empty;
    }

    TMP_Text CreateText(Transform parent, string objectName, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.offsetMin = sizeDelta == Vector2.zero ? Vector2.zero : rect.offsetMin;
        rect.offsetMax = sizeDelta == Vector2.zero ? Vector2.zero : rect.offsetMax;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;

        TMP_Text reference = FindAnyObjectByType<TextMeshProUGUI>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }
}
