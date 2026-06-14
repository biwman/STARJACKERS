using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
public class BoosterBarUI : MonoBehaviourPun
{
    const string BoosterBarName = "Booster_Bar";
    const string PercentName = "BoosterPercent";
    const float BarWidth = 440f;
    const float BarHeight = 42f;
    const float VerticalSpacing = 48f;

    PlayerMovement movement;
    Slider boosterBar;
    RectTransform boosterRect;
    RectTransform hpBarRect;
    Image fillImage;
    Image handleImage;
    TextMeshProUGUI percentText;
    bool isVisible = true;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateBoosterBar();
        RefreshBar();
    }

    void Update()
    {
        if (boosterBar == null)
        {
            CreateBoosterBar();
            RefreshBar();
        }

        UpdateVisibility();
        RefreshBar();
    }

    void OnDestroy()
    {
        if (boosterBar != null)
        {
            Destroy(boosterBar.gameObject);
        }
    }

    void CreateBoosterBar()
    {
        GameObject existingBar = GameObject.Find(BoosterBarName);
        if (existingBar != null)
        {
            Destroy(existingBar);
        }

        GameObject hpBarObject = GameObject.Find("HP_Bar");
        if (hpBarObject == null)
        {
            return;
        }

        GameObject clone = Instantiate(hpBarObject, hpBarObject.transform.parent);
        clone.name = BoosterBarName;

        hpBarRect = hpBarObject.GetComponent<RectTransform>();
        boosterRect = clone.GetComponent<RectTransform>();
        ApplyLayout(hpBarRect);

        boosterBar = clone.GetComponent<Slider>();
        boosterBar.minValue = 0f;
        boosterBar.maxValue = 1f;
        boosterBar.wholeNumbers = false;

        fillImage = FindFillImage(clone.transform);
        handleImage = FindImage(clone.transform, "Handle");
        HideHandle();
        Image backgroundImage = clone.GetComponentInChildren<Image>();
        DestroyIfExists(clone.transform, "HealthLabel");
        DestroyIfExists(clone.transform, "HealthValue");
        DestroyIfExists(clone.transform, "BoosterLabel");
        DestroyIfExists(clone.transform, PercentName);

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.045f, 0.035f, 0.02f, 0.95f);
            ConfigurePanelDepth(backgroundImage.gameObject, new Color(0f, 0f, 0f, 0.68f), new Color(1f, 0.82f, 0.24f, 0.38f));
        }

        if (fillImage != null)
        {
            fillImage.color = new Color(1f, 0.86f, 0.12f, 1f);
            ConfigureFillAccent(fillImage.gameObject, new Color(1f, 0.96f, 0.58f, 0.2f));
        }

        CreateLabel(clone.transform);
    }

    void RefreshBar()
    {
        if (movement == null || boosterBar == null)
        {
            return;
        }

        ApplyLayout();

        float normalized = movement.BoosterNormalized;
        boosterBar.value = normalized;

        if (fillImage == null)
        {
            return;
        }

        if (normalized > 0.5f)
        {
            float t = Mathf.InverseLerp(0.5f, 1f, normalized);
            fillImage.color = Color.Lerp(new Color(1f, 0.68f, 0.18f, 1f), new Color(1f, 0.9f, 0.18f, 1f), t);
        }
        else if (normalized > 0.2f)
        {
            float t = Mathf.InverseLerp(0.2f, 0.5f, normalized);
            fillImage.color = Color.Lerp(new Color(0.95f, 0.26f, 0.18f, 1f), new Color(1f, 0.68f, 0.18f, 1f), t);
        }
        else
        {
            fillImage.color = new Color(0.9f, 0.18f, 0.18f, 1f);
        }

        HideHandle();

        if (percentText != null)
        {
            percentText.text = Mathf.RoundToInt(normalized * 100f) + "%";
            percentText.color = normalized > 0.2f ? Color.white : new Color(1f, 0.84f, 0.84f, 1f);
        }
    }

    void UpdateVisibility()
    {
        if (boosterBar == null)
            return;

        bool shouldBeVisible = IsGameplayHudVisible();
        if (isVisible == shouldBeVisible)
            return;

        isVisible = shouldBeVisible;
        boosterBar.gameObject.SetActive(shouldBeVisible);
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

    void ApplyLayout()
    {
        if (boosterRect == null)
            return;

        if (hpBarRect == null)
        {
            GameObject hpBarObject = GameObject.Find("HP_Bar");
            hpBarRect = hpBarObject != null ? hpBarObject.GetComponent<RectTransform>() : null;
        }

        ApplyLayout(hpBarRect);
    }

    void ApplyLayout(RectTransform hpRect)
    {
        if (boosterRect == null || hpRect == null)
            return;

        boosterRect.sizeDelta = new Vector2(BarWidth, BarHeight);
        boosterRect.anchoredPosition = hpRect.anchoredPosition + new Vector2(0f, -VerticalSpacing * 2f);
    }

    Image FindFillImage(Transform root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == "Fill")
            {
                return image;
            }
        }

        return null;
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

    void DestroyIfExists(Transform parent, string objectName)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        if (existing != null)
            Destroy(existing.gameObject);
    }

    void CreateLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("BoosterLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(12f, 0f);
        rect.sizeDelta = new Vector2(180f, 0f);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = "BOOSTER";
        text.fontSize = 15f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        ConfigureTextShadow(text.gameObject);

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        GameObject percentObject = new GameObject(PercentName, typeof(RectTransform), typeof(TextMeshProUGUI));
        percentObject.transform.SetParent(parent, false);

        RectTransform percentRect = percentObject.GetComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(1f, 0f);
        percentRect.anchorMax = new Vector2(1f, 1f);
        percentRect.pivot = new Vector2(1f, 0.5f);
        percentRect.anchoredPosition = new Vector2(-12f, 0f);
        percentRect.sizeDelta = new Vector2(120f, 0f);

        percentText = percentObject.GetComponent<TextMeshProUGUI>();
        percentText.text = "100%";
        percentText.fontSize = 15f;
        percentText.color = Color.white;
        percentText.alignment = TextAlignmentOptions.Right;
        percentText.fontStyle = FontStyles.Bold;
        percentText.raycastTarget = false;
        ConfigureTextShadow(percentText.gameObject);

        if (referenceText != null)
        {
            percentText.font = referenceText.font;
            percentText.fontSharedMaterial = referenceText.fontSharedMaterial;
        }
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
