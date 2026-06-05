using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIRuntimeStyler : MonoBehaviour
{
    static readonly ProfilerMarker ApplyStylesMarker = new ProfilerMarker("UIRuntimeStyler.ApplyStyles");

    static UIRuntimeStyler instance;
    static Sprite playButtonShapeSprite;
    static Sprite whitePixelSprite;
    static Sprite cachedPlayRocketSprite;
    static Sprite cachedCraftingIconSprite;
    static Sprite cachedTraderIconSprite;
    static Sprite cachedInventoryIconSprite;
    static Sprite cachedJoystickBoosterRingSprite;
    static bool styleRefreshQueued;

    readonly List<GameObject> sceneObjects = new List<GameObject>(256);
    readonly List<Button> sceneButtons = new List<Button>(96);
    readonly Dictionary<string, GameObject> sceneObjectsByName = new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
    bool sceneObjectCacheReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        if (!Application.isPlaying)
            return;

        GameObject root = GameObject.Find("UIRuntimeStyler");
        if (root == null)
        {
            root = new GameObject("UIRuntimeStyler");
        }

        instance = root.GetComponent<UIRuntimeStyler>();
        if (instance == null)
            instance = root.AddComponent<UIRuntimeStyler>();

        if (Application.isPlaying)
            DontDestroyOnLoad(root);
    }

    public static void RefreshStyles()
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        if (instance == null)
            return;

        instance.QueueStyleRefresh();
    }

    public static void PrewarmRuntimeSprites()
    {
        if (!Application.isPlaying)
            return;

        GetWhitePixelSprite();
        GetPlayButtonShapeSprite();
        LoadPlayRocketSprite();
        LoadRuntimeUiSprite("UI/icon_crafting", ref cachedCraftingIconSprite, "RuntimeCraftingIconSprite");
        LoadRuntimeUiSprite("UI/icon_trader", ref cachedTraderIconSprite, "RuntimeTraderIconSprite");
        LoadRuntimeUiSprite("UI/icon_inventory", ref cachedInventoryIconSprite, "RuntimeInventoryIconSprite");
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnEnable()
    {
        if (Application.isPlaying)
            QueueStyleRefresh();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
            styleRefreshQueued = false;
        }
    }

    void Start()
    {
        if (Application.isPlaying)
            QueueStyleRefresh();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Application.isPlaying)
            QueueStyleRefresh();
    }

    void QueueStyleRefresh()
    {
        if (styleRefreshQueued)
            return;

        styleRefreshQueued = true;
        StartCoroutine(ApplyStylesDeferred());
    }

    IEnumerator ApplyStylesDeferred()
    {
        yield return null;
        yield return null;

        ApplyStylesImmediate();
        styleRefreshQueued = false;
    }

    void ApplyStylesImmediate()
    {
        using (ApplyStylesMarker.Auto())
        {
            RebuildSceneObjectCache();
            try
            {
                StyleButtons();
                StyleJoysticks();
                StyleScoreText();
                StyleLobbyPanel();
                StyleEndScreen();
                StyleExtractionMessage();
            }
            finally
            {
                ClearSceneObjectCache();
            }
        }
    }

    void StyleButtons()
    {
        for (int i = 0; i < sceneButtons.Count; i++)
        {
            Button button = sceneButtons[i];
            if (button != null)
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
        Image framedVisual = IsFramedButtonRole(role) ? GetOrCreateSaveRunButtonVisual(button.transform) : null;

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
                baseColor = new Color(0.11f, 0.38f, 0.21f, 0.98f);
                highlighted = new Color(0.15f, 0.5f, 0.28f, 1f);
                pressed = new Color(0.08f, 0.24f, 0.15f, 1f);
                targetSize = new Vector2(396f, 114f);
                break;
            case "nav_crafting":
            case "nav_trader":
            case "nav_inventory":
            case "nav_back":
            case "nav_back_wide":
            case "skin_choice":
                baseColor = button.colors.normalColor;
                highlighted = button.colors.highlightedColor;
                pressed = button.colors.pressedColor;
                targetSize = role == "skin_choice"
                    ? new Vector2(196f, 56f)
                    : role == "nav_back_wide"
                        ? new Vector2(390f, 66f)
                    : role == "nav_back"
                        ? new Vector2(216f, 62f)
                        : new Vector2(326f, 88f);
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
                baseColor = new Color(0.035f, 0.07f, 0.1f, 0.98f);
                highlighted = new Color(0.06f, 0.12f, 0.16f, 1f);
                pressed = new Color(0.02f, 0.04f, 0.06f, 1f);
                textColor = new Color(0.94f, 0.98f, 1f, 1f);
                targetSize = new Vector2(196f, 104f);
                break;
        }

        rect.sizeDelta = targetSize;
        image.color = role == "use" || IsFramedButtonRole(role)
            ? new Color(1f, 1f, 1f, 0.02f)
            : baseColor;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = highlighted;
        colors.selectedColor = highlighted;
        colors.pressedColor = pressed;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (role != "use" && role != "save_run")
        {
            ApplyOutline(image.gameObject, new Color(0f, 0f, 0f, 0.24f), new Vector2(2f, -2f));
        }

        if (text != null)
        {
            text.color = textColor;
            text.fontSize = role == "use" ? 30f : role == "save_run" ? 30f : IsNavButtonRole(role) ? 26f : 26f;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = role == "use" ? 3f : role == "save_run" ? 5f : IsNavButtonRole(role) ? 3.5f : 5f;
            text.margin = new Vector4(12f, 6f, 12f, 6f);

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            if (role == "use")
            {
                text.fontSize = 28f;
                text.characterSpacing = 3f;
                text.alignment = TextAlignmentOptions.Center;
                textRect.offsetMin = new Vector2(22f, 12f);
                textRect.offsetMax = new Vector2(-22f, -12f);
                ApplyOutline(text.gameObject, new Color(0f, 0f, 0f, 0.48f), new Vector2(2f, -2f));
            }
            else if (role == "save_run")
            {
                text.fontSize = 40f;
                text.characterSpacing = 4.5f;
                text.alignment = TextAlignmentOptions.Center;
                textRect.offsetMin = new Vector2(118f, 10f);
                textRect.offsetMax = new Vector2(-28f, -10f);
                ApplyOutline(text.gameObject, new Color(0f, 0f, 0f, 0.48f), new Vector2(2f, -2f));
            }
            else if (IsNavButtonRole(role) || role == "skin_choice")
            {
                text.fontSize = role == "skin_choice" ? 20f : 26f;
                text.characterSpacing = role == "skin_choice" ? 2f : 3f;
                text.alignment = TextAlignmentOptions.Center;
                if (role == "nav_back")
                {
                    textRect.offsetMin = new Vector2(18f, 8f);
                    textRect.offsetMax = new Vector2(-18f, -8f);
                }
                else if (role == "nav_back_wide")
                {
                    text.fontSize = 22f;
                    text.characterSpacing = 2f;
                    textRect.offsetMin = new Vector2(24f, 8f);
                    textRect.offsetMax = new Vector2(-24f, -8f);
                }
                else if (role == "skin_choice")
                {
                    textRect.offsetMin = new Vector2(16f, 6f);
                    textRect.offsetMax = new Vector2(-16f, -6f);
                }
                else
                {
                    textRect.offsetMin = new Vector2(136f, 8f);
                    textRect.offsetMax = new Vector2(-24f, -8f);
                }
                ApplyOutline(text.gameObject, new Color(0f, 0f, 0f, 0.44f), new Vector2(2f, -2f));
            }
            else
            {
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }
        }

        if (role == "use")
        {
            ConfigureUseButtonVisual(button, useVisual, baseColor, highlighted, pressed);
        }
        else if (role == "save_run")
        {
            ConfigureSaveRunButtonVisual(button, framedVisual, baseColor, highlighted, pressed);
        }
        else if (IsNavButtonRole(role) || role == "skin_choice")
        {
            ConfigureNavButtonVisual(button, framedVisual, role, baseColor, highlighted, pressed);
        }
        else if (useVisual != null)
        {
            useVisual.gameObject.SetActive(false);
            button.targetGraphic = image;
        }
        else if (framedVisual != null)
        {
            framedVisual.gameObject.SetActive(false);
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

        if (name.Contains("profilecraftingnavbutton"))
            return "nav_crafting";

        if (name.Contains("profileinventorynavbutton"))
            return "nav_inventory";

        if (name.Contains("shopbutton"))
            return "nav_trader";

        if (name.Contains("developersettingswideprofilebackbutton"))
            return "nav_back_wide";

        if (name.Contains("shipselectionbackbutton") || name.Contains("profilebackbutton"))
            return "nav_back";

        if (name.Contains("shipselectionskinbutton"))
            return "skin_choice";

        if (name.Contains("collectbutton"))
            return "use";

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
               name.StartsWith("RecipeResultButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShopItemCardButton", System.StringComparison.Ordinal) ||
               name.StartsWith("MainGun", System.StringComparison.Ordinal) ||
               name.StartsWith("Shield", System.StringComparison.Ordinal) ||
               name.StartsWith("Engine", System.StringComparison.Ordinal) ||
               name.StartsWith("Gadget", System.StringComparison.Ordinal) ||
               name.StartsWith("Support", System.StringComparison.Ordinal) ||
               name.StartsWith("Rescue", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipInventoryButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipInventoryHudSlot", System.StringComparison.Ordinal) ||
               name.StartsWith("RoomRow_", System.StringComparison.Ordinal) ||
               name.StartsWith("BackButton", System.StringComparison.Ordinal) ||
               name.StartsWith("EarlyRoundSummaryBackButton", System.StringComparison.Ordinal) ||
               name.StartsWith("BrowserBackButton", System.StringComparison.Ordinal) ||
               name.StartsWith("BrowserRefreshButton", System.StringComparison.Ordinal) ||
               name.StartsWith("BrowserNewRoundButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipPreviewButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipPreviewHitbox", System.StringComparison.Ordinal) ||
               name.StartsWith("CraftingCatalogButton", System.StringComparison.Ordinal) ||
               name.StartsWith("CraftingRecipeCloseButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShipInventoryUnloadButton", System.StringComparison.Ordinal) ||
               name.StartsWith("PlayerInventoryFilterButton", System.StringComparison.Ordinal) ||
               name.StartsWith("PlayerInventoryExtendButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShopBuyButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShopSortButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ShopCloseButton", System.StringComparison.Ordinal) ||
               name.StartsWith("EnemySettingButton_", System.StringComparison.Ordinal) ||
               name.StartsWith("ItemPreviewInfoButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ItemPreviewSellButton", System.StringComparison.Ordinal) ||
               name.StartsWith("ItemPreviewSalvageButton", System.StringComparison.Ordinal) ||
               name.StartsWith("CraftButton", System.StringComparison.Ordinal) ||
               name.StartsWith("FullScreenMapTile_", System.StringComparison.Ordinal);
    }

    void StyleJoysticks()
    {
        StyleMovementJoystick("JoystickBG");
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

            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.96f, 0.82f, 1f);
            text.characterSpacing = 0f;
            text.alignment = TextAlignmentOptions.Left;
            text.margin = new Vector4(10f, 4f, 10f, 4f);
        }

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(264f, -28f);
            rect.sizeDelta = new Vector2(210f, 42f);
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
        StyleLobbySettingButton("BoosterDelaySettingButton", new Vector2(-690f, -636f));
        StyleLobbySettingButton("ShipDriftSettingButton", new Vector2(-290f, -636f));
        StyleLobbySettingButton("DeathTimerSettingButton", new Vector2(-690f, -722f));
        StyleLobbySettingButton("MovingObjectsSettingButton", new Vector2(-290f, -722f));
        StyleLobbySettingButton("GravityWellPhysicsSettingButton", new Vector2(-290f, -894f));
        StyleLobbySettingButton("BulletPushSettingButton", new Vector2(-690f, -808f));
        StyleLobbySettingButton("ObstacleWeightSettingButton", new Vector2(-290f, -808f));
        StyleLobbySettingButton("TreasureWeightSettingButton", new Vector2(-690f, -894f));

        for (int i = 0; i < sceneObjects.Count; i++)
        {
            GameObject sceneObject = sceneObjects[i];
            if (sceneObject == null || !sceneObject.name.StartsWith("EnemySettingButton_"))
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

        for (Transform current = buttonObject.transform; current != null; current = current.parent)
        {
            if (current.name == "LobbyFullScreenRoot" || current.name == "LobbyDeveloperSettingsScreen")
                return;
        }

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
                rect.sizeDelta = new Vector2(900f, 600f);
            }
        }

        GameObject gameOverPanel = FindSceneObjectByName("GameOver");
        if (gameOverPanel != null)
        {
            Image image = gameOverPanel.GetComponent<Image>();
            RectTransform rect = gameOverPanel.GetComponent<RectTransform>();

            if (image != null)
            {
                image.color = new Color(0.035f, 0.055f, 0.082f, 0.98f);
                image.type = Image.Type.Sliced;
                ApplyOutline(gameOverPanel, new Color(0f, 0f, 0f, 0.34f), new Vector2(6f, -6f));
            }

            if (rect != null)
            {
                rect.sizeDelta = new Vector2(900f, 600f);
            }
        }

        GameObject endMessageTextObject = FindSceneObjectByName("EndMessageText");
        if (endMessageTextObject != null)
        {
            TMP_Text text = endMessageTextObject.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.fontSize = 42f;
                text.fontStyle = FontStyles.Bold;
                text.color = new Color(0.92f, 0.98f, 1f, 1f);
                text.alignment = TextAlignmentOptions.Left;
                text.characterSpacing = 1.6f;
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

    void StyleMovementJoystick(string objectName)
    {
        GameObject root = FindSceneObjectByName(objectName);
        if (root == null)
            return;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        if (rootRect != null)
            rootRect.sizeDelta = new Vector2(430f, 430f);

        Image rootImage = root.GetComponent<Image>();
        Sprite joystickSprite = rootImage != null ? rootImage.sprite : null;
        if (rootImage != null)
        {
            rootImage.color = new Color(0.035f, 0.07f, 0.1f, 0.42f);
            rootImage.raycastTarget = true;
            ApplyOutline(rootImage.gameObject, new Color(0f, 0f, 0f, 0.28f), new Vector2(2f, -2f));
        }

        Joystick joystick = root.GetComponent<Joystick>();
        if (joystick != null)
        {
            joystick.deadZone = 0.12f;
            joystick.rescaleInputAfterDeadZone = true;
            joystick.responseExponent = 1.08f;
        }

        Graphic boosterRing = GetOrCreateMovementJoystickBoosterRing(root.transform);

        Image glow = GetOrCreateChildImage(root.transform, "MovementJoystickGlow");
        ConfigureJoystickDisc(glow, joystickSprite, 392f, new Color(0.35f, 0.82f, 1f, 0.05f));
        glow.transform.SetSiblingIndex(Mathf.Min(1, glow.transform.parent.childCount - 1));

        Image ring = GetOrCreateChildImage(root.transform, "MovementJoystickRing");
        ConfigureJoystickDisc(ring, joystickSprite, 344f, new Color(0.32f, 0.62f, 0.78f, 0.22f));
        ring.transform.SetSiblingIndex(Mathf.Min(2, ring.transform.parent.childCount - 1));

        Image inner = GetOrCreateChildImage(root.transform, "MovementJoystickInner");
        ConfigureJoystickDisc(inner, joystickSprite, 276f, new Color(0.02f, 0.04f, 0.065f, 0.3f));
        inner.transform.SetSiblingIndex(Mathf.Min(3, inner.transform.parent.childCount - 1));

        RemoveMovementJoystickBoosterDebug(root.transform);

        Image handleImage = joystick != null && joystick.handle != null ? joystick.handle.GetComponent<Image>() : null;
        if (handleImage == null)
        {
            Transform handleTransform = root.transform.Find("Handle");
            handleImage = handleTransform != null ? handleTransform.GetComponent<Image>() : null;
        }

        if (handleImage != null)
        {
            RectTransform handleRect = handleImage.rectTransform;
            handleRect.sizeDelta = new Vector2(128f, 128f);
            handleRect.localScale = Vector3.one;
            if (joystickSprite != null && handleImage.sprite == null)
                handleImage.sprite = joystickSprite;
            handleImage.color = new Color(0.11f, 0.38f, 0.21f, 0.9f);
            handleImage.raycastTarget = false;
            handleImage.transform.SetAsLastSibling();
            ApplyOutline(handleImage.gameObject, new Color(0f, 0f, 0f, 0.34f), new Vector2(2f, -2f));
        }

        MovementJoystickVisualController controller = root.GetComponent<MovementJoystickVisualController>();
        if (controller == null)
            controller = root.AddComponent<MovementJoystickVisualController>();
        controller.Configure(joystick, rootImage, handleImage, glow, ring, inner, boosterRing);
    }

    Graphic GetOrCreateMovementJoystickBoosterRing(Transform joystickRoot)
    {
        Transform parent = joystickRoot;
        Transform existing = joystickRoot.Find("MovementJoystickBoosterRing");
        if (existing == null)
        {
            Transform oldSibling = joystickRoot.parent != null
                ? joystickRoot.parent.Find("MovementJoystickBoosterRing")
                : null;
            existing = oldSibling;
        }

        GameObject ringObject = existing != null
            ? existing.gameObject
            : new GameObject("MovementJoystickBoosterRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(JoystickBoosterRingGraphic));
        if (ringObject.transform.parent != parent)
            ringObject.transform.SetParent(parent, false);

        Image staleImage = ringObject.GetComponent<Image>();
        if (staleImage != null)
        {
            staleImage.enabled = false;
            Destroy(staleImage);
        }

        JoystickBoosterRingGraphic ringGraphic = ringObject.GetComponent<JoystickBoosterRingGraphic>();
        if (ringGraphic == null)
            ringGraphic = ringObject.AddComponent<JoystickBoosterRingGraphic>();

        RectTransform rect = ringObject.GetComponent<RectTransform>();
        RectTransform rootRect = joystickRoot as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = rootRect != null
            ? rootRect.sizeDelta * PlayerMovement.AdvancedBoosterOuterInputLimit
            : new Vector2(516f, 516f);

        ringGraphic.color = new Color(1f, 0.04f, 0.02f, 0f);
        ringGraphic.raycastTarget = false;
        ringGraphic.maskable = false;
        ringObject.SetActive(true);

        ringObject.transform.SetAsFirstSibling();

        return ringGraphic;
    }

    void RemoveMovementJoystickBoosterDebug(Transform parent)
    {
        Transform existing = parent.Find("MovementJoystickBoosterDebug");
        if (existing != null)
            Destroy(existing.gameObject);
    }

    void ConfigureJoystickDisc(Image image, Sprite sprite, float size, Color color)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        rect.localScale = Vector3.one;

        image.sprite = sprite != null ? sprite : GetWhitePixelSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.color = color;
    }

    void StyleJoystick(string objectName, Color backgroundColor, Color handleColor)
    {
        GameObject root = FindSceneObjectByName(objectName);
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

    static void SetChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        if (child != null)
            child.gameObject.SetActive(active);
    }

    void ConfigureUseButtonVisual(Button button, Image visual, Color baseColor, Color highlighted, Color pressed)
    {
        if (visual == null)
            return;

        RectTransform rootRect = visual.rectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        visual.sprite = GetPlayButtonShapeSprite();
        visual.type = Image.Type.Simple;
        visual.preserveAspect = false;
        visual.raycastTarget = false;
        visual.gameObject.SetActive(true);
        visual.color = button.interactable
            ? new Color(0.36f, 0.42f, 0.5f, 0.98f)
            : new Color(0.2f, 0.22f, 0.25f, 0.72f);
        visual.transform.SetAsFirstSibling();

        SetChildActive(visual.transform, "UseButtonGlow", false);
        SetChildActive(visual.transform, "UseButtonAmberCore", false);
        SetChildActive(visual.transform, "UseButtonIconRoot", false);

        UseButtonPulseUI pulse = visual.GetComponent<UseButtonPulseUI>();
        if (pulse != null)
            pulse.enabled = false;

        Image panel = GetOrCreateChildImage(visual.transform, "UseButtonInnerPanel");
        panel.gameObject.SetActive(true);
        panel.rectTransform.anchorMin = Vector2.zero;
        panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.offsetMin = new Vector2(8f, 8f);
        panel.rectTransform.offsetMax = new Vector2(-8f, -8f);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        panel.sprite = GetPlayButtonShapeSprite();
        panel.type = Image.Type.Simple;
        panel.preserveAspect = false;
        panel.raycastTarget = false;
        panel.color = baseColor;

        Image innerShade = GetOrCreateChildImage(panel.transform, "UseButtonInnerShade");
        innerShade.gameObject.SetActive(true);
        innerShade.rectTransform.anchorMin = Vector2.zero;
        innerShade.rectTransform.anchorMax = Vector2.one;
        innerShade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        innerShade.rectTransform.offsetMin = new Vector2(9f, 8f);
        innerShade.rectTransform.offsetMax = new Vector2(-9f, -8f);
        innerShade.sprite = GetPlayButtonShapeSprite();
        innerShade.type = Image.Type.Simple;
        innerShade.raycastTarget = false;
        innerShade.color = new Color(0.02f, 0.04f, 0.065f, 0.58f);

        Image progressFill = GetOrCreateChildImage(panel.transform, "UseButtonProgressFill");
        progressFill.gameObject.SetActive(false);
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = Vector2.one;
        progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        progressFill.rectTransform.offsetMin = new Vector2(9f, 8f);
        progressFill.rectTransform.offsetMax = new Vector2(-9f, -8f);
        progressFill.sprite = GetPlayButtonShapeSprite();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        progressFill.preserveAspect = false;
        progressFill.raycastTarget = false;
        progressFill.color = new Color(0.16f, 0.95f, 0.34f, 0.76f);
        progressFill.transform.SetSiblingIndex(innerShade.transform.GetSiblingIndex() + 1);

        Color accentColor = button.interactable
            ? new Color(0.35f, 0.82f, 1f, 0.95f)
            : new Color(0.38f, 0.42f, 0.46f, 0.4f);
        Color accentSoftColor = button.interactable
            ? new Color(0.35f, 0.82f, 1f, 0.28f)
            : new Color(0.38f, 0.42f, 0.46f, 0.14f);

        RectTransform leftAccent = GetOrCreateChildImage(visual.transform, "UseButtonLeftAccent").rectTransform;
        RectTransform rightAccent = GetOrCreateChildImage(visual.transform, "UseButtonRightAccent").rectTransform;
        RectTransform topAccent = GetOrCreateChildImage(visual.transform, "UseButtonTopAccent").rectTransform;
        RectTransform bottomAccent = GetOrCreateChildImage(visual.transform, "UseButtonBottomAccent").rectTransform;
        ConfigureAccentBar(leftAccent.GetComponent<Image>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(6f, 28f), accentColor);
        ConfigureAccentBar(rightAccent.GetComponent<Image>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(6f, 28f), accentColor);
        ConfigureAccentBar(topAccent.GetComponent<Image>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(58f, 4f), accentSoftColor);
        ConfigureAccentBar(bottomAccent.GetComponent<Image>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(46f, 3f), accentSoftColor);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        UseButtonVisualController controller = button.GetComponent<UseButtonVisualController>();
        if (controller == null)
            controller = button.gameObject.AddComponent<UseButtonVisualController>();

        controller.Configure(button, rootRect, panel.rectTransform, innerShade.rectTransform,
            leftAccent, rightAccent, topAccent, bottomAccent, label != null ? label.rectTransform : null,
            visual, panel, innerShade, progressFill, label, baseColor, highlighted, pressed,
            new Color(0.11f, 0.38f, 0.21f, 0.98f),
            new Color(0.15f, 0.5f, 0.28f, 1f),
            new Color(0.08f, 0.24f, 0.15f, 1f));

        ApplyOutline(visual.gameObject, new Color(0f, 0f, 0f, 0.38f), new Vector2(4f, -4f));
        button.targetGraphic = panel;
    }

    void ConfigureUseButtonIcon(Transform parent, Color color)
    {
        ConfigureIconBar(parent, "UseIconTop", new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(24f, 4f), 0f, color);
        ConfigureIconBar(parent, "UseIconBottom", new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(24f, 4f), 0f, color);
        ConfigureIconBar(parent, "UseIconLeft", new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(4f, 22f), 0f, color);
        ConfigureIconBar(parent, "UseIconRight", new Vector2(1f, 0.5f), new Vector2(-3f, 0f), new Vector2(4f, 22f), 0f, color);
        ConfigureIconBar(parent, "UseIconCrossH", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 3f), 0f, new Color(1f, 0.9f, 0.42f, color.a));
        ConfigureIconBar(parent, "UseIconCrossV", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 24f), 0f, new Color(1f, 0.9f, 0.42f, color.a));
        ConfigureIconBar(parent, "UseIconSlash", new Vector2(0.5f, 0.5f), new Vector2(12f, -6f), new Vector2(4f, 24f), -42f, new Color(1f, 1f, 1f, color.a * 0.72f));
    }

    void ConfigureIconBar(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, float zRotation, Color color)
    {
        Image image = GetOrCreateChildImage(parent, name);
        image.sprite = GetWhitePixelSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, zRotation);
    }

    void ConfigureSaveRunButtonVisual(Button button, Image visual, Color baseColor, Color highlighted, Color pressed)
    {
        if (visual == null)
            return;

        RectTransform rootRect = visual.rectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        visual.sprite = GetPlayButtonShapeSprite();
        visual.type = Image.Type.Simple;
        visual.preserveAspect = false;
        visual.raycastTarget = false;
        visual.color = button.interactable
            ? new Color(0.39f, 0.45f, 0.53f, 0.98f)
            : new Color(0.2f, 0.22f, 0.25f, 0.72f);
        visual.gameObject.SetActive(true);

        Image panel = GetOrCreateChildImage(visual.transform, "SaveRunPanel");
        panel.rectTransform.anchorMin = Vector2.zero;
        panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.offsetMin = new Vector2(8f, 8f);
        panel.rectTransform.offsetMax = new Vector2(-8f, -8f);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        panel.sprite = GetPlayButtonShapeSprite();
        panel.type = Image.Type.Simple;
        panel.preserveAspect = false;
        panel.raycastTarget = false;
        panel.color = button.interactable ? baseColor : button.colors.disabledColor;

        Image innerShade = GetOrCreateChildImage(panel.transform, "SaveRunInnerShade");
        innerShade.rectTransform.anchorMin = Vector2.zero;
        innerShade.rectTransform.anchorMax = Vector2.one;
        innerShade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        innerShade.rectTransform.offsetMin = new Vector2(12f, 10f);
        innerShade.rectTransform.offsetMax = new Vector2(-12f, -10f);
        innerShade.sprite = GetPlayButtonShapeSprite();
        innerShade.type = Image.Type.Simple;
        innerShade.raycastTarget = false;
        innerShade.color = button.interactable
            ? new Color(0.03f, 0.06f, 0.1f, 0.55f)
            : new Color(0.02f, 0.03f, 0.05f, 0.44f);

        Color accentColor = button.interactable
            ? new Color(0.42f, 0.9f, 0.58f, 0.95f)
            : new Color(0.38f, 0.42f, 0.46f, 0.4f);
        Color accentSoftColor = button.interactable
            ? new Color(0.42f, 0.9f, 0.58f, 0.3f)
            : new Color(0.38f, 0.42f, 0.46f, 0.14f);

        ConfigureAccentBar(GetOrCreateChildImage(visual.transform, "SaveRunLeftAccent"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(7f, 42f), accentColor);
        ConfigureAccentBar(GetOrCreateChildImage(visual.transform, "SaveRunRightAccent"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(7f, 42f), accentColor);
        ConfigureAccentBar(GetOrCreateChildImage(visual.transform, "SaveRunTopAccent"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(86f, 5f), accentSoftColor);
        ConfigureAccentBar(GetOrCreateChildImage(visual.transform, "SaveRunBottomAccent"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(70f, 4f), accentSoftColor);

        Image icon = GetOrCreateChildImage(visual.transform, "SaveRunIcon");
        icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(72f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(60f, 68f);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.sprite = LoadPlayRocketSprite();
        icon.color = button.interactable
            ? new Color(0.94f, 0.98f, 1f, 0.98f)
            : new Color(0.55f, 0.58f, 0.62f, 0.75f);
        icon.enabled = icon.sprite != null;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        SaveRunButtonVisualController controller = button.GetComponent<SaveRunButtonVisualController>();
        if (controller == null)
            controller = button.gameObject.AddComponent<SaveRunButtonVisualController>();
        controller.Configure(button, visual.rectTransform, panel.rectTransform, innerShade.rectTransform,
            GetOrCreateChildImage(visual.transform, "SaveRunLeftAccent").rectTransform,
            GetOrCreateChildImage(visual.transform, "SaveRunRightAccent").rectTransform,
            GetOrCreateChildImage(visual.transform, "SaveRunTopAccent").rectTransform,
            GetOrCreateChildImage(visual.transform, "SaveRunBottomAccent").rectTransform,
            icon.rectTransform, label != null ? label.rectTransform : null, panel, innerShade,
            icon, label, baseColor, highlighted, pressed);

        ApplyOutline(visual.gameObject, new Color(0f, 0f, 0f, 0.42f), new Vector2(4f, -4f));
        button.targetGraphic = panel;
    }

    void ConfigureNavButtonVisual(Button button, Image visual, string role, Color baseColor, Color highlighted, Color pressed)
    {
        if (visual == null)
            return;

        RectTransform rootRect = visual.rectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        visual.sprite = GetPlayButtonShapeSprite();
        visual.type = Image.Type.Simple;
        visual.preserveAspect = false;
        visual.raycastTarget = false;
        visual.color = button.interactable
            ? new Color(0.36f, 0.42f, 0.5f, 0.98f)
            : new Color(0.2f, 0.22f, 0.25f, 0.72f);
        visual.gameObject.SetActive(true);

        Image panel = GetOrCreateChildImage(visual.transform, "SaveRunPanel");
        panel.rectTransform.anchorMin = Vector2.zero;
        panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.offsetMin = new Vector2(8f, 8f);
        panel.rectTransform.offsetMax = new Vector2(-8f, -8f);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        panel.sprite = GetPlayButtonShapeSprite();
        panel.type = Image.Type.Simple;
        panel.preserveAspect = false;
        panel.raycastTarget = false;
        panel.color = button.interactable ? baseColor : button.colors.disabledColor;

        Image innerShade = GetOrCreateChildImage(panel.transform, "SaveRunInnerShade");
        innerShade.rectTransform.anchorMin = Vector2.zero;
        innerShade.rectTransform.anchorMax = Vector2.one;
        innerShade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        innerShade.rectTransform.offsetMin = new Vector2(9f, 8f);
        innerShade.rectTransform.offsetMax = new Vector2(-9f, -8f);
        innerShade.sprite = GetPlayButtonShapeSprite();
        innerShade.type = Image.Type.Simple;
        innerShade.raycastTarget = false;
        innerShade.color = button.interactable
            ? new Color(0.03f, 0.06f, 0.1f, 0.55f)
            : new Color(0.02f, 0.03f, 0.05f, 0.44f);

        Color accentColor = button.interactable
            ? new Color(0.35f, 0.82f, 1f, 0.95f)
            : new Color(0.38f, 0.42f, 0.46f, 0.4f);
        Color accentSoftColor = button.interactable
            ? new Color(0.35f, 0.82f, 1f, 0.28f)
            : new Color(0.38f, 0.42f, 0.46f, 0.14f);

        RectTransform leftAccent = GetOrCreateChildImage(visual.transform, "SaveRunLeftAccent").rectTransform;
        RectTransform rightAccent = GetOrCreateChildImage(visual.transform, "SaveRunRightAccent").rectTransform;
        RectTransform topAccent = GetOrCreateChildImage(visual.transform, "SaveRunTopAccent").rectTransform;
        RectTransform bottomAccent = GetOrCreateChildImage(visual.transform, "SaveRunBottomAccent").rectTransform;
        ConfigureAccentBar(leftAccent.GetComponent<Image>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(6f, 26f), accentColor);
        ConfigureAccentBar(rightAccent.GetComponent<Image>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(6f, 26f), accentColor);
        ConfigureAccentBar(topAccent.GetComponent<Image>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(52f, 4f), accentSoftColor);
        ConfigureAccentBar(bottomAccent.GetComponent<Image>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(42f, 3f), accentSoftColor);

        Image icon = GetOrCreateChildImage(visual.transform, "SaveRunIcon");
        icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(58f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(36f, 36f);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.sprite = role == "nav_back" || role == "nav_back_wide" || role == "skin_choice" ? null : LoadNavIconSprite(role);
        icon.color = button.interactable
            ? new Color(0.94f, 0.98f, 1f, 0.98f)
            : new Color(0.55f, 0.58f, 0.62f, 0.75f);
        icon.enabled = icon.sprite != null;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        SaveRunButtonVisualController controller = button.GetComponent<SaveRunButtonVisualController>();
        if (controller == null)
            controller = button.gameObject.AddComponent<SaveRunButtonVisualController>();
        controller.Configure(button, visual.rectTransform, panel.rectTransform, innerShade.rectTransform,
            leftAccent, rightAccent, topAccent, bottomAccent,
            icon.rectTransform, label != null ? label.rectTransform : null, panel, innerShade,
            icon, label, baseColor, highlighted, pressed);

        ApplyOutline(visual.gameObject, new Color(0f, 0f, 0f, 0.38f), new Vector2(4f, -4f));
        button.targetGraphic = panel;
    }

    static bool IsFramedButtonRole(string role)
    {
        return role == "save_run" || IsNavButtonRole(role) || role == "skin_choice";
    }

    static bool IsNavButtonRole(string role)
    {
        return role == "nav_crafting" || role == "nav_trader" || role == "nav_inventory" || role == "nav_back" || role == "nav_back_wide";
    }

    bool IsSceneObject(GameObject obj)
    {
        return obj != null && obj.scene.IsValid();
    }

    void RebuildSceneObjectCache()
    {
        sceneObjects.Clear();
        sceneButtons.Clear();
        sceneObjectsByName.Clear();

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (!IsSceneObject(obj))
                continue;

            sceneObjects.Add(obj);
            if (!sceneObjectsByName.ContainsKey(obj.name))
                sceneObjectsByName.Add(obj.name, obj);

            Button button = obj.GetComponent<Button>();
            if (button != null)
                sceneButtons.Add(button);
        }

        sceneObjectCacheReady = true;
    }

    void ClearSceneObjectCache()
    {
        sceneObjectCacheReady = false;
        sceneObjects.Clear();
        sceneButtons.Clear();
        sceneObjectsByName.Clear();
    }

    GameObject FindSceneObjectByName(string name)
    {
        if (sceneObjectCacheReady)
        {
            return sceneObjectsByName.TryGetValue(name, out GameObject cachedObject) && IsSceneObject(cachedObject)
                ? cachedObject
                : null;
        }

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

    Image GetOrCreateSaveRunButtonVisual(Transform parent)
    {
        Transform existing = parent.Find("SaveRunButtonVisual");
        GameObject visualObject;

        if (existing != null)
        {
            visualObject = existing.gameObject;
        }
        else
        {
            visualObject = new GameObject("SaveRunButtonVisual", typeof(RectTransform), typeof(Image));
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.SetAsFirstSibling();
        }

        Image image = visualObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    Image GetOrCreateChildImage(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        GameObject childObject;

        if (existing != null)
        {
            childObject = existing.gameObject;
        }
        else
        {
            childObject = new GameObject(childName, typeof(RectTransform), typeof(Image));
            childObject.transform.SetParent(parent, false);
        }

        Image image = childObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    void ConfigureAccentBar(Image image, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        image.sprite = GetWhitePixelSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.color = color;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    static Sprite LoadPlayRocketSprite()
    {
        if (cachedPlayRocketSprite != null)
            return cachedPlayRocketSprite;

        Texture2D texture = Resources.Load<Texture2D>("UI/play_button_launch_tight");
        if (texture == null)
            return null;

        cachedPlayRocketSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        cachedPlayRocketSprite.name = "RuntimePlayButtonRocketSprite";
        cachedPlayRocketSprite.hideFlags = HideFlags.HideAndDontSave;

        return cachedPlayRocketSprite;
    }

    static Sprite LoadNavIconSprite(string role)
    {
        switch (role)
        {
            case "nav_crafting":
                return LoadRuntimeUiSprite("UI/icon_crafting", ref cachedCraftingIconSprite, "RuntimeCraftingIconSprite");
            case "nav_trader":
                return LoadRuntimeUiSprite("UI/icon_trader", ref cachedTraderIconSprite, "RuntimeTraderIconSprite");
            case "nav_inventory":
                return LoadRuntimeUiSprite("UI/icon_inventory", ref cachedInventoryIconSprite, "RuntimeInventoryIconSprite");
            default:
                return null;
        }
    }

    static Sprite LoadRuntimeUiSprite(string resourcePath, ref Sprite cache, string spriteName)
    {
        if (cache != null)
            return cache;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        try
        {
            Texture2D maskTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            maskTexture.name = spriteName + "_Texture";
            maskTexture.hideFlags = HideFlags.HideAndDontSave;

            Color[] sourcePixels = texture.GetPixels();
            Color[] outputPixels = new Color[sourcePixels.Length];
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color source = sourcePixels[i];
                float luminance = (source.r + source.g + source.b) / 3f;
                float alpha = source.a * Mathf.Clamp01(1f - luminance);
                outputPixels[i] = new Color(1f, 1f, 1f, alpha);
            }

            maskTexture.SetPixels(outputPixels);
            maskTexture.Apply(false, true);

            cache = Sprite.Create(
                maskTexture,
                new Rect(0f, 0f, maskTexture.width, maskTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            cache.name = spriteName;
            cache.hideFlags = HideFlags.HideAndDontSave;
            return cache;
        }
        catch (System.ArgumentException)
        {
            Sprite fallback = Resources.Load<Sprite>(resourcePath);
            if (fallback != null)
                return fallback;

            return null;
        }
    }

    static Sprite GetWhitePixelSprite()
    {
        if (whitePixelSprite != null)
            return whitePixelSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "RuntimeWhitePixel";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);

        whitePixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whitePixelSprite.name = "RuntimeWhitePixelSprite";
        whitePixelSprite.hideFlags = HideFlags.HideAndDontSave;
        return whitePixelSprite;
    }

    static Sprite GetJoystickBoosterRingSprite()
    {
        if (cachedJoystickBoosterRingSprite != null)
            return cachedJoystickBoosterRingSprite;

        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeJoystickBoosterRingTexture";
        texture.hideFlags = HideFlags.HideAndDontSave;

        float outerRadius = (size - 1) * 0.5f;
        float innerRadius = outerRadius * (1f / PlayerMovement.AdvancedBoosterOuterInputLimit);
        float edge = 2.25f;
        Color32 clear = new Color32(255, 255, 255, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - outerRadius;
                float dy = y - outerRadius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float outerAlpha = 1f - Mathf.SmoothStep(outerRadius - edge, outerRadius, distance);
                float innerAlpha = Mathf.SmoothStep(innerRadius - edge, innerRadius + edge, distance);
                float alpha = Mathf.Clamp01(outerAlpha * innerAlpha);
                texture.SetPixel(x, y, alpha > 0.001f ? new Color(1f, 1f, 1f, alpha) : clear);
            }
        }

        texture.Apply(false, true);
        cachedJoystickBoosterRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        cachedJoystickBoosterRingSprite.name = "RuntimeJoystickBoosterRingSprite";
        cachedJoystickBoosterRingSprite.hideFlags = HideFlags.HideAndDontSave;
        return cachedJoystickBoosterRingSprite;
    }

    static Sprite GetPlayButtonShapeSprite()
    {
        if (playButtonShapeSprite != null)
            return playButtonShapeSprite;

        const int width = 320;
        const int height = 92;
        const float bevel = 18f;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "RuntimePlayButtonShape";
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float coverage = GetChamferCoverage(x, y, width, height, bevel);
                pixels[y * width + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        playButtonShapeSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        playButtonShapeSprite.name = "RuntimePlayButtonShapeSprite";
        playButtonShapeSprite.hideFlags = HideFlags.HideAndDontSave;
        return playButtonShapeSprite;
    }

    static float GetChamferCoverage(int x, int y, int width, int height, float bevel)
    {
        float[] sampleOffsets = { 0.2f, 0.5f, 0.8f };
        float inside = 0f;
        float samples = 0f;

        for (int sx = 0; sx < sampleOffsets.Length; sx++)
        {
            for (int sy = 0; sy < sampleOffsets.Length; sy++)
            {
                samples += 1f;
                float px = x + sampleOffsets[sx];
                float py = y + sampleOffsets[sy];
                if (IsInsideChamferedRect(px, py, width, height, bevel))
                    inside += 1f;
            }
        }

        return inside / samples;
    }

    static bool IsInsideChamferedRect(float x, float y, float width, float height, float bevel)
    {
        float maxX = width - 1f;
        float maxY = height - 1f;

        if (x < 0f || y < 0f || x > maxX || y > maxY)
            return false;

        if (x + y < bevel)
            return false;

        if ((maxX - x) + y < bevel)
            return false;

        if (x + (maxY - y) < bevel)
            return false;

        if ((maxX - x) + (maxY - y) < bevel)
            return false;

        return true;
    }
}

class SaveRunButtonVisualController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    Button button;
    RectTransform visualRootRect;
    RectTransform panelRect;
    RectTransform innerShadeRect;
    RectTransform leftAccentRect;
    RectTransform rightAccentRect;
    RectTransform topAccentRect;
    RectTransform bottomAccentRect;
    RectTransform iconRect;
    RectTransform labelRect;
    Image panelImage;
    Image innerShadeImage;
    Image iconImage;
    TMP_Text labelText;
    Color baseColor;
    Color highlightedColor;
    Color pressedColor;
    bool pointerDown;
    bool pointerInside;

    public void Configure(Button targetButton, RectTransform visualRoot, RectTransform panel, RectTransform innerShade,
        RectTransform leftAccent, RectTransform rightAccent, RectTransform topAccent, RectTransform bottomAccent,
        RectTransform icon, RectTransform label, Image panelImageRef, Image innerShadeImageRef, Image iconImageRef,
        TMP_Text labelTextRef, Color baseTint, Color highlightedTint, Color pressedTint)
    {
        button = targetButton;
        visualRootRect = visualRoot;
        panelRect = panel;
        innerShadeRect = innerShade;
        leftAccentRect = leftAccent;
        rightAccentRect = rightAccent;
        topAccentRect = topAccent;
        bottomAccentRect = bottomAccent;
        iconRect = icon;
        labelRect = label;
        panelImage = panelImageRef;
        innerShadeImage = innerShadeImageRef;
        iconImage = iconImageRef;
        labelText = labelTextRef;
        baseColor = baseTint;
        highlightedColor = highlightedTint;
        pressedColor = pressedTint;
        ApplyStateImmediate();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDown = true;
        ApplyStateImmediate();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
        ApplyStateImmediate();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        ApplyStateImmediate();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerDown = false;
        ApplyStateImmediate();
    }

    void OnDisable()
    {
        pointerDown = false;
        pointerInside = false;
    }

    void LateUpdate()
    {
        ApplyStateImmediate();
    }

    void ApplyStateImmediate()
    {
        if (button == null || panelRect == null || visualRootRect == null || panelImage == null)
            return;

        bool disabled = !button.interactable;
        bool pressed = !disabled && pointerDown;
        bool hovered = !disabled && pointerInside;

        if (visualRootRect != null)
            visualRootRect.localScale = pressed ? new Vector3(0.985f, 0.965f, 1f) : Vector3.one;

        panelRect.anchoredPosition = pressed ? new Vector2(0f, -3.5f) : hovered ? new Vector2(0f, -1f) : Vector2.zero;

        if (innerShadeRect != null)
            innerShadeRect.anchoredPosition = pressed ? new Vector2(0f, 2f) : Vector2.zero;

        if (iconRect != null)
            iconRect.anchoredPosition = pressed ? new Vector2(50f, -2f) : hovered ? new Vector2(50f, -0.5f) : new Vector2(50f, 0f);

        if (labelRect != null)
            labelRect.anchoredPosition = pressed ? new Vector2(0f, -2f) : hovered ? new Vector2(0f, -0.5f) : Vector2.zero;

        panelImage.color = disabled
            ? button.colors.disabledColor
            : pressed ? pressedColor : hovered ? highlightedColor : baseColor;

        if (innerShadeImage != null)
        {
            innerShadeImage.color = disabled
                ? new Color(0.02f, 0.03f, 0.05f, 0.4f)
                : pressed
                    ? new Color(0.02f, 0.04f, 0.07f, 0.78f)
                    : hovered
                        ? new Color(0.04f, 0.08f, 0.12f, 0.66f)
                        : new Color(0.03f, 0.06f, 0.1f, 0.55f);
        }

        Color hardAccent = disabled
            ? new Color(0.34f, 0.37f, 0.4f, 0.32f)
            : pressed
                ? new Color(0.56f, 0.9f, 1f, 1f)
                : hovered
                    ? new Color(0.45f, 0.86f, 1f, 0.98f)
                    : new Color(0.35f, 0.82f, 1f, 0.95f);
        Color softAccent = disabled
            ? new Color(0.34f, 0.37f, 0.4f, 0.16f)
            : pressed
                ? new Color(0.56f, 0.9f, 1f, 0.62f)
                : hovered
                    ? new Color(0.45f, 0.86f, 1f, 0.46f)
                    : new Color(0.35f, 0.82f, 1f, 0.34f);

        ApplyAccentState(leftAccentRect, pressed ? new Vector2(8f, 40f) : hovered ? new Vector2(7f, 37f) : new Vector2(6f, 34f), hardAccent);
        ApplyAccentState(rightAccentRect, pressed ? new Vector2(8f, 40f) : hovered ? new Vector2(7f, 37f) : new Vector2(6f, 34f), hardAccent);
        ApplyAccentState(topAccentRect, pressed ? new Vector2(92f, 6f) : hovered ? new Vector2(80f, 5f) : new Vector2(68f, 4f), softAccent);
        ApplyAccentState(bottomAccentRect, pressed ? new Vector2(76f, 5f) : hovered ? new Vector2(66f, 4f) : new Vector2(56f, 3f), softAccent);

        if (iconImage != null)
        {
            iconImage.color = disabled
                ? new Color(0.55f, 0.58f, 0.62f, 0.75f)
                : pressed
                    ? new Color(1f, 1f, 1f, 1f)
                    : hovered
                        ? new Color(0.97f, 1f, 1f, 1f)
                        : new Color(0.94f, 0.98f, 1f, 0.98f);
        }

        if (labelText != null)
        {
            labelText.color = disabled
                ? new Color(0.62f, 0.66f, 0.71f, 0.82f)
                : pressed
                    ? new Color(1f, 1f, 1f, 1f)
                    : hovered
                        ? new Color(0.97f, 1f, 1f, 1f)
                        : Color.white;
        }
    }

    static void ApplyAccentState(RectTransform rect, Vector2 size, Color color)
    {
        if (rect == null)
            return;

        rect.sizeDelta = size;
        Image image = rect.GetComponent<Image>();
        if (image != null)
            image.color = color;
    }
}
