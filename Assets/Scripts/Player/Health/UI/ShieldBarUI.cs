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

        UpdateVisibility();
        RefreshBar();
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

    void RefreshBar()
    {
        if (health == null || shieldBar == null)
            return;

        ApplyLayout();

        shieldBar.maxValue = health.MaxShield;
        shieldBar.value = health.CurrentShield;

        if (valueText != null)
            valueText.text = health.CurrentShield + " / " + health.MaxShield;

        float normalized = shieldBar.maxValue > 0f ? shieldBar.value / shieldBar.maxValue : 0f;
        if (fillImage != null)
            fillImage.color = Color.Lerp(new Color(0.08f, 0.36f, 0.88f, 1f), new Color(0.34f, 0.96f, 1f, 1f), normalized);

        HideHandle();
    }

    void UpdateVisibility()
    {
        if (shieldBar == null)
            return;

        bool shouldBeVisible = health != null &&
                               health.MaxShield > 0 &&
                               PhotonNetwork.CurrentRoom != null &&
                               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
                               value is bool started &&
                               GameplayHudVisibility.IsGameplayHudVisible(started);
        if (isVisible == shouldBeVisible)
            return;

        isVisible = shouldBeVisible;
        shieldBar.gameObject.SetActive(shouldBeVisible);
    }

    void ApplyLayout()
    {
        if (shieldRect == null)
            return;

        if (hpRect == null)
        {
            GameObject hpBarObject = GameObject.Find("HP_Bar");
            hpRect = hpBarObject != null ? hpBarObject.GetComponent<RectTransform>() : null;
        }

        ApplyLayout(hpRect);
    }

    void ApplyLayout(RectTransform hpRect)
    {
        if (shieldRect == null || hpRect == null)
            return;

        shieldRect.sizeDelta = new Vector2(BarWidth, BarHeight);
        shieldRect.anchoredPosition = hpRect.anchoredPosition + new Vector2(0f, -VerticalSpacing);
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
