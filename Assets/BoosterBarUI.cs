using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
public class BoosterBarUI : MonoBehaviourPun
{
    const string BoosterBarName = "Booster_Bar";
    const string PercentName = "BoosterPercent";

    PlayerMovement movement;
    Slider boosterBar;
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

        RectTransform hpRect = hpBarObject.GetComponent<RectTransform>();
        RectTransform boosterRect = clone.GetComponent<RectTransform>();
        boosterRect.anchoredPosition = hpRect.anchoredPosition + new Vector2(0f, -110f);

        boosterBar = clone.GetComponent<Slider>();
        boosterBar.minValue = 0f;
        boosterBar.maxValue = 1f;
        boosterBar.wholeNumbers = false;

        fillImage = FindFillImage(clone.transform);
        handleImage = FindImage(clone.transform, "Handle");
        Image backgroundImage = clone.GetComponentInChildren<Image>();
        DestroyIfExists(clone.transform, "HealthLabel");
        DestroyIfExists(clone.transform, "HealthValue");
        DestroyIfExists(clone.transform, "BoosterLabel");
        DestroyIfExists(clone.transform, PercentName);

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);
        }

        if (fillImage != null)
        {
            fillImage.color = new Color(1f, 0.9f, 0.18f, 1f);
        }

        CreateLabel(clone.transform);
    }

    void RefreshBar()
    {
        if (movement == null || boosterBar == null)
        {
            return;
        }

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

        if (handleImage != null)
        {
            handleImage.color = normalized >= 0.999f
                ? new Color(0.96f, 0.38f, 0.4f, 1f)
                : new Color(0.6f, 0.64f, 0.7f, 1f);
        }

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
            return started;
        }

        return false;
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
        text.fontSize = 22f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;

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
        percentText.fontSize = 20f;
        percentText.color = Color.white;
        percentText.alignment = TextAlignmentOptions.Right;

        if (referenceText != null)
        {
            percentText.font = referenceText.font;
            percentText.fontSharedMaterial = referenceText.fontSharedMaterial;
        }
    }
}
