using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ToxicBorderDeathZoneWarningUI : MonoBehaviour
{
    const string WarningObjectName = "ToxicBorderDeathZoneWarning";
    const string WarningMessage = "LEAVE THE DEATH ZONE!!!";
    const float HideGraceSeconds = 0.18f;
    const float BlinkSpeed = 8.5f;
    const float ShakeMagnitude = 7f;
    const float WarningWidth = 620f;
    const float WarningHeight = 62f;
    const float TopOffset = -86f;

    static ToxicBorderDeathZoneWarningUI instance;

    RectTransform rectTransform;
    CanvasGroup canvasGroup;
    Image background;
    TextMeshProUGUI messageText;
    Vector2 baseAnchoredPosition;
    float lastShownTime = -1000f;
    bool visible;

    public static void ShowForFrame()
    {
        ToxicBorderDeathZoneWarningUI ui = EnsureExists();
        if (ui == null)
            return;

        ui.lastShownTime = Time.unscaledTime;
        ui.Show();
    }

    public static void HideImmediate()
    {
        if (instance != null)
            instance.Hide();
    }

    static ToxicBorderDeathZoneWarningUI EnsureExists()
    {
        if (instance != null)
            return instance;

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
            return null;

        Transform existing = canvas.transform.Find(WarningObjectName);
        GameObject warningObject = existing != null
            ? existing.gameObject
            : new GameObject(WarningObjectName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        warningObject.transform.SetParent(canvas.transform, false);

        ToxicBorderDeathZoneWarningUI ui = warningObject.GetComponent<ToxicBorderDeathZoneWarningUI>();
        if (ui == null)
            ui = warningObject.AddComponent<ToxicBorderDeathZoneWarningUI>();

        ui.Build();
        instance = ui;
        return instance;
    }

    static Canvas ResolveCanvas()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null && canvasObject.TryGetComponent(out Canvas namedCanvas))
            return namedCanvas;

        return FindAnyObjectByType<Canvas>();
    }

    void Awake()
    {
        instance = this;
        Build();
        Hide();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (!visible)
            return;

        if (Time.unscaledTime - lastShownTime > HideGraceSeconds)
        {
            Hide();
            return;
        }

        Animate();
    }

    void Build()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = new Vector2(WarningWidth, WarningHeight);
            baseAnchoredPosition = new Vector2(0f, TopOffset);
            rectTransform.anchoredPosition = baseAnchoredPosition;
        }

        background = GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.03f, 0.12f, 0.03f, 0.66f);
            background.raycastTarget = false;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        EnsureText();
        gameObject.SetActive(false);
    }

    void EnsureText()
    {
        Transform existing = transform.Find("Message");
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow), typeof(Outline));
        textObject.transform.SetParent(transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 0f);
            textRect.offsetMax = new Vector2(-20f, 0f);
        }

        messageText = textObject.GetComponent<TextMeshProUGUI>();
        messageText.text = WarningMessage;
        messageText.fontSize = 30f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.characterSpacing = 2.4f;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.textWrappingMode = TextWrappingModes.NoWrap;
        messageText.raycastTarget = false;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            messageText.font = reference.font;
            messageText.fontSharedMaterial = reference.fontSharedMaterial;
        }

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
        shadow.effectDistance = new Vector2(2.5f, -2.5f);

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.35f, 0.02f, 0.92f);
        outline.effectDistance = new Vector2(1.25f, -1.25f);
    }

    void Show()
    {
        visible = true;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        transform.SetAsLastSibling();
        Animate();
    }

    void Hide()
    {
        visible = false;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = baseAnchoredPosition;
            rectTransform.localScale = Vector3.one;
        }

        gameObject.SetActive(false);
    }

    void Animate()
    {
        float blink = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.unscaledTime * BlinkSpeed));
        float hardFlash = Mathf.PingPong(Time.unscaledTime * 3.6f, 1f);
        float alpha = Mathf.Lerp(0.45f, 1f, Mathf.Max(blink, hardFlash * 0.82f));

        if (canvasGroup != null)
            canvasGroup.alpha = alpha;

        if (background != null)
        {
            background.color = Color.Lerp(
                new Color(0.02f, 0.09f, 0.02f, 0.46f),
                new Color(0.13f, 0.34f, 0.04f, 0.78f),
                blink);
        }

        if (messageText != null)
        {
            messageText.color = Color.Lerp(
                new Color(0.72f, 1f, 0.2f, 0.96f),
                new Color(1f, 1f, 0.62f, 1f),
                blink);
        }

        if (rectTransform != null)
        {
            float shakeX = Mathf.Sin(Time.unscaledTime * 37f) * ShakeMagnitude * (0.3f + blink * 0.7f);
            float shakeY = Mathf.Cos(Time.unscaledTime * 29f) * ShakeMagnitude * 0.28f * blink;
            rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(shakeX, shakeY);
            rectTransform.localScale = Vector3.one * Mathf.Lerp(0.98f, 1.035f, blink);
        }
    }
}
