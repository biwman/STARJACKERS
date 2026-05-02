using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UIRuntimeStyler : MonoBehaviour
{
    static UIRuntimeStyler instance;
#if UNITY_EDITOR
    double nextEditorRefreshTime;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    static void BootstrapInEditor()
    {
        EditorApplication.delayCall += EnsureEditorInstance;
    }
#endif

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = GameObject.Find("UIRuntimeStyler");
        if (root == null)
        {
            root = new GameObject("UIRuntimeStyler");
#if UNITY_EDITOR
            if (!Application.isPlaying)
                root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
#endif
        }

        instance = root.GetComponent<UIRuntimeStyler>();
        if (instance == null)
            instance = root.AddComponent<UIRuntimeStyler>();

        if (Application.isPlaying)
            DontDestroyOnLoad(root);
    }

#if UNITY_EDITOR
    static void EnsureEditorInstance()
    {
        if (Application.isPlaying)
            return;

        EnsureInstance();
        if (instance != null)
            instance.ApplyStylesImmediate();
    }
#endif

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
#endif

        if (Application.isPlaying)
            StartCoroutine(ApplyStylesDeferred());
        else
            ApplyStylesImmediate();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
#endif
        if (instance == this)
            instance = null;
    }

    void Start()
    {
        if (Application.isPlaying)
            StartCoroutine(ApplyStylesDeferred());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Application.isPlaying)
            StartCoroutine(ApplyStylesDeferred());
        else
            ApplyStylesImmediate();
    }

    IEnumerator ApplyStylesDeferred()
    {
        yield return null;
        yield return null;

        ApplyStylesImmediate();
    }

    void ApplyStylesImmediate()
    {
        StyleButtons();
        StyleJoysticks();
        StyleScoreText();
        StyleLobbyPanel();
        StyleEndScreen();
        StyleExtractionMessage();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (EditorApplication.timeSinceStartup < nextEditorRefreshTime)
                return;

            nextEditorRefreshTime = EditorApplication.timeSinceStartup + 0.75f;
            ApplyStylesImmediate();
        }
#endif
    }

#if UNITY_EDITOR
    void OnEditorHierarchyChanged()
    {
        if (Application.isPlaying)
            return;

        ApplyStylesImmediate();
    }
#endif

    void StyleButtons()
    {
        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!IsSceneObject(button.gameObject))
                continue;

            StyleButton(button);
        }
    }

    void StyleButton(Button button)
    {
        if (ShouldSkipManagedButtonStyle(button))
            return;

        Image image = button.GetComponent<Image>();
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        RectTransform rect = button.GetComponent<RectTransform>();

        if (image == null || rect == null)
            return;

        string role = ResolveButtonRole(button, text);
        Image useVisual = role == "use" ? GetOrCreateUseButtonVisual(button.transform) : null;

        image.type = Image.Type.Sliced;
        image.raycastTarget = true;
        button.transition = Selectable.Transition.ColorTint;

        Color baseColor = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        Color highlighted = new Color(0.24f, 0.29f, 0.36f, 1f);
        Color pressed = new Color(0.12f, 0.16f, 0.2f, 1f);
        Color textColor = Color.white;
        Vector2 targetSize = rect.sizeDelta;

        switch (role)
        {
            case "ready":
                baseColor = new Color(0.14f, 0.56f, 0.44f, 0.96f);
                highlighted = new Color(0.19f, 0.66f, 0.52f, 1f);
                pressed = new Color(0.09f, 0.39f, 0.3f, 1f);
                targetSize = new Vector2(220f, 62f);
                break;
            case "save_run":
                baseColor = new Color(0.08f, 0.58f, 0.18f, 1f);
                highlighted = new Color(0.12f, 0.68f, 0.23f, 1f);
                pressed = new Color(0.05f, 0.42f, 0.13f, 1f);
                targetSize = new Vector2(294f, 84f);
                break;
            case "restart":
                baseColor = new Color(0.85f, 0.45f, 0.18f, 0.96f);
                highlighted = new Color(0.93f, 0.55f, 0.26f, 1f);
                pressed = new Color(0.64f, 0.3f, 0.1f, 1f);
                targetSize = new Vector2(210f, 64f);
                break;
            case "reload":
                baseColor = new Color(0.23f, 0.56f, 0.9f, 0.96f);
                highlighted = new Color(0.34f, 0.66f, 0.98f, 1f);
                pressed = new Color(0.15f, 0.39f, 0.69f, 1f);
                targetSize = new Vector2(176f, 62f);
                break;
            case "use":
                baseColor = new Color(0.97f, 0.8f, 0.24f, 0.97f);
                highlighted = new Color(1f, 0.87f, 0.34f, 1f);
                pressed = new Color(0.82f, 0.61f, 0.1f, 1f);
                textColor = new Color(0.14f, 0.1f, 0.04f, 1f);
                targetSize = new Vector2(188f, 112f);
                break;
        }

        rect.sizeDelta = targetSize;
        if (role == "save_run")
            rect.anchoredPosition = new Vector2(296f, -816f);
        image.color = role == "use" ? new Color(1f, 1f, 1f, 0.02f) : baseColor;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = highlighted;
        colors.selectedColor = highlighted;
        colors.pressedColor = pressed;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (role != "use")
        {
            ApplyOutline(image.gameObject, new Color(0f, 0f, 0f, 0.24f), new Vector2(2f, -2f));
        }

        if (text != null)
        {
            text.color = textColor;
            text.fontSize = role == "use" ? 30f : role == "save_run" ? 30f : 26f;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = role == "use" ? 3f : role == "save_run" ? 5f : 5f;
            text.margin = new Vector4(12f, 6f, 12f, 6f);
        }

        if (role == "use")
        {
            ConfigureUseButtonVisual(button, useVisual, baseColor, highlighted, pressed);
        }
        else if (useVisual != null)
        {
            useVisual.gameObject.SetActive(false);
            button.targetGraphic = image;
        }
    }

    string ResolveButtonRole(Button button, TMP_Text text)
    {
        string name = button.gameObject.name.ToLowerInvariant();
        string label = text != null ? text.text.ToLowerInvariant() : string.Empty;

        if (name.Contains("ready") || label.Contains("ready"))
            return "ready";

        if (name.Contains("saveandrun") || label.Contains("save & run"))
            return "save_run";

        if (name.Contains("restart") || label.Contains("restart"))
            return "restart";

        if (label.Contains("use"))
            return "use";

        if (label.Contains("reload"))
            return "reload";

        return "default";
    }

    bool ShouldSkipManagedButtonStyle(Button button)
    {
        if (button == null)
            return true;

        string name = button.gameObject.name;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.StartsWith("PlayerSlot", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipSlot", System.StringComparison.Ordinal) ||
               name.StartsWith("CraftingSlot", System.StringComparison.Ordinal) ||
               name.StartsWith("MainGun", System.StringComparison.Ordinal) ||
               name.StartsWith("Shield", System.StringComparison.Ordinal) ||
               name.StartsWith("Engine", System.StringComparison.Ordinal) ||
               name.StartsWith("Gadget", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipInventoryHudSlot", System.StringComparison.Ordinal) ||
               name.StartsWith("RoomRow_", System.StringComparison.Ordinal) ||
               name.StartsWith("BrowserBackButton", System.StringComparison.Ordinal) ||
               name.StartsWith("BrowserRefreshButton", System.StringComparison.Ordinal) ||
               name.StartsWith("BrowserNewRoundButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipPreviewButton", System.StringComparison.Ordinal) ||
               name.StartsWith("CraftingCatalogButton", System.StringComparison.Ordinal) ||
               name.StartsWith("CraftingRecipeCloseButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShopButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShopBuyButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShopCloseButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ItemPreviewSellButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ItemPreviewSalvageButton", System.StringComparison.Ordinal) ||
               name.StartsWith("CraftButton", System.StringComparison.Ordinal);
    }

    void StyleJoysticks()
    {
        StyleJoystick("JoystickBG", new Color(0.08f, 0.11f, 0.16f, 0.38f), new Color(0.95f, 0.77f, 0.26f, 0.95f));
        StyleJoystick("ShootJoystickBG", new Color(0.12f, 0.09f, 0.14f, 0.4f), new Color(0.92f, 0.36f, 0.28f, 0.95f));
    }

    void StyleScoreText()
    {
        GameObject scoreObject = FindSceneObjectByName("ScoreText");
        if (scoreObject == null)
            return;

        TMP_Text text = scoreObject.GetComponent<TMP_Text>();
        RectTransform rect = scoreObject.GetComponent<RectTransform>();
        Image badge = GetOrCreateBackground(scoreObject.transform, "ScoreBadge");

        if (text != null)
        {
            if (text.text.StartsWith("Score"))
                text.text = "XP: 0";

            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.96f, 0.82f, 1f);
            text.characterSpacing = 0.5f;
            text.alignment = TextAlignmentOptions.TopRight;
            text.margin = new Vector4(18f, 8f, 18f, 8f);
        }

        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-28f, -22f);
            rect.sizeDelta = new Vector2(240f, 56f);
        }

        if (badge != null)
        {
            badge.color = new Color(0.07f, 0.1f, 0.14f, 0.78f);
            badge.type = Image.Type.Sliced;
        }

        ApplyOutline(scoreObject, new Color(0f, 0f, 0f, 0.5f), new Vector2(3f, -3f));
    }

    void StyleLobbyPanel()
    {
        GameObject lobbyPanel = FindSceneObjectByName("LobbyPanel");
        if (lobbyPanel == null)
            return;

        Image image = lobbyPanel.GetComponent<Image>();
        RectTransform rect = lobbyPanel.GetComponent<RectTransform>();

        if (image != null)
        {
            image.color = new Color(0.08f, 0.12f, 0.16f, 0.88f);
            image.type = Image.Type.Sliced;
            ApplyOutline(lobbyPanel, new Color(0f, 0f, 0f, 0.42f), new Vector2(6f, -6f));
        }

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(2100f, 1520f);
        }

        GameObject readyTextObject = FindSceneObjectByName("ReadyText");
        if (readyTextObject != null)
        {
            TMP_Text readyText = readyTextObject.GetComponent<TMP_Text>();
            if (readyText != null)
            {
                readyText.fontSize = 30f;
                readyText.fontStyle = FontStyles.Bold;
                readyText.characterSpacing = 4f;
                readyText.color = Color.white;
                readyText.alignment = TextAlignmentOptions.Center;
            }
        }

        GameObject readyButtonObject = FindSceneObjectByName("readyButton");
        if (readyButtonObject != null)
        {
            RectTransform buttonRect = readyButtonObject.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(0f, -460f);
                buttonRect.SetAsLastSibling();
            }
        }

        GameObject roomPlayersObject = FindSceneObjectByName("RoomPlayersText");
        if (roomPlayersObject != null)
        {
            TMP_Text roomPlayersText = roomPlayersObject.GetComponent<TMP_Text>();
            RectTransform playersRect = roomPlayersObject.GetComponent<RectTransform>();

            if (playersRect != null)
            {
                playersRect.anchorMin = new Vector2(0.5f, 1f);
                playersRect.anchorMax = new Vector2(0.5f, 1f);
                playersRect.pivot = new Vector2(0.5f, 1f);
                playersRect.anchoredPosition = new Vector2(0f, -26f);
                playersRect.sizeDelta = new Vector2(390f, 145f);
            }

            if (roomPlayersText != null)
            {
                roomPlayersText.fontSize = 23f;
                roomPlayersText.fontStyle = FontStyles.Bold;
                roomPlayersText.color = new Color(0.93f, 0.96f, 1f, 1f);
                roomPlayersText.alignment = TextAlignmentOptions.TopLeft;
                roomPlayersText.textWrappingMode = TextWrappingModes.NoWrap;
                roomPlayersText.lineSpacing = 8f;
                roomPlayersText.margin = new Vector4(12f, 10f, 12f, 10f);
            }
        }

        StyleLobbySettingButton("RoundSettingButton", new Vector2(-690f, -292f));
        StyleLobbySettingButton("MapSizeSettingButton", new Vector2(-290f, -292f));
        StyleLobbySettingButton("MapBackgroundSettingButton", new Vector2(-690f, -980f));
        StyleLobbySettingButton("ObstacleSettingButton", new Vector2(-690f, -378f));
        StyleLobbySettingButton("ObstacleDestroySettingButton", new Vector2(-690f, -1066f));
        StyleLobbySettingButton("ObstacleHpValueSettingButton", new Vector2(-290f, -1066f));
        StyleLobbySettingButton("ObstacleSizeSettingButton", new Vector2(-690f, -1152f));
        StyleLobbySettingButton("ObstacleNoBordersSettingButton", new Vector2(-290f, -1152f));
        StyleLobbySettingButton("TreasureSettingButton", new Vector2(-290f, -378f));
        StyleLobbySettingButton("NebulaSettingButton", new Vector2(-690f, -464f));
        StyleLobbySettingButton("ExtractionSettingButton", new Vector2(-290f, -464f));
        StyleLobbySettingButton("BoosterSettingButton", new Vector2(-690f, -550f));
        StyleLobbySettingButton("AmmoSettingButton", new Vector2(-290f, -550f));
        StyleLobbySettingButton("BoosterDelaySettingButton", new Vector2(-690f, -636f));
        StyleLobbySettingButton("ShipDriftSettingButton", new Vector2(-290f, -636f));
        StyleLobbySettingButton("DeathTimerSettingButton", new Vector2(-690f, -722f));
        StyleLobbySettingButton("MovingObjectsSettingButton", new Vector2(-290f, -722f));
        StyleLobbySettingButton("BulletPushSettingButton", new Vector2(-690f, -808f));
        StyleLobbySettingButton("ObstacleWeightSettingButton", new Vector2(-290f, -808f));
        StyleLobbySettingButton("TreasureWeightSettingButton", new Vector2(-690f, -894f));

        foreach (GameObject sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (sceneObject == null || !IsSceneObject(sceneObject) || !sceneObject.name.StartsWith("EnemySettingButton_"))
                continue;

            RectTransform buttonRect = sceneObject.GetComponent<RectTransform>();
            Vector2 anchoredPosition = buttonRect != null ? buttonRect.anchoredPosition : Vector2.zero;
            StyleLobbySettingButton(sceneObject.name, anchoredPosition);
        }
    }

    void StyleLobbySettingButton(string objectName, Vector2 anchoredPosition)
    {
        GameObject buttonObject = FindSceneObjectByName(objectName);
        if (buttonObject == null)
            return;

        Button button = buttonObject.GetComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>(true);

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 60f);
        }

        if (image != null)
        {
            image.type = Image.Type.Sliced;
            image.color = button != null && !button.interactable
                ? new Color(0.12f, 0.14f, 0.18f, 0.72f)
                : new Color(0.16f, 0.2f, 0.27f, 0.95f);
        }

        if (text != null)
        {
            text.fontSize = 17f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.95f, 0.97f, 1f, 1f);
            text.margin = new Vector4(8f, 4f, 8f, 4f);
            text.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    void StyleEndScreen()
    {
        GameObject endScreen = FindSceneObjectByName("EndScreen");
        if (endScreen != null)
        {
            RectTransform rect = endScreen.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(760f, 560f);
            }
        }

        GameObject gameOverPanel = FindSceneObjectByName("GameOver");
        if (gameOverPanel != null)
        {
            Image image = gameOverPanel.GetComponent<Image>();
            RectTransform rect = gameOverPanel.GetComponent<RectTransform>();

            if (image != null)
            {
                image.color = new Color(0.94f, 0.94f, 0.9f, 0.98f);
                image.type = Image.Type.Sliced;
                ApplyOutline(gameOverPanel, new Color(0f, 0f, 0f, 0.22f), new Vector2(6f, -6f));
            }

            if (rect != null)
            {
                rect.sizeDelta = new Vector2(760f, 560f);
            }
        }

        GameObject endMessageTextObject = FindSceneObjectByName("EndMessageText");
        if (endMessageTextObject != null)
        {
            TMP_Text text = endMessageTextObject.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.fontSize = 40f;
                text.fontStyle = FontStyles.Bold;
                text.color = new Color(0.15f, 0.18f, 0.24f, 1f);
                text.alignment = TextAlignmentOptions.Center;
                text.characterSpacing = 2f;
            }
        }
    }

    void StyleExtractionMessage()
    {
        GameObject messageObject = FindSceneObjectByName("ExtractionMessage");
        if (messageObject == null)
            return;

        TMP_Text text = messageObject.GetComponent<TMP_Text>();
        if (text == null)
            text = messageObject.GetComponentInChildren<TMP_Text>(true);

        RectTransform rect = messageObject.GetComponent<RectTransform>();
        Image badge = GetOrCreateBackground(messageObject.transform, "ExtractionMessageBadge");

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -222f);
            rect.sizeDelta = new Vector2(560f, 78f);
            rect.SetAsLastSibling();
        }

        if (messageObject.activeSelf && !Application.isPlaying)
            messageObject.SetActive(false);

        if (badge != null)
        {
            badge.color = new Color(0.06f, 0.12f, 0.18f, 0.82f);
            badge.type = Image.Type.Sliced;
        }

        if (text != null)
        {
            text.text = "Extraction Zone Activated";
            text.fontSize = 28f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.9f, 1f, 0.97f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.characterSpacing = 1.1f;
            text.margin = new Vector4(18f, 8f, 18f, 8f);
        }

        ApplyOutline(messageObject, new Color(0f, 0f, 0f, 0.4f), new Vector2(3f, -3f));
    }

    void StyleJoystick(string objectName, Color backgroundColor, Color handleColor)
    {
        GameObject root = GameObject.Find(objectName);
        if (root == null)
            return;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        RectTransform rootRect = root.GetComponent<RectTransform>();

        if (rootRect != null)
        {
            rootRect.sizeDelta = new Vector2(430f, 430f);
        }

        foreach (Image image in images)
        {
            RectTransform rect = image.GetComponent<RectTransform>();

            if (image.gameObject == root)
            {
                image.color = backgroundColor;
                ApplyOutline(image.gameObject, new Color(1f, 1f, 1f, 0.08f), new Vector2(2f, 2f));
                continue;
            }

            image.color = handleColor;
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(145f, 145f);
            }

            ApplyOutline(image.gameObject, new Color(0f, 0f, 0f, 0.28f), new Vector2(2f, -2f));
        }
    }

    void ApplyOutline(GameObject target, Color effectColor, Vector2 distance)
    {
        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = target.AddComponent<Shadow>();
        }

        shadow.effectColor = effectColor;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    void ConfigureUseButtonVisual(Button button, Image visual, Color baseColor, Color highlighted, Color pressed)
    {
        if (visual == null)
            return;

        RectTransform rect = visual.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        visual.sprite = null;
        visual.type = Image.Type.Sliced;
        visual.preserveAspect = false;
        visual.raycastTarget = false;

        visual.gameObject.SetActive(true);
        visual.color = baseColor;
        button.targetGraphic = visual;

        ApplyOutline(visual.gameObject, new Color(0f, 0f, 0f, 0.22f), new Vector2(2f, -2f));
    }

    bool IsSceneObject(GameObject obj)
    {
        return obj != null && obj.scene.IsValid();
    }

    GameObject FindSceneObjectByName(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name && IsSceneObject(go))
            {
                return go;
            }
        }

        return null;
    }

    Image GetOrCreateBackground(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject backgroundObject;

        if (existing != null)
        {
            backgroundObject = existing.gameObject;
        }
        else
        {
            backgroundObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            backgroundObject.transform.SetAsFirstSibling();
        }

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(-8f, -4f);
        rect.offsetMax = new Vector2(8f, 4f);

        return backgroundObject.GetComponent<Image>();
    }

    Image GetOrCreateUseButtonVisual(Transform parent)
    {
        Transform existing = parent.Find("UseButtonVisual");
        GameObject visualObject;

        if (existing != null)
        {
            visualObject = existing.gameObject;
        }
        else
        {
            visualObject = new GameObject("UseButtonVisual", typeof(RectTransform), typeof(Image));
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.SetAsFirstSibling();
        }

        Image image = visualObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
