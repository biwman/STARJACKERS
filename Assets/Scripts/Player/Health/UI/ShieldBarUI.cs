using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerHealth))]
public class ShieldBarUI : MonoBehaviourPun
{
    const string ShieldBarName = "Shield_Bar";
    const string ShieldLabelName = "ShieldLabel";
    const string ShieldValueName = "ShieldValue";
    const float BarWidth = 440f;
    const float BarHeight = 42f;
    const float VerticalSpacing = 48f;
    static readonly Color LowShieldColor = new Color(0.08f, 0.36f, 0.88f, 1f);
    static readonly Color HighShieldColor = new Color(0.34f, 0.96f, 1f, 1f);

    PlayerHealth health;
    Slider shieldBar;
    RectTransform hpRect;
    RectTransform shieldRect;
    Image fillImage;
    Image backgroundImage;
    Image handleImage;
    TMPro.TextMeshProUGUI labelText;
    TMPro.TextMeshProUGUI valueText;
    bool isVisible = true;
    int lastDisplayedShield = int.MinValue;
    int lastDisplayedMaxShield = int.MinValue;
    Vector2 lastHpAnchoredPosition = new Vector2(float.NaN, float.NaN);

    void Start()
    {
        health = GetComponent<PlayerHealth>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateShieldBar();
        RefreshBar();
    }

    void Update()
    {
        if (shieldBar == null)
        {
            CreateShieldBar();
            RefreshBar();
        }

        bool visibilityChanged = UpdateVisibility();
        if (isVisible || visibilityChanged)
            RefreshBar(visibilityChanged);
    }

    void OnDestroy()
    {
        if (shieldBar != null)
            Destroy(shieldBar.gameObject);
    }

    void CreateShieldBar()
    {
        GameObject existingBar = GameObject.Find(ShieldBarName);
        if (existingBar != null)
            Destroy(existingBar);

        GameObject hpBarObject = GameObject.Find("HP_Bar");
        if (hpBarObject == null)
            return;

        GameObject clone = Object.Instantiate(hpBarObject, hpBarObject.transform.parent);
        clone.name = ShieldBarName;

        hpRect = hpBarObject.GetComponent<RectTransform>();
        shieldRect = clone.GetComponent<RectTransform>();
        ApplyLayout(hpRect);

        shieldBar = clone.GetComponent<Slider>();
        shieldBar.minValue = 0f;
        shieldBar.maxValue = health != null ? health.MaxShield : 50f;
        shieldBar.wholeNumbers = false;

        backgroundImage = FindImage(clone.transform, "Background");
        fillImage = FindImage(clone.transform, "Fill");
        handleImage = FindImage(clone.transform, "Handle");
        HideHandle();
        DestroyIfExists(clone.transform, "HealthLabel");
        DestroyIfExists(clone.transform, "HealthValue");
        DestroyIfExists(clone.transform, ShieldLabelName);
        DestroyIfExists(clone.transform, ShieldValueName);

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.02f, 0.045f, 0.075f, 0.96f);
            ConfigurePanelDepth(backgroundImage.gameObject, new Color(0f, 0f, 0f, 0.72f), new Color(0.36f, 0.9f, 1f, 0.5f));
        }

        if (fillImage != null)
        {
            fillImage.color = new Color(0.2f, 0.88f, 1f, 1f);
            ConfigureFillAccent(fillImage.gameObject, new Color(0.72f, 1f, 1f, 0.26f));
        }

        labelText = CreateText(clone.transform, ShieldLabelName, new Vector2(12f, 0f), TMPro.TextAlignmentOptions.Left, "SHIELD");
        valueText = CreateText(clone.transform, ShieldValueName, new Vector2(-12f, 0f), TMPro.TextAlignmentOptions.Right, string.Empty);
    }

    void RefreshBar(bool force = false)
    {
        if (health == null || shieldBar == null)
            return;

        ApplyLayoutIfNeeded(force);

        int maxShield = Mathf.Max(0, health.MaxShield);
        int currentShield = Mathf.Clamp(health.CurrentShield, 0, Mathf.Max(1, maxShield));
        if (!force && currentShield == lastDisplayedShield && maxShield == lastDisplayedMaxShield)
            return;

        lastDisplayedShield = currentShield;
        lastDisplayedMaxShield = maxShield;

        shieldBar.maxValue = maxShield;
        shieldBar.value = currentShield;

        if (valueText != null)
            valueText.text = currentShield + " / " + maxShield;

        float normalized = maxShield > 0 ? currentShield / (float)maxShield : 0f;
        if (fillImage != null)
            fillImage.color = Color.Lerp(LowShieldColor, HighShieldColor, normalized);

        HideHandle();
    }

    bool UpdateVisibility()
    {
        if (shieldBar == null)
            return false;

        bool shouldBeVisible = health != null &&
                               health.MaxShield > 0 &&
                               PhotonNetwork.CurrentRoom != null &&
                               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
                               value is bool started &&
                               GameplayHudVisibility.IsGameplayHudVisible(started);
        if (isVisible == shouldBeVisible)
            return false;

        isVisible = shouldBeVisible;
        shieldBar.gameObject.SetActive(shouldBeVisible);
        return true;
    }

    void ApplyLayoutIfNeeded(bool force = false)
    {
        if (shieldRect == null)
            return;

        if (hpRect == null)
        {
            GameObject hpBarObject = GameObject.Find("HP_Bar");
            hpRect = hpBarObject != null ? hpBarObject.GetComponent<RectTransform>() : null;
        }

        if (!force && hpRect != null && lastHpAnchoredPosition == hpRect.anchoredPosition)
            return;

        ApplyLayout(hpRect);
    }

    void ApplyLayout(RectTransform hpRect)
    {
        if (shieldRect == null || hpRect == null)
            return;

        shieldRect.sizeDelta = new Vector2(BarWidth, BarHeight);
        shieldRect.anchoredPosition = hpRect.anchoredPosition + new Vector2(0f, -VerticalSpacing);
        lastHpAnchoredPosition = hpRect.anchoredPosition;
    }

    TMPro.TextMeshProUGUI CreateText(Transform parent, string objectName, Vector2 anchoredPosition, TMPro.TextAlignmentOptions alignment, string initialText)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;

        TMPro.TextMeshProUGUI text = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = 15f;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        ConfigureTextShadow(text.gameObject);

        TMPro.TMP_Text referenceText = Object.FindAnyObjectByType<TMPro.TMP_Text>();
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return text;
    }

    void DestroyIfExists(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            Destroy(existing.gameObject);
    }

    Image FindImage(Transform root, string objectName)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == objectName)
                return image;
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
