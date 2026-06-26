using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerHealth))]
public sealed class RoundVitalsIconHudUI : MonoBehaviourPun
{
    public const string RootName = "VitalsIconHud";

    const string HeartSpritePath = "UI/Vitals/round_vitals_heart";
    const string ShieldSpritePath = "UI/Vitals/round_vitals_shield";
    const string HeartFrameSpritePath = "UI/Vitals/round_vitals_heart_frame";
    const string ShieldFrameSpritePath = "UI/Vitals/round_vitals_shield_frame";
    const string HpBarName = "HP_Bar";
    const string ShieldBarName = "Shield_Bar";
    const float ScoreVerticalGap = 48f;
    const float DesktopScoreHorizontalOffset = -24f;
    const float MobileScoreHorizontalOffset = 72f;
    const float IconWidth = 142f;
    const float IconHeight = 140f;
    const float IconGap = 8f;
    const float GlyphSize = 124f;
    const float LegacyHideInterval = 0.5f;
    const float LayoutRefreshInterval = 0.2f;

    static readonly Color PanelColor = new Color(0f, 0f, 0f, 0f);
    static readonly Color FrameColor = new Color(0.006f, 0.01f, 0.014f, 0.98f);
    static readonly Color HpFillColor = new Color(0.24f, 0.86f, 0.38f, 1f);
    static readonly Color HpEmptyColor = new Color(0.025f, 0.065f, 0.045f, 0.95f);
    static readonly Color HpGlowColor = new Color(0.56f, 1f, 0.58f, 0.16f);
    static readonly Color ShieldFillColor = new Color(0.34f, 0.96f, 1f, 1f);
    static readonly Color ShieldEmptyColor = new Color(0.025f, 0.06f, 0.095f, 0.95f);
    static readonly Color ShieldGlowColor = new Color(0.36f, 0.9f, 1f, 0.18f);

    PlayerHealth health;
    RectTransform rootRect;
    CanvasGroup rootGroup;
    TMP_Text scoreText;
    VitalIconView hpView;
    VitalIconView shieldView;
    Sprite heartSprite;
    Sprite shieldSprite;
    Sprite heartFrameSprite;
    Sprite shieldFrameSprite;
    bool isVisible = true;
    float nextLegacyHideTime;
    float nextLayoutRefreshTime;

    sealed class VitalIconView
    {
        public GameObject Root;
        public RectTransform Rect;
        public Image Fill;
        public TMP_Text ValueText;
        public int LastCurrent = int.MinValue;
        public int LastMax = int.MinValue;
    }

    void Start()
    {
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        health = GetComponent<PlayerHealth>();
        LoadSprites();
        ResolveReferences();
        HideLegacyVitalBars();
        RefreshValues();
        UpdateVisibility();
    }

    void Update()
    {
        if (rootRect == null || scoreText == null)
            ResolveReferences();

        if (Time.unscaledTime >= nextLegacyHideTime)
        {
            nextLegacyHideTime = Time.unscaledTime + LegacyHideInterval;
            HideLegacyVitalBars();
        }

        if (Time.unscaledTime >= nextLayoutRefreshTime)
        {
            nextLayoutRefreshTime = Time.unscaledTime + LayoutRefreshInterval;
            ApplyLayout();
        }

        RefreshValues();
        UpdateVisibility();
    }

    void OnDisable()
    {
        if (photonView != null && photonView.IsMine)
            DestroyRoot();
    }

    void OnDestroy()
    {
        if (photonView != null && photonView.IsMine)
            DestroyRoot();
    }

    public static void HideLegacyVitalBars()
    {
        SetLegacyBarVisible(HpBarName, false);
        SetLegacyBarVisible(ShieldBarName, false);
    }

    public static void HideAllRuntimeObjects()
    {
        GameObject rootObject = FindSceneObject(RootName);
        if (rootObject != null)
            rootObject.SetActive(false);

        HideLegacyVitalBars();
    }

    public static void DestroyAllRuntimeObjects()
    {
        GameObject rootObject = FindSceneObject(RootName);
        if (rootObject != null)
            Destroy(rootObject);
    }

    void LoadSprites()
    {
        if (heartSprite == null)
            heartSprite = Resources.Load<Sprite>(HeartSpritePath);
        if (shieldSprite == null)
            shieldSprite = Resources.Load<Sprite>(ShieldSpritePath);
        if (heartFrameSprite == null)
            heartFrameSprite = Resources.Load<Sprite>(HeartFrameSpritePath);
        if (shieldFrameSprite == null)
            shieldFrameSprite = Resources.Load<Sprite>(ShieldFrameSpritePath);
    }

    void ResolveReferences()
    {
        if (scoreText == null)
            scoreText = FindText("ScoreText");

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
            return;

        Transform parent = scoreText != null && scoreText.transform.parent != null
            ? scoreText.transform.parent
            : canvas.transform;

        EnsureRoot(parent);
        ApplyLayout();
    }

    Canvas ResolveCanvas()
    {
        if (scoreText != null)
        {
            Canvas scoreCanvas = scoreText.GetComponentInParent<Canvas>();
            if (scoreCanvas != null)
                return scoreCanvas;
        }

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            Canvas namedCanvas = canvasObject.GetComponent<Canvas>();
            if (namedCanvas != null)
                return namedCanvas;
        }

        return FindAnyObjectByType<Canvas>();
    }

    void EnsureRoot(Transform parent)
    {
        Transform existing = parent.Find(RootName);
        GameObject rootObject;
        if (existing != null)
        {
            rootObject = existing.gameObject;
        }
        else
        {
            rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(parent, false);
        }

        if (rootObject.transform.parent != parent)
            rootObject.transform.SetParent(parent, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootGroup = rootObject.GetComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        rootGroup.ignoreParentGroups = false;

        hpView = EnsureVitalView(rootObject.transform, "VitalsHeart", heartSprite, heartFrameSprite, HpEmptyColor, HpGlowColor);
        shieldView = EnsureVitalView(rootObject.transform, "VitalsShield", shieldSprite, shieldFrameSprite, ShieldEmptyColor, ShieldGlowColor);
        hpView.LastCurrent = int.MinValue;
        shieldView.LastCurrent = int.MinValue;
        nextLayoutRefreshTime = 0f;

        rootObject.transform.SetAsLastSibling();
    }

    VitalIconView EnsureVitalView(Transform parent, string name, Sprite sprite, Sprite frameSprite, Color emptyColor, Color glowColor)
    {
        Transform existing = parent.Find(name);
        GameObject viewObject;
        if (existing != null)
        {
            viewObject = existing.gameObject;
        }
        else
        {
            viewObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            viewObject.transform.SetParent(parent, false);
        }

        RectTransform rect = viewObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(IconWidth, IconHeight);

        Image panel = viewObject.GetComponent<Image>();
        panel.color = PanelColor;
        panel.raycastTarget = false;

        Image glow = EnsureImage(viewObject.transform, "Glow");
        ConfigureGlyphImage(glow, sprite, glowColor, GlyphSize + 22f);

        Image empty = EnsureImage(viewObject.transform, "Empty");
        ConfigureGlyphImage(empty, sprite, emptyColor, GlyphSize);

        Image fill = EnsureImage(viewObject.transform, "Fill");
        ConfigureGlyphImage(fill, sprite, Color.white, GlyphSize);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 1f;

        Image frame = EnsureImage(viewObject.transform, "Frame");
        ConfigureGlyphImage(frame, frameSprite != null ? frameSprite : sprite, FrameColor, GlyphSize);
        ConfigureShapeDepth(frame.gameObject, glowColor);

        TMP_Text text = EnsureText(viewObject.transform, "Value");
        text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        text.rectTransform.sizeDelta = new Vector2(GlyphSize + 36f, 52f);
        text.transform.SetAsLastSibling();

        return new VitalIconView
        {
            Root = viewObject,
            Rect = rect,
            Fill = fill,
            ValueText = text
        };
    }

    void ConfigureGlyphImage(Image image, Sprite sprite, Color color, float size)
    {
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
    }

    void ApplyLayout()
    {
        if (rootRect == null)
            return;

        Vector2 rootSize = new Vector2(IconWidth * 2f + IconGap, IconHeight);
        if (scoreText != null)
        {
            RectTransform scoreRect = scoreText.rectTransform;
            rootRect.anchorMin = scoreRect.anchorMin;
            rootRect.anchorMax = scoreRect.anchorMax;
            rootRect.pivot = scoreRect.pivot;
            float horizontalOffset = MobilePerformanceSettings.UseReducedVfx ? MobileScoreHorizontalOffset : DesktopScoreHorizontalOffset;
            rootRect.anchoredPosition = scoreRect.anchoredPosition + new Vector2(horizontalOffset, -ScoreVerticalGap);
        }
        else
        {
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(MobilePerformanceSettings.UseReducedVfx ? 360f : 264f, -76f);
        }

        if (shieldView != null && health != null && health.MaxShield <= 0)
            rootSize.x = IconWidth;

        rootRect.sizeDelta = rootSize;

        if (hpView != null)
        {
            hpView.Rect.anchoredPosition = Vector2.zero;
            hpView.Root.SetActive(true);
        }

        if (shieldView != null)
        {
            shieldView.Rect.anchoredPosition = new Vector2(IconWidth + IconGap, 0f);
            shieldView.Root.SetActive(health == null || health.MaxShield > 0);
        }
    }

    void RefreshValues()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();

        if (health == null)
            return;

        RefreshView(hpView, health.CurrentHP, health.maxHP, HpFillColor);
        RefreshView(shieldView, health.CurrentShield, health.MaxShield, ShieldFillColor);
    }

    void RefreshView(VitalIconView view, int current, int max, Color fillColor)
    {
        if (view == null)
            return;

        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        if (view.LastCurrent == safeCurrent && view.LastMax == safeMax)
            return;

        view.LastCurrent = safeCurrent;
        view.LastMax = safeMax;

        float normalized = Mathf.Clamp01(safeCurrent / (float)safeMax);

        if (view.Fill != null)
        {
            view.Fill.fillAmount = normalized;
            view.Fill.color = fillColor;
        }

        if (view.ValueText != null)
            view.ValueText.text = safeCurrent + "/" + safeMax;
    }

    void UpdateVisibility()
    {
        if (rootRect == null)
            return;

        bool shouldBeVisible = health != null &&
                               PhotonNetwork.CurrentRoom != null &&
                               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
                               value is bool started &&
                               GameplayHudVisibility.IsGameplayHudVisible(started);

        if (isVisible != shouldBeVisible)
            isVisible = shouldBeVisible;

        if (rootRect.gameObject.activeSelf != shouldBeVisible)
            rootRect.gameObject.SetActive(shouldBeVisible);

        if (rootGroup != null)
            rootGroup.alpha = shouldBeVisible ? 1f : 0f;
    }

    Image EnsureImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject imageObject;
        if (existing != null)
        {
            imageObject = existing.gameObject;
        }
        else
        {
            imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
        }

        return imageObject.GetComponent<Image>();
    }

    TMP_Text EnsureText(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject textObject;
        if (existing != null)
        {
            textObject = existing.gameObject;
        }
        else
        {
            textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
        }

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.fontSize = 32f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 32f;
        text.color = new Color(0.96f, 1f, 0.98f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 0f;
        text.raycastTarget = false;
        ConfigureTextShadow(text.gameObject);
        ApplyReferenceFont(text);
        return text;
    }

    static TMP_Text FindText(string objectName)
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.gameObject.name == objectName)
                return text;
        }

        return null;
    }

    static GameObject FindSceneObject(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current != null && current.gameObject.name == objectName)
                return current.gameObject;
        }

        return null;
    }

    static void SetLegacyBarVisible(string objectName, bool visible)
    {
        GameObject obj = FindSceneObject(objectName);
        if (obj != null && obj.activeSelf != visible)
            obj.SetActive(visible);
    }

    void DestroyRoot()
    {
        if (rootRect != null)
        {
            Destroy(rootRect.gameObject);
            rootRect = null;
            rootGroup = null;
            hpView = null;
            shieldView = null;
            return;
        }

        DestroyAllRuntimeObjects();
    }

    void ConfigureShapeDepth(GameObject target, Color glowColor)
    {
        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
            shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.42f);
        outline.effectDistance = new Vector2(2.6f, 2.6f);
        outline.useGraphicAlpha = true;
    }

    void ConfigureTextShadow(GameObject target)
    {
        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
            shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
        shadow.effectDistance = new Vector2(1.6f, -1.6f);
        shadow.useGraphicAlpha = false;
    }

    void ApplyReferenceFont(TMP_Text text)
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text reference = texts[i];
            if (reference == null || reference == text || reference.font == null)
                continue;

            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
            return;
        }
    }
}
