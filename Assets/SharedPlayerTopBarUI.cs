using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SharedPlayerTopBarUI : MonoBehaviour
{
    const string CoinResourcePath = "UI/icon_astrons_coin_white";

    GameObject statBannerObject;
    Image astronsIcon;
    TMP_Text astronsLabelText;
    bool editableNickname;
    Sprite cachedCoinSprite;

    public TMP_InputField NicknameInput { get; private set; }
    public TMP_Text NicknameText { get; private set; }
    public TMP_Text GamesText { get; private set; }
    public TMP_Text LevelXpText { get; private set; }
    public TMP_Text AstronsText { get; private set; }
    public TMP_Text AccountText { get; private set; }

    public static SharedPlayerTopBarUI Ensure(GameObject rootObject, bool editableNickname)
    {
        if (rootObject == null)
            return null;

        SharedPlayerTopBarUI topBar = rootObject.GetComponent<SharedPlayerTopBarUI>();
        if (topBar == null)
            topBar = rootObject.AddComponent<SharedPlayerTopBarUI>();

        topBar.Configure(editableNickname);
        return topBar;
    }

    public void Configure(bool editable)
    {
        editableNickname = editable;
        ConfigureRootImage();
        HideLegacyChildren();

        statBannerObject = EnsureChild("SharedTopStatBanner", typeof(RectTransform), typeof(Image));
        EnsureBannerChild("InnerPanel");
        EnsureBannerChild("TopAccent");
        EnsureBannerChild("BottomAccent");
        EnsureBannerChild("LeftAccent");
        EnsureBannerChild("RightAccent");

        GamesText = EnsureText("SharedTopBarGames", TextAlignmentOptions.Left, 30f);
        LevelXpText = EnsureText("SharedTopBarLevelXp", TextAlignmentOptions.Left, 30f);
        astronsLabelText = EnsureText("SharedTopBarAstronsLabel", TextAlignmentOptions.Left, 30f);
        astronsLabelText.text = "Astrons:";
        AstronsText = EnsureText("SharedTopBarAstrons", TextAlignmentOptions.Left, 30f);
        astronsIcon = EnsureImage("SharedTopBarAstronsIcon");
        if (astronsIcon != null)
        {
            astronsIcon.sprite = LoadCoinSprite();
            astronsIcon.color = Color.white;
            astronsIcon.preserveAspect = true;
            astronsIcon.raycastTarget = false;
        }

        if (editableNickname)
        {
            NicknameInput = EnsureNicknameInput();
            AccountText = EnsureText("SharedTopBarAccount", TextAlignmentOptions.Left, 22f);
            if (NicknameText != null)
                NicknameText.gameObject.SetActive(false);
        }
        else
        {
            NicknameText = EnsureText("SharedTopBarNickname", TextAlignmentOptions.Left, 30f);
            if (NicknameInput != null)
                NicknameInput.gameObject.SetActive(false);
            if (AccountText != null)
                AccountText.gameObject.SetActive(false);
        }
    }

    public void SetProfile(PlayerProfileData profile, string fallbackNickname, string accountLine, bool updateNicknameInput)
    {
        string nickname = profile != null && !string.IsNullOrWhiteSpace(profile.Nickname)
            ? profile.Nickname
            : string.IsNullOrWhiteSpace(fallbackNickname) ? "Pilot" : fallbackNickname;
        int games = profile != null ? Mathf.Max(0, profile.GamesPlayed) : 0;
        int totalXp = profile != null ? Mathf.Max(0, profile.TotalXp) : 0;
        int level = RoundXpBalance.GetLevelForTotalXp(totalXp);
        int astrons = profile != null ? Mathf.Max(0, profile.Astrons) : 0;

        if (editableNickname && NicknameInput != null && updateNicknameInput)
            NicknameInput.text = nickname;
        if (!editableNickname && NicknameText != null)
            NicknameText.text = nickname;
        if (GamesText != null)
            GamesText.text = "Games: " + games;
        if (LevelXpText != null)
            LevelXpText.text = "Level: " + level + "  XP: " + totalXp;
        if (AstronsText != null)
            AstronsText.text = astrons.ToString();
        if (AccountText != null)
            AccountText.text = string.IsNullOrWhiteSpace(accountLine) ? string.Empty : accountLine;
    }

    public void SetInteractable(bool interactable)
    {
        if (NicknameInput != null)
            NicknameInput.interactable = interactable;
    }

    public void Layout(float rootWidth)
    {
        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(Mathf.Max(820f, rootWidth), 110f);
        }

        if (editableNickname)
            LayoutProfile(rootWidth);
        else
            LayoutLobby(rootWidth);
    }

    void LayoutProfile(float rootWidth)
    {
        float bannerX = 370f;
        float bannerWidth = 1048f;
        LayoutBanner(bannerX, bannerWidth);

        LayoutInput(NicknameInput, new Vector2(34f, -42f), new Vector2(310f, 54f), 30f);
        LayoutText(AccountText, new Vector2(42f, -92f), new Vector2(360f, 28f), 22f, 18f, 22f, TextAlignmentOptions.Left);
        LayoutText(GamesText, new Vector2(394f, -40f), new Vector2(170f, 42f), 34f, 24f, 34f, TextAlignmentOptions.Left);
        LayoutText(LevelXpText, new Vector2(592f, -40f), new Vector2(420f, 42f), 34f, 24f, 34f, TextAlignmentOptions.Left);
        LayoutAstrons(new Vector2(1090f, -40f), new Vector2(310f, 42f), 34f, 24f, 34f, 34f);
    }

    void LayoutLobby(float rootWidth)
    {
        float bannerX = 168f;
        float bannerWidth = Mathf.Clamp(rootWidth - bannerX - 270f, 620f, 1120f);
        LayoutBanner(bannerX, bannerWidth);

        LayoutText(NicknameText, new Vector2(26f, -40f), new Vector2(140f, 42f), 30f, 22f, 30f, TextAlignmentOptions.Left);
        LayoutText(GamesText, new Vector2(bannerX + 22f, -40f), new Vector2(165f, 42f), 30f, 22f, 30f, TextAlignmentOptions.Left);
        LayoutText(LevelXpText, new Vector2(bannerX + 210f, -40f), new Vector2(380f, 42f), 30f, 22f, 30f, TextAlignmentOptions.Left);
        LayoutAstrons(new Vector2(bannerX + bannerWidth - 288f, -40f), new Vector2(270f, 42f), 30f, 22f, 30f, 30f);
    }

    void LayoutBanner(float bannerX, float bannerWidth)
    {
        if (statBannerObject == null)
            return;

        RectTransform rect = statBannerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(bannerX, -40f);
        rect.sizeDelta = new Vector2(bannerWidth, 58f);

        Image frame = statBannerObject.GetComponent<Image>();
        frame.color = new Color(0.33f, 0.39f, 0.47f, 0.94f);
        frame.raycastTarget = false;

        Transform innerPanel = statBannerObject.transform.Find("InnerPanel");
        if (innerPanel != null)
        {
            RectTransform innerRect = innerPanel.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.offsetMin = new Vector2(8f, 8f);
            innerRect.offsetMax = new Vector2(-8f, -8f);

            Image innerImage = innerPanel.GetComponent<Image>();
            innerImage.color = new Color(0.05f, 0.09f, 0.13f, 0.78f);
            innerImage.raycastTarget = false;
        }

        ConfigureAccent("TopAccent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(128f, 4f), new Color(0.35f, 0.82f, 1f, 0.32f));
        ConfigureAccent("BottomAccent", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(96f, 3f), new Color(0.35f, 0.82f, 1f, 0.24f));
        ConfigureAccent("LeftAccent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));
        ConfigureAccent("RightAccent", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));

        statBannerObject.transform.SetAsFirstSibling();
    }

    void LayoutAstrons(Vector2 position, Vector2 size, float fontSize, float minFontSize, float maxFontSize, float iconSize)
    {
        if (astronsLabelText == null || astronsIcon == null || AstronsText == null)
            return;

        float labelWidth = Mathf.Min(fontSize >= 34f ? 138f : 120f, size.x * 0.46f);
        LayoutText(astronsLabelText, position, new Vector2(labelWidth, size.y), fontSize, minFontSize, maxFontSize, TextAlignmentOptions.Left);

        astronsLabelText.ForceMeshUpdate();
        float usedLabelWidth = Mathf.Clamp(astronsLabelText.preferredWidth, 0f, labelWidth);
        float iconX = position.x + usedLabelWidth + (iconSize * 0.5f) + 8f;

        RectTransform iconRect = astronsIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(iconX, position.y);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        astronsIcon.gameObject.SetActive(true);

        float valueX = iconX + (iconSize * 0.5f) + 8f;
        float valueWidth = Mathf.Max(48f, position.x + size.x - valueX);
        LayoutText(AstronsText, new Vector2(valueX, position.y), new Vector2(valueWidth, size.y), fontSize, minFontSize, maxFontSize, TextAlignmentOptions.Left);
        AstronsText.ForceMeshUpdate();

        astronsLabelText.transform.SetAsLastSibling();
        astronsIcon.transform.SetAsLastSibling();
        AstronsText.transform.SetAsLastSibling();
    }

    void LayoutText(TMP_Text text, Vector2 position, Vector2 size, float fontSize, float minFontSize, float maxFontSize, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = minFontSize;
        text.fontSizeMax = maxFontSize;
        text.alignment = alignment;
        text.gameObject.SetActive(true);
    }

    void LayoutInput(TMP_InputField input, Vector2 position, Vector2 size, float fontSize)
    {
        if (input == null)
            return;

        RectTransform rect = input.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        input.gameObject.SetActive(true);

        if (input.textComponent != null)
            input.textComponent.fontSize = fontSize;
        if (input.placeholder is TMP_Text placeholder)
            placeholder.fontSize = fontSize;
    }

    void ConfigureAccent(string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        if (statBannerObject == null)
            return;

        Transform child = statBannerObject.transform.Find(childName);
        if (child == null)
            return;

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    TMP_Text EnsureText(string objectName, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject textObject = EnsureChild(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (text.text == "New Text")
            text.text = string.Empty;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.95f, 0.98f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 0f;
        text.raycastTarget = false;
        ApplyReferenceFont(text);
        return text;
    }

    TMP_InputField EnsureNicknameInput()
    {
        GameObject inputObject = EnsureChild("SharedTopBarNicknameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        Image inputBackground = inputObject.GetComponent<Image>();
        inputBackground.color = new Color(0.15f, 0.2f, 0.27f, 0.98f);

        GameObject viewport = EnsureChild(inputObject.transform, "Text Area", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12f, 8f);
        viewportRect.offsetMax = new Vector2(-12f, -8f);

        TMP_Text placeholder = EnsureText(viewport.transform, "Placeholder", "Nickname", TextAlignmentOptions.Left, 20f);
        placeholder.color = new Color(0.74f, 0.79f, 0.86f, 0.5f);
        TMP_Text inputText = EnsureText(viewport.transform, "Text", string.Empty, TextAlignmentOptions.Left, 20f);
        inputText.color = new Color(0.96f, 0.98f, 1f, 1f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.targetGraphic = inputBackground;
        input.textViewport = viewportRect;
        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterLimit = 18;
        input.gameObject.SetActive(true);
        return input;
    }

    TMP_Text EnsureText(Transform parent, string objectName, string value, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject textObject = EnsureChild(parent, objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.95f, 0.98f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 0f;
        ApplyReferenceFont(text);
        return text;
    }

    Image EnsureImage(string objectName)
    {
        GameObject imageObject = EnsureChild(objectName, typeof(RectTransform), typeof(Image));
        return imageObject.GetComponent<Image>();
    }

    GameObject EnsureBannerChild(string objectName)
    {
        return EnsureChild(statBannerObject.transform, objectName, typeof(RectTransform), typeof(Image));
    }

    GameObject EnsureChild(string objectName, params System.Type[] components)
    {
        return EnsureChild(transform, objectName, components);
    }

    GameObject EnsureChild(Transform parent, string objectName, params System.Type[] components)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(objectName, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    void ConfigureRootImage()
    {
        Image image = GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();

        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
    }

    void HideLegacyChildren()
    {
        string[] legacyNames =
        {
            "ProfileTitle",
            "AccountText",
            "GamesPlayedText",
            "TotalXpText",
            "AstronsText",
            "NicknameLabel",
            "NicknameInput",
            "ProfileTopStatBanner",
            "LobbyTopBarNickname",
            "LobbyTopBarGames",
            "LobbyTopBarLevelXp",
            "LobbyTopBarAstrons",
            "LobbyTopStatBanner"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            Transform child = transform.Find(legacyNames[i]);
            if (child != null)
                child.gameObject.SetActive(false);
        }
    }

    Sprite LoadCoinSprite()
    {
        if (cachedCoinSprite != null)
            return cachedCoinSprite;

        cachedCoinSprite = Resources.Load<Sprite>(CoinResourcePath);
        if (cachedCoinSprite != null)
            return cachedCoinSprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(CoinResourcePath);
        if (sprites != null && sprites.Length > 0)
            cachedCoinSprite = sprites[0];

        if (cachedCoinSprite != null)
            return cachedCoinSprite;

        Texture2D texture = Resources.Load<Texture2D>(CoinResourcePath);
        if (texture == null)
            return null;

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        cachedCoinSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        return cachedCoinSprite;
    }

    void ApplyReferenceFont(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference == null || reference == text)
            return;

        text.font = reference.font;
        text.fontSharedMaterial = reference.fontSharedMaterial;
    }
}
