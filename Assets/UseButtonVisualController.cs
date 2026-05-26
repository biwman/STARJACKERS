using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UseButtonVisualController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    Button button;
    RectTransform visualRootRect;
    RectTransform panelRect;
    RectTransform innerShadeRect;
    RectTransform leftAccentRect;
    RectTransform rightAccentRect;
    RectTransform topAccentRect;
    RectTransform bottomAccentRect;
    RectTransform labelRect;
    Image frameImage;
    Image panelImage;
    Image innerShadeImage;
    Image progressFillImage;
    TMP_Text labelText;
    Color inactiveBaseColor;
    Color inactiveHighlightedColor;
    Color inactivePressedColor;
    Color activeBaseColor;
    Color activeHighlightedColor;
    Color activePressedColor;
    bool pointerDown;
    bool pointerInside;
    bool available;
    string overrideLabel;
    bool hasOverrideLabel;
    float progressValue;
    bool progressVisible;

    public void Configure(Button targetButton, RectTransform visualRoot, RectTransform panel, RectTransform innerShade,
        RectTransform leftAccent, RectTransform rightAccent, RectTransform topAccent, RectTransform bottomAccent,
        RectTransform label, Image frameImageRef, Image panelImageRef, Image innerShadeImageRef, Image progressFillImageRef, TMP_Text labelTextRef,
        Color inactiveBaseTint, Color inactiveHighlightedTint, Color inactivePressedTint,
        Color activeBaseTint, Color activeHighlightedTint, Color activePressedTint)
    {
        button = targetButton;
        visualRootRect = visualRoot;
        panelRect = panel;
        innerShadeRect = innerShade;
        leftAccentRect = leftAccent;
        rightAccentRect = rightAccent;
        topAccentRect = topAccent;
        bottomAccentRect = bottomAccent;
        labelRect = label;
        frameImage = frameImageRef;
        panelImage = panelImageRef;
        innerShadeImage = innerShadeImageRef;
        progressFillImage = progressFillImageRef;
        labelText = labelTextRef;
        inactiveBaseColor = inactiveBaseTint;
        inactiveHighlightedColor = inactiveHighlightedTint;
        inactivePressedColor = inactivePressedTint;
        activeBaseColor = activeBaseTint;
        activeHighlightedColor = activeHighlightedTint;
        activePressedColor = activePressedTint;
        ApplyLabel();
        ApplyProgressState();
        ApplyStateImmediate();
    }

    public void SetLabel(string value)
    {
        overrideLabel = value ?? string.Empty;
        hasOverrideLabel = true;
        ApplyLabel();
    }

    public void SetAvailable(bool value)
    {
        if (available == value)
            return;

        available = value;
        ApplyStateImmediate();
    }

    public void SetProgress(float progress, bool visible)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        if (Mathf.Approximately(progressValue, clampedProgress) && progressVisible == visible)
            return;

        progressValue = clampedProgress;
        progressVisible = visible;
        ApplyProgressState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDown = true;
        ApplyStateImmediate();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
        ApplyStateImmediate();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        ApplyStateImmediate();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerDown = false;
        ApplyStateImmediate();
    }

    void OnDisable()
    {
        pointerDown = false;
        pointerInside = false;
        SetProgress(0f, false);
    }

    void LateUpdate()
    {
        ApplyStateImmediate();
    }

    void ApplyLabel()
    {
        if (!hasOverrideLabel || labelText == null)
            return;

        if (labelText.text != overrideLabel)
            labelText.text = overrideLabel;
    }

    void ApplyStateImmediate()
    {
        if (button == null || panelRect == null || panelImage == null)
            return;

        bool disabled = !button.interactable;
        bool pressed = !disabled && pointerDown;
        bool hovered = !disabled && pointerInside;

        if (visualRootRect != null)
            visualRootRect.localScale = pressed ? new Vector3(0.985f, 0.965f, 1f) : Vector3.one;

        panelRect.anchoredPosition = pressed ? new Vector2(0f, -3.5f) : hovered ? new Vector2(0f, -1f) : Vector2.zero;

        if (innerShadeRect != null)
            innerShadeRect.anchoredPosition = pressed ? new Vector2(0f, 2f) : Vector2.zero;

        if (labelRect != null)
            labelRect.anchoredPosition = pressed ? new Vector2(0f, -2f) : hovered ? new Vector2(0f, -0.5f) : Vector2.zero;

        Color baseColor = available ? activeBaseColor : inactiveBaseColor;
        Color highlightedColor = available ? activeHighlightedColor : inactiveHighlightedColor;
        Color pressedColor = available ? activePressedColor : inactivePressedColor;

        if (frameImage != null)
        {
            frameImage.color = disabled
                ? new Color(0.2f, 0.22f, 0.25f, 0.72f)
                : available
                    ? new Color(0.34f, 0.48f, 0.42f, 0.98f)
                    : new Color(0.36f, 0.42f, 0.5f, 0.98f);
        }

        panelImage.color = disabled
            ? button.colors.disabledColor
            : pressed ? pressedColor : hovered ? highlightedColor : baseColor;

        if (innerShadeImage != null)
        {
            innerShadeImage.color = disabled
                ? new Color(0.02f, 0.03f, 0.05f, 0.4f)
                : available
                    ? pressed
                        ? new Color(0.02f, 0.055f, 0.035f, 0.68f)
                        : hovered
                            ? new Color(0.035f, 0.1f, 0.06f, 0.48f)
                            : new Color(0.025f, 0.075f, 0.045f, 0.42f)
                    : pressed
                        ? new Color(0.015f, 0.03f, 0.045f, 0.78f)
                        : hovered
                            ? new Color(0.035f, 0.075f, 0.1f, 0.66f)
                    : new Color(0.02f, 0.04f, 0.065f, 0.58f);
        }

        ApplyProgressState();

        Color hardAccent = disabled
            ? new Color(0.34f, 0.37f, 0.4f, 0.32f)
            : available
                ? pressed
                    ? new Color(0.58f, 1f, 0.72f, 1f)
                    : hovered
                        ? new Color(0.48f, 0.96f, 0.64f, 0.98f)
                        : new Color(0.42f, 0.9f, 0.58f, 0.95f)
                : pressed
                    ? new Color(0.46f, 0.84f, 1f, 0.72f)
                    : hovered
                        ? new Color(0.4f, 0.78f, 1f, 0.58f)
                        : new Color(0.32f, 0.62f, 0.78f, 0.42f);

        Color softAccent = disabled
            ? new Color(0.34f, 0.37f, 0.4f, 0.16f)
            : available
                ? pressed
                    ? new Color(0.58f, 1f, 0.72f, 0.62f)
                    : hovered
                        ? new Color(0.48f, 0.96f, 0.64f, 0.46f)
                        : new Color(0.42f, 0.9f, 0.58f, 0.34f)
                : pressed
                    ? new Color(0.46f, 0.84f, 1f, 0.36f)
                    : hovered
                        ? new Color(0.4f, 0.78f, 1f, 0.28f)
                        : new Color(0.32f, 0.62f, 0.78f, 0.18f);

        ApplyAccentState(leftAccentRect, pressed ? new Vector2(7f, 34f) : hovered ? new Vector2(6f, 31f) : new Vector2(6f, 28f), hardAccent);
        ApplyAccentState(rightAccentRect, pressed ? new Vector2(7f, 34f) : hovered ? new Vector2(6f, 31f) : new Vector2(6f, 28f), hardAccent);
        ApplyAccentState(topAccentRect, pressed ? new Vector2(70f, 5f) : hovered ? new Vector2(64f, 4f) : new Vector2(58f, 4f), softAccent);
        ApplyAccentState(bottomAccentRect, pressed ? new Vector2(56f, 4f) : hovered ? new Vector2(50f, 3f) : new Vector2(46f, 3f), softAccent);

        if (labelText != null)
        {
            labelText.color = disabled
                ? new Color(0.62f, 0.66f, 0.71f, 0.82f)
                : available || hovered || pressed
                    ? Color.white
                    : new Color(0.82f, 0.88f, 0.92f, 0.88f);
        }
    }

    void ApplyProgressState()
    {
        if (progressFillImage == null)
            return;

        bool visible = progressVisible && progressValue > 0.001f;
        progressFillImage.gameObject.SetActive(visible);
        progressFillImage.fillAmount = visible ? progressValue : 0f;
        progressFillImage.color = new Color(0.16f, 0.95f, 0.34f, 0.76f);
    }

    static void ApplyAccentState(RectTransform rect, Vector2 size, Color color)
    {
        if (rect == null)
            return;

        rect.sizeDelta = size;
        Image image = rect.GetComponent<Image>();
        if (image != null)
            image.color = color;
    }
}
