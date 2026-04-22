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

    GameObject buttonObject;
    Button inventoryButton;
    Image buttonImage;
    TextMeshProUGUI buttonText;
    GameObject panelObject;
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
        RefreshRuntimeLayout();
        UpdateVisibility();
        RefreshUi();
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
        RectTransform collectRect = null;
        GameObject collectButton = GameObject.Find("CollectButton");
        if (collectButton != null)
            collectRect = collectButton.GetComponent<RectTransform>();

        buttonObject = new GameObject(InventoryButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(164f, 62f);
        if (collectRect != null)
        {
            rect.anchorMin = collectRect.anchorMin;
            rect.anchorMax = collectRect.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            float verticalOffset = collectRect.rect.height * 0.5f + rect.sizeDelta.y * 0.5f + 18f;
            rect.anchoredPosition = collectRect.anchoredPosition + new Vector2(0f, verticalOffset);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(180f, 210f);
        }

        buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.38f, 0.62f, 0.95f);
        buttonImage.type = Image.Type.Sliced;

        inventoryButton = buttonObject.GetComponent<Button>();
        inventoryButton.onClick.AddListener(TogglePanel);

        GameObject textObject = new GameObject("ShipInventoryButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        buttonText = textObject.GetComponent<TextMeshProUGUI>();
        buttonText.text = "CARGO";
        buttonText.fontSize = 24f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.textWrappingMode = TextWrappingModes.NoWrap;
        buttonText.color = Color.white;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            buttonText.font = reference.font;
            buttonText.fontSharedMaterial = reference.fontSharedMaterial;
        }
    }

    void CreatePanel(Transform parent)
    {
        RectTransform buttonRect = buttonObject != null ? buttonObject.GetComponent<RectTransform>() : null;

        panelObject = new GameObject(InventoryPanelName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(330f, 170f);
        if (buttonRect != null)
        {
            rect.anchorMin = buttonRect.anchorMin;
            rect.anchorMax = buttonRect.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            float verticalOffset = buttonRect.rect.height * 0.5f + rect.sizeDelta.y * 0.5f + 14f;
            rect.anchoredPosition = buttonRect.anchoredPosition + new Vector2(0f, verticalOffset);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(28f, 76f);
        }

        Image bg = panelObject.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.1f, 0.15f, 0.9f);
        bg.type = Image.Type.Sliced;

        CreateLabel(panelObject.transform, "ShipInventoryPanelLabel", "SHIP INVENTORY", new Vector2(12f, -14f), new Vector2(240f, 24f), TextAlignmentOptions.Left, 20f);

        slotButtons = new Button[PlayerInventoryData.ShipSlotCount];
        slotIcons = new Image[PlayerInventoryData.ShipSlotCount];
        slotLabels = new TMP_Text[PlayerInventoryData.ShipSlotCount];

        const float slotSize = 55f;
        const float slotSpacing = 10f;
        Vector2 start = new Vector2(20f, -52f);

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                int index = row * 5 + col;
                Vector2 pos = new Vector2(start.x + col * (slotSize + slotSpacing), start.y - row * (slotSize + slotSpacing));
                CreateSlot(index, pos, new Vector2(slotSize, slotSize));
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
        iconRect.sizeDelta = new Vector2(40f, 40f);

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;
        slotIcons[index] = icon;

        slotLabels[index] = CreateLabel(slotObject.transform, "Label", string.Empty, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, 12f);
        RectTransform labelRect = slotLabels[index].GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        slotLabels[index].margin = new Vector4(2f, 2f, 2f, 2f);
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
        rect.sizeDelta = new Vector2(64f, 64f);

        Image bg = dragVisualObject.GetComponent<Image>();
        bg.color = new Color(0.14f, 0.22f, 0.31f, 0.9f);
        bg.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(dragVisualObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(46f, 46f);
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

        int filledSlots = 0;

        for (int i = 0; i < normalized.ShipSlots.Length && i < slotButtons.Length; i++)
        {
            string itemId = normalized.ShipSlots[i];
            bool occupied = !string.IsNullOrWhiteSpace(itemId);
            Sprite icon = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;
            Image slotImage = slotButtons[i] != null ? slotButtons[i].GetComponent<Image>() : null;

            if (occupied)
                filledSlots++;

            if (slotImage != null)
            {
                slotImage.color = occupied
                    ? InventoryItemCatalog.GetRarityColor(itemId)
                    : new Color(0.11f, 0.16f, 0.22f, 0.98f);
            }

            if (slotIcons[i] != null)
            {
                slotIcons[i].sprite = icon;
                slotIcons[i].enabled = occupied && icon != null;
            }

            if (slotLabels[i] != null)
            {
                bool showText = occupied && icon == null;
                slotLabels[i].text = showText ? InventoryItemCatalog.GetShortLabel(itemId) : string.Empty;
                slotLabels[i].color = showText ? Color.white : new Color(0f, 0f, 0f, 0f);
            }
        }

        if (buttonText != null)
        {
            buttonText.text = "CARGO " + filledSlots + "/" + PlayerInventoryData.ShipSlotCount;
            buttonText.fontSize = 20f;
        }
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
        HideDragVisual();
        dragInProgress = false;
        draggedSlotIndex = -1;

        if (shouldDrop)
        {
            DropShipItem(index);
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

    void HideDragVisual()
    {
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);
    }

    void RefreshRuntimeLayout()
    {
        if (buttonObject == null)
            return;

        GameObject collectButton = GameObject.Find("CollectButton");
        GameObject joystickObject = GameObject.Find("JoystickBG");
        RectTransform collectRect = collectButton != null ? collectButton.GetComponent<RectTransform>() : null;
        RectTransform joystickRect = joystickObject != null ? joystickObject.GetComponent<RectTransform>() : null;
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();

        if (collectRect == null || buttonRect == null)
            return;

        buttonRect.anchorMin = collectRect.anchorMin;
        buttonRect.anchorMax = collectRect.anchorMax;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        float buttonVerticalOffset = collectRect.rect.height * 0.5f + buttonRect.rect.height * 0.5f + 30f;
        Vector2 targetButtonPosition = collectRect.anchoredPosition + new Vector2(0f, buttonVerticalOffset);
        if (joystickRect != null)
        {
            float joystickTop = joystickRect.anchoredPosition.y + joystickRect.rect.height * 0.5f;
            float minimumButtonCenterY = joystickTop + buttonRect.rect.height * 0.5f + 26f;
            targetButtonPosition.y = Mathf.Max(targetButtonPosition.y, minimumButtonCenterY);
        }

        buttonRect.anchoredPosition = targetButtonPosition;

        if (panelObject == null)
            return;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        if (panelRect == null)
            return;

        panelRect.anchorMin = buttonRect.anchorMin;
        panelRect.anchorMax = buttonRect.anchorMax;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        float panelVerticalOffset = buttonRect.rect.height * 0.5f + panelRect.rect.height * 0.5f + 20f;
        panelRect.anchoredPosition = buttonRect.anchoredPosition + new Vector2(0f, panelVerticalOffset);
    }

    void UpdateVisibility()
    {
        bool shouldShow = IsGameplayHudVisible();
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
            buttonObject.SetActive(shouldShow);

        if (panelObject != null)
            panelObject.SetActive(shouldShow && panelVisible);

        if (!shouldShow)
        {
            dragInProgress = false;
            draggedSlotIndex = -1;
            HideDragVisual();
        }
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return started;
        }

        return false;
    }
}
