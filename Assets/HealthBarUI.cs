using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerHealth))]
public class HealthBarUI : MonoBehaviourPun
{
    const string HpBarName = "HP_Bar";
    const string LabelName = "HealthLabel";
    const string ValueName = "HealthValue";
    const float BarWidth = 440f;
    const float BarHeight = 42f;
    const float TopOffset = -24f;

    Slider hpBar;
    RectTransform hpRect;
    Image backgroundImage;
    Image fillImage;
    Image handleImage;
    TextMeshProUGUI labelText;
    TextMeshProUGUI valueText;
    bool isVisible = true;
    PlayerHealth health;

    void Start()
    {
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        health = GetComponent<PlayerHealth>();
        InitializeBar();
        RefreshVisuals();
    }

    void Update()
    {
        UpdateVisibility();
        RefreshVisuals();
    }

    void InitializeBar()
    {
        GameObject hpBarObject = GameObject.Find(HpBarName);
        if (hpBarObject == null)
            return;

        hpBar = hpBarObject.GetComponent<Slider>();
        hpRect = hpBarObject.GetComponent<RectTransform>();

        if (hpBar == null || hpRect == null)
            return;

        hpRect.sizeDelta = new Vector2(BarWidth, BarHeight);
        hpRect.anchoredPosition = new Vector2(0f, TopOffset);

        backgroundImage = FindImage(hpBar.transform, "Background");
        fillImage = hpBar.fillRect != null ? hpBar.fillRect.GetComponent<Image>() : null;
        handleImage = FindImage(hpBar.transform, "Handle");
        HideHandle();

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.025f, 0.035f, 0.045f, 0.96f);
            ConfigurePanelDepth(backgroundImage.gameObject, new Color(0f, 0f, 0f, 0.72f), new Color(0.76f, 1f, 0.82f, 0.42f));
        }

        if (fillImage != null)
        {
            fillImage.color = new Color(0.32f, 1f, 0.46f, 1f);
            ConfigureFillAccent(fillImage.gameObject, new Color(0.9f, 1f, 0.88f, 0.24f));
        }

        hpBar.transition = Selectable.Transition.None;
        hpBar.targetGraphic = backgroundImage;

        labelText = GetOrCreateText(LabelName, new Vector2(12f, 0f), TextAlignmentOptions.Left, "HEALTH");
        valueText = GetOrCreateText(ValueName, new Vector2(-12f, 0f), TextAlignmentOptions.Right, string.Empty);
    }

    void RefreshVisuals()
    {
        if (hpBar == null)
            return;

        if (health != null)
        {
            hpBar.maxValue = Mathf.Max(1, health.maxHP);
            hpBar.value = Mathf.Clamp(health.CurrentHP, 0, Mathf.RoundToInt(hpBar.maxValue));
        }

        float normalized = hpBar.maxValue > 0f ? hpBar.value / hpBar.maxValue : 0f;

        if (valueText != null)
        {
            valueText.text = Mathf.RoundToInt(hpBar.value) + " / " + Mathf.RoundToInt(hpBar.maxValue);
        }

        if (fillImage == null)
            return;

        Color low = new Color(0.89f, 0.2f, 0.24f, 1f);
        Color mid = new Color(0.95f, 0.75f, 0.2f, 1f);
        Color high = new Color(0.24f, 0.86f, 0.38f, 1f);

        if (normalized > 0.5f)
        {
            float t = Mathf.InverseLerp(0.5f, 1f, normalized);
            fillImage.color = Color.Lerp(mid, high, t);
        }
        else
        {
            float t = Mathf.InverseLerp(0f, 0.5f, normalized);
            fillImage.color = Color.Lerp(low, mid, t);
        }

        HideHandle();
    }

    void UpdateVisibility()
    {
        if (hpBar == null)
            return;

        bool shouldBeVisible = IsGameplayHudVisible();
        if (isVisible == shouldBeVisible)
            return;

        isVisible = shouldBeVisible;
        hpBar.gameObject.SetActive(shouldBeVisible);
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

    TextMeshProUGUI GetOrCreateText(string objectName, Vector2 anchoredPosition, TextAlignmentOptions alignment, string initialText)
    {
        Transform existing = hpBar.transform.Find(objectName);
        GameObject textObject;

        if (existing != null)
        {
            textObject = existing.gameObject;
        }
        else
        {
            textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(hpBar.transform, false);
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = 14f;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        ConfigureTextShadow(text.gameObject);

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return text;
    }

    Image FindImage(Transform root, string objectName)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == objectName)
            {
                return image;
            }
        }

        return null;
    }

    void HideHandle()
    {
        if (handleImage == null)
            return;

        handleImage.enabled = false;
        handleImage.raycastTarget = false;
    }

    void ConfigurePanelDepth(GameObject target, Color shadowColor, Color outlineColor)
    {
        if (target == null)
            return;

        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
            shadow = target.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = false;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = false;
    }

    void ConfigureFillAccent(GameObject target, Color color)
    {
        if (target == null)
            return;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, 1f);
        outline.useGraphicAlpha = true;
    }

    void ConfigureTextShadow(GameObject target)
    {
        if (target == null)
            return;

        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
            shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
        shadow.effectDistance = new Vector2(1.6f, -1.6f);
        shadow.useGraphicAlpha = false;
    }
}
