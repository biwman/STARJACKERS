using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerShooting))]
public class AmmoUI : MonoBehaviourPun
{
    const string AmmoRootName = "AmmoCounter";
    const string AmmoLabelName = "AmmoLabel";
    const string AmmoValueName = "AmmoValue";

    PlayerShooting shooting;
    GameObject rootObject;
    Image backgroundImage;
    TextMeshProUGUI labelText;
    TextMeshProUGUI valueText;
    bool isVisible = true;
    RectTransform rootRect;
    RectTransform reloadButtonRect;
    RectTransform shootJoystickRect;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateCounter();
        RefreshCounter();
    }

    void Update()
    {
        UpdateVisibility();
        UpdateLayout();
        RefreshCounter();
    }

    void OnDestroy()
    {
        if (rootObject != null)
        {
            Destroy(rootObject);
        }
    }

    void CreateCounter()
    {
        GameObject existing = GameObject.Find(AmmoRootName);
        if (existing != null)
        {
            Destroy(existing);
        }

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        rootObject = new GameObject(AmmoRootName, typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(canvas.transform, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-148f, 292f);
        rootRect.sizeDelta = new Vector2(210f, 42f);

        backgroundImage = rootObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.07f, 0.1f, 0.14f, 0.92f);
        backgroundImage.type = Image.Type.Sliced;

        labelText = CreateText(AmmoLabelName, new Vector2(0f, 0f), TextAlignmentOptions.Left);
        labelText.text = "AMMO:";

        valueText = CreateText(AmmoValueName, new Vector2(0f, 0f), TextAlignmentOptions.Right);
        UpdateLayout();
    }

    void RefreshCounter()
    {
        if (shooting == null || valueText == null || backgroundImage == null)
            return;

        if (shooting.IsReloading)
        {
            float secondsLeft = Mathf.Max(0f, shooting.reloadDuration * (1f - shooting.ReloadProgress));
            valueText.text = "RLD " + secondsLeft.ToString("0.0") + "s";
            valueText.color = new Color(1f, 0.78f, 0.28f, 1f);
            backgroundImage.color = new Color(0.16f, 0.12f, 0.07f, 0.94f);
        }
        else
        {
            valueText.text = shooting.CurrentAmmo + "/" + shooting.MaxAmmo;
            valueText.color = shooting.CurrentAmmo <= 3
                ? new Color(1f, 0.45f, 0.35f, 1f)
                : Color.white;
            backgroundImage.color = new Color(0.07f, 0.1f, 0.14f, 0.92f);
        }
    }

    TextMeshProUGUI CreateText(string objectName, Vector2 anchoredPosition, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rootObject.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.margin = new Vector4(10f, 5f, 10f, 5f);

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return text;
    }

    void UpdateLayout()
    {
        if (rootRect == null)
            return;

        ResolveLayoutReferences();

        if (reloadButtonRect != null)
        {
            rootRect.anchorMin = reloadButtonRect.anchorMin;
            rootRect.anchorMax = reloadButtonRect.anchorMax;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = reloadButtonRect.anchoredPosition + new Vector2(0f, 78f);
            rootRect.sizeDelta = new Vector2(210f, 42f);
            return;
        }

        if (shootJoystickRect != null)
        {
            rootRect.anchorMin = shootJoystickRect.anchorMin;
            rootRect.anchorMax = shootJoystickRect.anchorMax;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = shootJoystickRect.anchoredPosition + new Vector2(0f, 296f);
            rootRect.sizeDelta = new Vector2(210f, 42f);
        }
    }

    void ResolveLayoutReferences()
    {
        if (reloadButtonRect == null)
        {
            GameObject reloadButton = GameObject.Find("ReloadButton");
            reloadButtonRect = reloadButton != null ? reloadButton.GetComponent<RectTransform>() : null;
        }

        if (shootJoystickRect == null)
        {
            GameObject shootJoystick = GameObject.Find("ShootJoystickBG");
            shootJoystickRect = shootJoystick != null ? shootJoystick.GetComponent<RectTransform>() : null;
        }
    }

    void UpdateVisibility()
    {
        if (rootObject == null)
            return;

        bool shouldBeVisible = IsGameplayHudVisible();
        if (isVisible == shouldBeVisible)
            return;

        isVisible = shouldBeVisible;
        rootObject.SetActive(shouldBeVisible);
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (RoomSettings.IsComplexShootingModel())
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return GameplayHudVisibility.IsGameplayHudVisible(started);
        }

        return false;
    }
}
