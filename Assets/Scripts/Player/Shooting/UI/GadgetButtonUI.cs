using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerShooting))]
public class GadgetButtonUI : MonoBehaviourPun
{
    sealed class GadgetButtonWidget
    {
        public string ItemId;
        public GameObject Root;
        public Button Button;
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Charges;
        public int LastRemaining = int.MinValue;
        public int LastMax = int.MinValue;
        public bool LastCanUse;
        public bool LastDepleted;
        public bool HasLastState;
        public Sprite LastIcon;
        public string LastLabel;
    }

    const string GadgetButtonRootName = "GadgetButtonsRoot";
    const float GadgetButtonSize = 112f;
    const float GadgetButtonBaseOffsetY = 392f;
    const float GadgetButtonPitch = 126f;
    const float GadgetButtonTopPadding = 24f;
    const float LayoutRefreshInterval = 0.25f;
    const float StateRefreshInterval = 0.1f;
    static Sprite circularButtonSprite;

    PlayerShooting shooting;
    GameObject rootObject;
    RectTransform rootRect;
    RectTransform canvasRect;
    RectTransform shootJoystickRect;
    readonly List<GadgetButtonWidget> widgets = new List<GadgetButtonWidget>();
    string lastWidgetSignature = string.Empty;
    float nextLayoutRefreshTime;
    float nextStateRefreshTime;
    bool forceLayoutRefresh;
    bool forceStateRefresh = true;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        RebuildButtonsIfNeeded();
        RefreshRuntimeLayout();
        RefreshState(true);
    }

    void Update()
    {
        RebuildButtonsIfNeeded();

        if (forceLayoutRefresh || Time.unscaledTime >= nextLayoutRefreshTime)
        {
            nextLayoutRefreshTime = Time.unscaledTime + LayoutRefreshInterval;
            forceLayoutRefresh = false;
            RefreshRuntimeLayout();
        }

        if (forceStateRefresh || Time.unscaledTime >= nextStateRefreshTime)
        {
            nextStateRefreshTime = Time.unscaledTime + StateRefreshInterval;
            RefreshState(forceStateRefresh);
            forceStateRefresh = false;
        }
    }

    void OnDestroy()
    {
        DestroyAllButtons();
    }

    void RebuildButtonsIfNeeded()
    {
        if (!photonView.IsMine || shooting == null)
            return;

        IReadOnlyList<string> itemIds = shooting.ActiveGadgetItemIds;
        string signature = BuildWidgetSignature(itemIds);
        if (signature == lastWidgetSignature && rootObject != null && widgets.Count == itemIds.Count)
            return;

        DestroyAllButtons();
        CreateButtons(itemIds);
        lastWidgetSignature = signature;
        forceLayoutRefresh = true;
        forceStateRefresh = true;
    }

    void CreateButtons(IReadOnlyList<string> itemIds)
    {
        GameObject canvas = GameObject.Find("Canvas");
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (canvas == null || shootJoystickObject == null)
            return;

        canvasRect = canvas.GetComponent<RectTransform>();
        shootJoystickRect = shootJoystickObject.GetComponent<RectTransform>();
        if (canvasRect == null || shootJoystickRect == null)
            return;

        GameObject existingRoot = GameObject.Find(GadgetButtonRootName);
        if (existingRoot != null)
            Destroy(existingRoot);

        rootObject = new GameObject(GadgetButtonRootName, typeof(RectTransform));
        rootObject.transform.SetParent(canvas.transform, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;
        ApplyRootLayoutFromJoystick();

        int rowsPerColumn = CalculateRowsPerColumn(itemIds.Count);

        for (int i = 0; i < itemIds.Count; i++)
        {
            GadgetButtonWidget widget = CreateWidget(itemIds[i], i, rowsPerColumn);
            if (widget != null)
                widgets.Add(widget);
        }
    }

    GadgetButtonWidget CreateWidget(string itemId, int index, int rowsPerColumn)
    {
        if (rootObject == null || string.IsNullOrWhiteSpace(itemId))
            return null;

        GadgetButtonWidget widget = new GadgetButtonWidget();
        widget.ItemId = itemId;
        widget.Root = new GameObject("GadgetButton_" + itemId, typeof(RectTransform), typeof(Image), typeof(Button));
        widget.Root.transform.SetParent(rootObject.transform, false);

        RectTransform rect = widget.Root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = GetWidgetAnchoredPosition(index, rowsPerColumn);
        rect.sizeDelta = new Vector2(GadgetButtonSize, GadgetButtonSize);

        widget.Background = widget.Root.GetComponent<Image>();
        widget.Background.sprite = GetCircularButtonSprite();
        widget.Background.type = Image.Type.Simple;

        widget.Button = widget.Root.GetComponent<Button>();
        widget.Button.transition = Selectable.Transition.ColorTint;
        widget.Button.targetGraphic = widget.Background;
        string capturedItemId = itemId;
        if (shooting != null && shooting.IsHoldGadget(capturedItemId))
            ConfigureHoldGadgetInput(widget.Button, capturedItemId);
        else
            widget.Button.onClick.AddListener(() => HandleGadgetClicked(capturedItemId));

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(widget.Root.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 8f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        widget.Icon = iconObject.GetComponent<Image>();
        widget.Icon.preserveAspect = true;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(widget.Root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 14f);
        labelRect.sizeDelta = new Vector2(90f, 24f);
        widget.Label = labelObject.GetComponent<TextMeshProUGUI>();
        widget.Label.fontSize = 18f;
        widget.Label.fontStyle = FontStyles.Bold;
        widget.Label.alignment = TextAlignmentOptions.Center;
        widget.Label.textWrappingMode = TextWrappingModes.NoWrap;
        if (referenceText != null)
        {
            widget.Label.font = referenceText.font;
            widget.Label.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        GameObject chargesObject = new GameObject("Charges", typeof(RectTransform), typeof(TextMeshProUGUI));
        chargesObject.transform.SetParent(widget.Root.transform, false);
        RectTransform chargesRect = chargesObject.GetComponent<RectTransform>();
        chargesRect.anchorMin = new Vector2(0.5f, 0f);
        chargesRect.anchorMax = new Vector2(0.5f, 0f);
        chargesRect.pivot = new Vector2(0.5f, 0f);
        chargesRect.anchoredPosition = new Vector2(0f, -8f);
        chargesRect.sizeDelta = new Vector2(72f, 24f);
        widget.Charges = chargesObject.GetComponent<TextMeshProUGUI>();
        widget.Charges.fontSize = 20f;
        widget.Charges.fontStyle = FontStyles.Bold;
        widget.Charges.alignment = TextAlignmentOptions.Center;
        widget.Charges.textWrappingMode = TextWrappingModes.NoWrap;
        if (referenceText != null)
        {
            widget.Charges.font = referenceText.font;
            widget.Charges.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return widget;
    }

    void RefreshRuntimeLayout()
    {
        if (rootObject == null || rootRect == null || widgets.Count == 0)
            return;

        if (shootJoystickRect == null)
        {
            GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
            shootJoystickRect = shootJoystickObject != null ? shootJoystickObject.GetComponent<RectTransform>() : null;
        }

        if (canvasRect == null)
        {
            Transform parent = rootObject.transform.parent;
            canvasRect = parent != null ? parent.GetComponent<RectTransform>() : null;
        }

        ApplyRootLayoutFromJoystick();
        int rowsPerColumn = CalculateRowsPerColumn(widgets.Count);
        for (int i = 0; i < widgets.Count; i++)
        {
            GadgetButtonWidget widget = widgets[i];
            RectTransform widgetRect = widget?.Root != null ? widget.Root.GetComponent<RectTransform>() : null;
            if (widgetRect != null)
                widgetRect.anchoredPosition = GetWidgetAnchoredPosition(i, rowsPerColumn);
        }
    }

    void ApplyRootLayoutFromJoystick()
    {
        if (rootRect == null || shootJoystickRect == null)
            return;

        rootRect.anchorMin = shootJoystickRect.anchorMin;
        rootRect.anchorMax = shootJoystickRect.anchorMax;
        rootRect.anchoredPosition = shootJoystickRect.anchoredPosition;
    }

    int CalculateRowsPerColumn(int buttonCount)
    {
        if (buttonCount <= 0)
            return 1;

        if (rootRect == null || canvasRect == null)
            return buttonCount;

        float maxChildCenterY = canvasRect.rect.yMax - GadgetButtonTopPadding - (GadgetButtonSize * 0.5f) - GetRootCenterY();
        int fittingRows = Mathf.FloorToInt((maxChildCenterY - GadgetButtonBaseOffsetY) / GadgetButtonPitch) + 1;
        return Mathf.Clamp(fittingRows, 1, buttonCount);
    }

    float GetRootCenterY()
    {
        if (rootRect == null || canvasRect == null)
            return 0f;

        Vector2 anchorCenter = (rootRect.anchorMin + rootRect.anchorMax) * 0.5f;
        float anchorY = Mathf.Lerp(canvasRect.rect.yMin, canvasRect.rect.yMax, anchorCenter.y);
        return anchorY + rootRect.anchoredPosition.y;
    }

    static Vector2 GetWidgetAnchoredPosition(int index, int rowsPerColumn)
    {
        rowsPerColumn = Mathf.Max(1, rowsPerColumn);
        int column = Mathf.Max(0, index) / rowsPerColumn;
        int row = Mathf.Max(0, index) % rowsPerColumn;
        return new Vector2(-column * GadgetButtonPitch, GadgetButtonBaseOffsetY + (row * GadgetButtonPitch));
    }

    void ConfigureHoldGadgetInput(Button button, string itemId)
    {
        if (button == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers = new List<EventTrigger.Entry>();
        AddGadgetTrigger(trigger, EventTriggerType.PointerDown, () => shooting?.BeginGadgetUse(itemId));
        AddGadgetTrigger(trigger, EventTriggerType.PointerUp, () => shooting?.EndGadgetUse(itemId));
        AddGadgetTrigger(trigger, EventTriggerType.PointerExit, () => shooting?.EndGadgetUse(itemId));
        AddGadgetTrigger(trigger, EventTriggerType.Cancel, () => shooting?.EndGadgetUse(itemId));
    }

    void AddGadgetTrigger(EventTrigger trigger, EventTriggerType eventType, System.Action callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(_ => callback?.Invoke());
        trigger.triggers.Add(entry);
    }

    void RefreshState(bool force = false)
    {
        if (shooting == null)
            return;

        if (rootObject != null)
            rootObject.SetActive(GameplayHudVisibility.IsGameplayHudVisible(widgets.Count > 0));

        for (int i = 0; i < widgets.Count; i++)
        {
            GadgetButtonWidget widget = widgets[i];
            if (widget == null || widget.Root == null)
                continue;

            int remaining = shooting.GetRemainingGadgetCharges(widget.ItemId);
            int max = shooting.GetMaxGadgetCharges(widget.ItemId);
            bool canUse = shooting.CanUseGadget(widget.ItemId);
            bool depleted = max > 0 && remaining <= 0;
            Sprite icon = shooting.GetGadgetIcon(widget.ItemId);
            string label = shooting.GetGadgetButtonLabel(widget.ItemId);

            if (!force &&
                widget.HasLastState &&
                widget.LastRemaining == remaining &&
                widget.LastMax == max &&
                widget.LastCanUse == canUse &&
                widget.LastDepleted == depleted &&
                widget.LastIcon == icon &&
                string.Equals(widget.LastLabel, label, System.StringComparison.Ordinal))
            {
                continue;
            }

            widget.HasLastState = true;
            widget.LastRemaining = remaining;
            widget.LastMax = max;
            widget.LastCanUse = canUse;
            widget.LastDepleted = depleted;
            widget.LastIcon = icon;
            widget.LastLabel = label;

            widget.Icon.sprite = icon;
            widget.Icon.enabled = icon != null;
            widget.Label.text = label;
            widget.Charges.text = max > 0 ? remaining.ToString() : string.Empty;

            widget.Button.interactable = canUse;
            widget.Background.color = canUse
                ? shooting.GetGadgetButtonColor(widget.ItemId)
                : new Color(0.15f, 0.19f, 0.24f, 0.82f);
            widget.Label.color = canUse
                ? Color.white
                : new Color(0.82f, 0.86f, 0.91f, 0.82f);
            widget.Charges.color = canUse
                ? Color.white
                : new Color(0.78f, 0.8f, 0.82f, 0.82f);
            widget.Icon.color = depleted
                ? new Color(0.6f, 0.6f, 0.6f, 0.72f)
                : canUse
                    ? new Color(1f, 1f, 1f, 0.88f)
                    : new Color(0.82f, 0.86f, 0.91f, 0.54f);
        }
    }

    void HandleGadgetClicked(string itemId)
    {
        if (shooting == null)
            return;

        shooting.TriggerGadgetUse(itemId);
    }

    void DestroyAllButtons()
    {
        for (int i = 0; i < widgets.Count; i++)
        {
            if (widgets[i]?.Root != null)
                Destroy(widgets[i].Root);
        }

        widgets.Clear();
        lastWidgetSignature = string.Empty;
        forceLayoutRefresh = true;
        forceStateRefresh = true;

        if (rootObject != null)
        {
            Destroy(rootObject);
            rootObject = null;
        }

        rootRect = null;
        canvasRect = null;
        shootJoystickRect = null;
    }

    static string BuildWidgetSignature(IReadOnlyList<string> itemIds)
    {
        if (itemIds == null || itemIds.Count == 0)
            return string.Empty;

        return string.Join("|", itemIds);
    }

    static Sprite GetCircularButtonSprite()
    {
        if (circularButtonSprite != null)
            return circularButtonSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GadgetButtonCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        float feather = size * 0.06f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - feather, radius, distance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        circularButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circularButtonSprite;
    }
}
