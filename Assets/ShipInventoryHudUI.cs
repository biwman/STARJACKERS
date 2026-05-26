using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(PhotonView))]
public class ShipInventoryHudUI : MonoBehaviourPun
{
    const string InventoryButtonName = "ShipInventoryButton";
    const string InventoryPanelName = "ShipInventoryPanel";
    const string DragVisualName = "ShipInventoryDragVisual";
    const float CargoButtonWidth = 96f;
    const float CargoButtonHitboxWidth = CargoButtonWidth;
    const float CargoButtonHeight = 244f;
    const float CargoButtonPanelGap = 12f;
    const float CargoButtonLeftInset = 4f;
    const float CargoButtonTopInset = 270f;
    const float CargoSlotSize = 99f;
    const float CargoSlotSpacing = 10f;
    const int CargoSlotColumns = 5;
    const int CargoSlotRows = 2;
    const float CargoPanelWidth = 5f * CargoSlotSize + 4f * CargoSlotSpacing;
    const float CargoPanelHeight = 2f * CargoSlotSize + CargoSlotSpacing;
    const float CargoSlotIconSize = 74f;

    GameObject buttonObject;
    Button inventoryButton;
    Image buttonImage;
    TextMeshProUGUI buttonText;
    GameObject panelObject;
    RectTransform buttonRect;
    RectTransform cargoVisualRect;
    RectTransform panelRect;
    RectTransform canvasRect;
    Button[] slotButtons;
    Image[] slotIcons;
    TMP_Text[] slotLabels;
    GameObject dragVisualObject;
    Image dragVisualIcon;
    TMP_Text dragVisualLabel;
    bool panelVisible;
    bool hudVisible = true;
    bool previousHudVisible = true;
    bool dragInProgress;
    int draggedSlotIndex = -1;
    int observedInventoryRevision = -1;
    bool forceUiRefresh = true;
    float nextLayoutRefresh;

    void Start()
    {
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateUi();
        RefreshUi();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextLayoutRefresh)
        {
            nextLayoutRefresh = Time.unscaledTime + 0.25f;
            RefreshRuntimeLayout();
        }

        bool visibilityChanged = UpdateVisibility();
        RefreshUiIfNeeded(visibilityChanged);
    }

    void OnDestroy()
    {
        if (buttonObject != null)
            Destroy(buttonObject);

        if (panelObject != null)
            Destroy(panelObject);

        if (dragVisualObject != null)
            Destroy(dragVisualObject);
    }

    void CreateUi()
    {
        GameObject existingButton = GameObject.Find(InventoryButtonName);
        if (existingButton != null)
            Destroy(existingButton);

        GameObject existingPanel = GameObject.Find(InventoryPanelName);
        if (existingPanel != null)
            Destroy(existingPanel);

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        canvasRect = canvas.GetComponent<RectTransform>();
        CreateButton(canvas.transform);
        CreatePanel(canvas.transform);
        CreateDragVisual(canvas.transform);
    }

    void CreateButton(Transform parent)
    {
        buttonObject = new GameObject(InventoryButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.sizeDelta = new Vector2(CargoButtonHitboxWidth, CargoButtonHeight);
        buttonRect.anchoredPosition = GetCargoButtonPosition();

        buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0f, 0f, 0f, 0f);
        buttonImage.type = Image.Type.Simple;
        buttonImage.raycastTarget = true;

        GameObject visualObject = new GameObject("CargoButtonVisual", typeof(RectTransform), typeof(Image));
        visualObject.transform.SetParent(buttonObject.transform, false);

        cargoVisualRect = visualObject.GetComponent<RectTransform>();
        cargoVisualRect.anchorMin = new Vector2(0f, 1f);
        cargoVisualRect.anchorMax = new Vector2(0f, 1f);
        cargoVisualRect.pivot = new Vector2(0f, 1f);
        cargoVisualRect.anchoredPosition = Vector2.zero;
        cargoVisualRect.sizeDelta = new Vector2(CargoButtonWidth, CargoButtonHeight);

        Image visualImage = visualObject.GetComponent<Image>();
        visualImage.color = new Color(0.035f, 0.07f, 0.1f, 0.82f);
        visualImage.type = Image.Type.Simple;
        visualImage.raycastTarget = false;

        Outline buttonOutline = visualObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(0.36f, 0.48f, 0.56f, 0.78f);
        buttonOutline.effectDistance = new Vector2(2f, 2f);
        buttonOutline.useGraphicAlpha = false;

        Shadow buttonShadow = visualObject.AddComponent<Shadow>();
        buttonShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        buttonShadow.effectDistance = new Vector2(3f, -3f);
        buttonShadow.useGraphicAlpha = false;

        inventoryButton = buttonObject.GetComponent<Button>();
        inventoryButton.onClick.AddListener(TogglePanel);
        inventoryButton.transition = Selectable.Transition.None;

        GameObject textObject = new GameObject("ShipInventoryButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(visualObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(3f, 6f);
        textRect.offsetMax = new Vector2(-3f, -6f);

        buttonText = textObject.GetComponent<TextMeshProUGUI>();
        buttonText.text = "CARGO";
        buttonText.fontSize = 14f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.textWrappingMode = TextWrappingModes.NoWrap;
        buttonText.richText = true;
        buttonText.lineSpacing = -10f;
        buttonText.characterSpacing = 1f;
        buttonText.color = new Color(0.88f, 0.96f, 1f, 0.92f);
        buttonText.raycastTarget = false;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            buttonText.font = reference.font;
            buttonText.fontSharedMaterial = reference.fontSharedMaterial;
        }
    }

    void CreatePanel(Transform parent)
    {
        panelObject = new GameObject(InventoryPanelName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(CargoPanelWidth, CargoPanelHeight);
        panelRect.anchoredPosition = GetCargoPanelPosition();

        Image bg = panelObject.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;

        slotButtons = new Button[PlayerInventoryData.ShipSlotCount];
        slotIcons = new Image[PlayerInventoryData.ShipSlotCount];
        slotLabels = new TMP_Text[PlayerInventoryData.ShipSlotCount];

        Vector2 start = Vector2.zero;

        for (int row = 0; row < CargoSlotRows; row++)
        {
            for (int col = 0; col < CargoSlotColumns; col++)
            {
                int index = row * CargoSlotColumns + col;
                Vector2 pos = new Vector2(start.x + col * (CargoSlotSize + CargoSlotSpacing), start.y - row * (CargoSlotSize + CargoSlotSpacing));
                CreateSlot(index, pos, new Vector2(CargoSlotSize, CargoSlotSize));
            }
        }

        panelVisible = false;
        panelObject.SetActive(false);
    }

    void CreateSlot(int index, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject slotObject = new GameObject("ShipInventoryHudSlot" + index, typeof(RectTransform), typeof(Image), typeof(Button));
        slotObject.transform.SetParent(panelObject.transform, false);

        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = slotObject.GetComponent<Image>();
        image.color = new Color(0.11f, 0.16f, 0.22f, 0.98f);

        Button button = slotObject.GetComponent<Button>();
        button.interactable = true;
        button.transition = Selectable.Transition.None;
        slotButtons[index] = button;

        ShipInventoryHudSlotDragHandler dragHandler = slotObject.AddComponent<ShipInventoryHudSlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.slotIndex = index;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(CargoSlotIconSize, CargoSlotIconSize);

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;
        slotIcons[index] = icon;

        slotLabels[index] = CreateLabel(slotObject.transform, "Label", string.Empty, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, 16f);
        RectTransform labelRect = slotLabels[index].GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        slotLabels[index].margin = new Vector4(4f, 4f, 4f, 4f);
    }

    TextMeshProUGUI CreateLabel(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = new Color(0.95f, 0.98f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }

    void CreateDragVisual(Transform parent)
    {
        GameObject existing = GameObject.Find(DragVisualName);
        if (existing != null)
            Destroy(existing);

        dragVisualObject = new GameObject(DragVisualName, typeof(RectTransform), typeof(Image));
        dragVisualObject.transform.SetParent(parent, false);

        RectTransform rect = dragVisualObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(CargoSlotSize, CargoSlotSize);

        Image bg = dragVisualObject.GetComponent<Image>();
        bg.color = new Color(0.14f, 0.22f, 0.31f, 0.9f);
        bg.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(dragVisualObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(CargoSlotIconSize, CargoSlotIconSize);
        iconRect.anchoredPosition = Vector2.zero;

        dragVisualIcon = iconObject.GetComponent<Image>();
        dragVisualIcon.preserveAspect = true;
        dragVisualIcon.raycastTarget = false;

        dragVisualLabel = CreateLabel(dragVisualObject.transform, "Label", string.Empty, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, 14f);
        RectTransform labelRect = dragVisualLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        dragVisualLabel.margin = new Vector4(3f, 3f, 3f, 3f);

        dragVisualObject.SetActive(false);
    }

    void TogglePanel()
    {
        panelVisible = !panelVisible;
        if (panelObject != null)
            panelObject.SetActive(panelVisible && hudVisible);

        forceUiRefresh = true;
    }

    void RefreshUiIfNeeded(bool visibilityChanged)
    {
        int revision = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.InventoryRevision : -1;
        if (!forceUiRefresh && !visibilityChanged && revision == observedInventoryRevision)
            return;

        RefreshUi();
        observedInventoryRevision = revision;
        forceUiRefresh = false;
    }

    void RefreshUi()
    {
        if (slotButtons == null || slotIcons == null || slotLabels == null)
            return;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile != null
            ? PlayerProfileService.Instance.CurrentProfile.Inventory
            : null;
        PlayerInventoryData normalized = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        normalized.Normalize();
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, 0);
        int shipCapacity = PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, normalized.EquipmentSlots);

        int filledSlots = 0;

        for (int i = 0; i < normalized.ShipSlots.Length && i < slotButtons.Length; i++)
        {
            bool slotEnabled = i < shipCapacity;
            bool isSafePocket = slotEnabled && PlayerProfileService.IsSafePocketIndex(shipSkinIndex, i);
            bool isAstronautSlot = slotEnabled && PlayerProfileService.IsAstronautCargoIndex(shipSkinIndex, shipCapacity, i);
            string itemId = normalized.ShipSlots[i];
            bool occupied = slotEnabled && !string.IsNullOrWhiteSpace(itemId);
            Sprite icon = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;
            Image slotImage = slotButtons[i] != null ? slotButtons[i].GetComponent<Image>() : null;

            if (occupied)
                filledSlots++;

            if (slotButtons[i] != null)
                slotButtons[i].gameObject.SetActive(slotEnabled);

            if (slotImage != null)
            {
                if (occupied)
                {
                    Color baseColor = InventoryItemCatalog.GetRarityColor(itemId);
                    if (isSafePocket)
                        slotImage.color = Color.Lerp(baseColor, new Color(0.24f, 0.74f, 0.66f, baseColor.a), 0.32f);
                    else if (isAstronautSlot)
                        slotImage.color = Color.Lerp(baseColor, new Color(1f, 0.67f, 0.28f, baseColor.a), 0.28f);
                    else
                        slotImage.color = baseColor;
                }
                else
                {
                    if (isSafePocket)
                        slotImage.color = new Color(0.09f, 0.23f, 0.22f, 0.98f);
                    else if (isAstronautSlot)
                        slotImage.color = new Color(0.28f, 0.18f, 0.08f, 0.98f);
                    else
                        slotImage.color = new Color(0.11f, 0.16f, 0.22f, 0.98f);
                }
            }

            if (slotIcons[i] != null)
            {
                slotIcons[i].sprite = icon;
                slotIcons[i].enabled = occupied && icon != null;
            }

            if (slotLabels[i] != null)
            {
                bool showText = occupied && icon == null;
                if (showText)
                {
                    slotLabels[i].text = InventoryItemCatalog.GetShortLabel(itemId);
                    slotLabels[i].color = Color.white;
                }
                else if (isSafePocket)
                {
                    slotLabels[i].text = "SAFE";
                    slotLabels[i].color = occupied ? new Color(0f, 0f, 0f, 0f) : new Color(0.56f, 1f, 0.95f, 0.86f);
                }
                else if (isAstronautSlot)
                {
                    slotLabels[i].text = "ASTRO";
                    slotLabels[i].color = occupied ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 0.79f, 0.42f, 0.88f);
                }
                else
                {
                    slotLabels[i].text = string.Empty;
                    slotLabels[i].color = new Color(0f, 0f, 0f, 0f);
                }
            }

            if (slotButtons[i] != null)
            {
                Outline outline = slotButtons[i].GetComponent<Outline>();
                if (isSafePocket)
                {
                    if (outline == null)
                        outline = slotButtons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.38f, 0.98f, 0.88f, 0.95f);
                    outline.effectDistance = new Vector2(2f, 2f);
                    outline.enabled = true;
                }
                else if (isAstronautSlot)
                {
                    if (outline == null)
                        outline = slotButtons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1f, 0.64f, 0.22f, 0.95f);
                    outline.effectDistance = new Vector2(2f, 2f);
                    outline.enabled = true;
                }
                else if (outline != null)
                {
                    outline.enabled = false;
                }
            }
        }

        if (buttonText != null)
        {
            buttonText.text = "C\nA\nR\nG\nO\n<size=24>" + FormatVerticalCount(filledSlots, shipCapacity) + "</size>";
            buttonText.fontSize = 14f;
        }
    }

    string FormatVerticalCount(int filledSlots, int shipCapacity)
    {
        return filledSlots + "\n-\n" + shipCapacity;
    }

    public void BeginSlotDrag(int index, PointerEventData eventData)
    {
        if (!hudVisible || !panelVisible)
            return;

        string itemId = GetShipItemAt(index);
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        dragInProgress = true;
        draggedSlotIndex = index;
        UpdateDragVisual(itemId, eventData);
    }

    public void UpdateSlotDrag(int index, PointerEventData eventData)
    {
        if (!dragInProgress || draggedSlotIndex != index)
            return;

        string itemId = GetShipItemAt(index);
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        UpdateDragVisual(itemId, eventData);
    }

    public void EndSlotDrag(int index, PointerEventData eventData)
    {
        if (!dragInProgress || draggedSlotIndex != index)
            return;

        bool shouldDrop = ShouldDropToSpace(eventData);
        int targetSlotIndex = shouldDrop ? -1 : GetSlotIndexAtScreenPosition(eventData);
        HideDragVisual();
        dragInProgress = false;
        draggedSlotIndex = -1;

        if (shouldDrop)
        {
            DropShipItem(index);
            return;
        }

        if (targetSlotIndex >= 0 && targetSlotIndex != index)
        {
            MoveShipItemBetweenSlots(index, targetSlotIndex);
        }
    }

    string GetShipItemAt(int index)
    {
        if (PlayerProfileService.Instance.CurrentProfile == null ||
            PlayerProfileService.Instance.CurrentProfile.Inventory == null)
            return null;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile.Inventory;
        inventory.Normalize();

        if (index < 0 || index >= inventory.ShipSlots.Length)
            return null;

        return inventory.ShipSlots[index];
    }

    void UpdateDragVisual(string itemId, PointerEventData eventData)
    {
        if (dragVisualObject == null || canvasRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            RectTransform rect = dragVisualObject.GetComponent<RectTransform>();
            rect.anchoredPosition = localPoint;
        }

        Sprite icon = InventoryItemCatalog.GetIcon(itemId);
        if (dragVisualIcon != null)
        {
            dragVisualIcon.sprite = icon;
            dragVisualIcon.enabled = icon != null;
        }

        if (dragVisualLabel != null)
        {
            bool showLabel = icon == null;
            dragVisualLabel.text = showLabel ? InventoryItemCatalog.GetShortLabel(itemId) : string.Empty;
            dragVisualLabel.color = showLabel ? Color.white : new Color(0f, 0f, 0f, 0f);
        }

        dragVisualObject.SetActive(true);
    }

    bool ShouldDropToSpace(PointerEventData eventData)
    {
        if (eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera;
        RectTransform panelRect = panelObject != null ? panelObject.GetComponent<RectTransform>() : null;
        RectTransform buttonRect = buttonObject != null ? buttonObject.GetComponent<RectTransform>() : null;

        bool overPanel = panelRect != null && RectTransformUtility.RectangleContainsScreenPoint(panelRect, eventData.position, eventCamera);
        bool overButton = buttonRect != null && RectTransformUtility.RectangleContainsScreenPoint(buttonRect, eventData.position, eventCamera);

        return !overPanel && !overButton;
    }

    int GetSlotIndexAtScreenPosition(PointerEventData eventData)
    {
        if (eventData == null || slotButtons == null)
            return -1;

        Camera eventCamera = eventData.pressEventCamera;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, 0);
        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile != null
            ? PlayerProfileService.Instance.CurrentProfile.Inventory
            : null;
        string[] equipmentSlots = inventory != null ? inventory.EquipmentSlots : null;
        int shipCapacity = PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, equipmentSlots);

        for (int i = 0; i < slotButtons.Length && i < shipCapacity; i++)
        {
            if (slotButtons[i] == null)
                continue;

            RectTransform rect = slotButtons[i].GetComponent<RectTransform>();
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventCamera))
                return i;
        }

        return -1;
    }

    async void DropShipItem(int index)
    {
        string removedItem = null;

        try
        {
            removedItem = await PlayerProfileService.Instance.RemoveShipItemAtAsync(index);
            if (string.IsNullOrWhiteSpace(removedItem))
                return;

            DroppedCargoManager.DropItemFromShip(removedItem, transform);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to drop ship inventory item: " + ex);

            if (!string.IsNullOrWhiteSpace(removedItem))
            {
                try
                {
                    await PlayerProfileService.Instance.RestoreShipItemAtAsync(index, removedItem);
                }
                catch (System.Exception restoreEx)
                {
                    Debug.LogError("Failed to restore dropped ship inventory item: " + restoreEx);
                }
            }
        }
    }

    async void MoveShipItemBetweenSlots(int sourceIndex, int targetIndex)
    {
        try
        {
            await PlayerProfileService.Instance.MoveShipItemWithinShipAsync(sourceIndex, targetIndex);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to move ship inventory item within ship slots: " + ex);
        }
    }

    void HideDragVisual()
    {
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);
    }

    Vector2 GetCargoButtonPosition()
    {
        return new Vector2(CargoButtonLeftInset, -CargoButtonTopInset);
    }

    Vector2 GetCargoPanelPosition()
    {
        return GetCargoButtonPosition() + new Vector2(CargoButtonHitboxWidth + CargoButtonPanelGap, 0f);
    }

    void RefreshRuntimeLayout()
    {
        if (buttonObject == null)
            return;

        if (buttonRect == null)
            return;

        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.sizeDelta = new Vector2(CargoButtonHitboxWidth, CargoButtonHeight);
        buttonRect.anchoredPosition = GetCargoButtonPosition();
        buttonObject.transform.SetAsLastSibling();

        if (cargoVisualRect != null)
        {
            cargoVisualRect.anchoredPosition = Vector2.zero;
            cargoVisualRect.sizeDelta = new Vector2(CargoButtonWidth, CargoButtonHeight);
        }

        if (panelObject == null)
            return;

        if (panelRect == null)
            return;

        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(CargoPanelWidth, CargoPanelHeight);
        panelRect.anchoredPosition = GetCargoPanelPosition();
        if (panelObject.activeSelf)
        {
            panelObject.transform.SetAsLastSibling();
            buttonObject.transform.SetAsLastSibling();
        }
    }

    bool UpdateVisibility()
    {
        bool shouldShow = IsGameplayHudVisible();
        bool changed = hudVisible != shouldShow;
        if (hudVisible != shouldShow)
        {
            previousHudVisible = hudVisible;
            hudVisible = shouldShow;
        }

        if (!previousHudVisible && hudVisible)
        {
            panelVisible = false;
        }

        previousHudVisible = hudVisible;

        if (buttonObject != null)
        {
            bool wasActive = buttonObject.activeSelf;
            buttonObject.SetActive(shouldShow);
            changed |= wasActive != shouldShow;
        }

        if (panelObject != null)
        {
            bool panelShouldShow = shouldShow && panelVisible;
            bool wasActive = panelObject.activeSelf;
            panelObject.SetActive(panelShouldShow);
            changed |= wasActive != panelShouldShow;
        }

        if (!shouldShow)
        {
            dragInProgress = false;
            draggedSlotIndex = -1;
            HideDragVisual();
        }

        return changed;
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return GameplayHudVisibility.IsGameplayHudVisible(started);
        }

        return false;
    }
}
