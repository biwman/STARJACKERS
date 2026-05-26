using System;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoundChatCommandUI : MonoBehaviourPun
{
    sealed class ChatCommandDefinition
    {
        public readonly string Text;
        public readonly string IconResourcePath;
        public readonly string LightIconResourcePath;

        public ChatCommandDefinition(string text, string iconResourcePath)
        {
            Text = text;
            IconResourcePath = iconResourcePath;
            LightIconResourcePath = iconResourcePath
                .Replace("chat_command_v8_", "chat_command_light_v8_")
                .Replace("chat_command_v6_", "chat_command_light_v6_")
                .Replace("chat_command_v5_", "chat_command_light_v5_")
                .Replace("chat_command_v3_", "chat_command_light_v3_");
        }
    }

    const string ChatButtonName = "RoundChatButton";
    const string ChatMenuRootName = "RoundChatMenu";
    const string PortraitCalloutName = "RoundChatPortraitCallout";
    const string BubbleName = "RoundChatBubble";
    const string ChatButtonResourcePath = "UI/Chat/chat_button_light_v3";
    const float ChatButtonSize = 96f;
    const float CommandButtonSize = 92f;
    const float CommandButtonGap = 112f;
    const float LocalCalloutDuration = 3f;
    const float BubbleDuration = 3f;
    const float NearbyBroadcastDistance = 28f;
    const float ShipBubbleWorldOffset = 1.35f;

    static readonly ChatCommandDefinition[] Commands =
    {
        new ChatCommandDefinition("DONT SHOOT", "UI/Chat/chat_command_v8_00_dont_shoot"),
        new ChatCommandDefinition("DROP YOUR CARGO", "UI/Chat/chat_command_v8_01_drop_your_cargo"),
        new ChatCommandDefinition("IM FRIENDLY", "UI/Chat/chat_command_v8_02_im_friendly"),
        new ChatCommandDefinition("HELP ME", "UI/Chat/chat_command_v8_03_help_me"),
        new ChatCommandDefinition("YOU WILL NOT ESCAPE", "UI/Chat/chat_command_v8_04_you_will_not_escape"),
        new ChatCommandDefinition("RESISTANCE IS FUTILE", "UI/Chat/chat_command_v8_05_resistance_is_futile"),
        new ChatCommandDefinition("STOP", "UI/Chat/chat_command_v8_06_stop"),
        new ChatCommandDefinition("LOW ON HEALTH", "UI/Chat/chat_command_v8_07_low_on_health"),
        new ChatCommandDefinition("CARGO FULL", "UI/Chat/chat_command_v8_08_cargo_full"),
        new ChatCommandDefinition("LETS GO", "UI/Chat/chat_command_v8_09_lets_go"),
        new ChatCommandDefinition("ENEMIES NEARBY", "UI/Chat/chat_command_v8_10_enemies_nearby"),
        new ChatCommandDefinition("GOOD SHOOT", "UI/Chat/chat_command_v8_11_good_shoot"),
        new ChatCommandDefinition("WHERE TO?", "UI/Chat/chat_command_v8_12_where_to"),
        new ChatCommandDefinition("DIE!", "UI/Chat/chat_command_v8_13_die"),
        new ChatCommandDefinition("GOOD GAME", "UI/Chat/chat_command_v8_14_good_game"),
        new ChatCommandDefinition("WATCH THIS", "UI/Chat/chat_command_v8_15_watch_this"),
        new ChatCommandDefinition("FOLLOW ME", "UI/Chat/chat_command_v8_16_follow_me"),
        new ChatCommandDefinition("ROGER THAT!", "UI/Chat/chat_command_v8_17_roger_that"),
        new ChatCommandDefinition("WANNA TRADE?", "UI/Chat/chat_command_v8_18_wanna_trade")
    };

    static readonly Sprite[] commandSpriteCache = new Sprite[Commands.Length];
    static readonly Sprite[] commandLightSpriteCache = new Sprite[Commands.Length];
    static readonly Dictionary<string, Sprite> portraitSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    static Sprite chatButtonSprite;
    static Sprite circleSprite;
    static Sprite roundedPanelSprite;
    static Sprite bubbleSprite;
    static RoundChatCommandUI localMenuOwner;

    PlayerHealth health;
    Canvas canvas;
    RectTransform canvasRect;
    GameObject buttonObject;
    RectTransform buttonRect;
    GameObject menuRootObject;
    RectTransform menuRootRect;
    CanvasGroup menuGroup;
    GameObject portraitCalloutObject;
    TMP_Text portraitCalloutText;
    GameObject bubbleObject;
    RectTransform bubbleRect;
    Image bubbleIcon;
    Joystick blockedShootJoystick;
    bool blockedShootJoystickByChat;
    bool menuOpen;
    float portraitCalloutUntil;
    float bubbleUntil;

    public static bool IsLocalChatMenuOpen => localMenuOwner != null && localMenuOwner.menuOpen;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        if (!IsRealPilotShip())
        {
            enabled = false;
            return;
        }

        if (photonView.IsMine)
            EnsureLocalControls();
    }

    void Update()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();

        if (!IsRealPilotShip())
        {
            if (photonView != null && photonView.IsMine)
                SetMenuOpen(false);

            HideBubble();
            return;
        }

        if (photonView.IsMine)
        {
            EnsureLocalControls();
            RefreshLocalControls();
            RefreshPortraitCallout();
        }

        RefreshBubble();
    }

    void OnDisable()
    {
        if (photonView != null && photonView.IsMine)
            SetMenuOpen(false);

        HideBubble();
    }

    void OnDestroy()
    {
        if (photonView != null && photonView.IsMine)
        {
            SetMenuOpen(false);
            DestroyIfAlive(buttonObject);
            DestroyIfAlive(menuRootObject);
            DestroyIfAlive(portraitCalloutObject);
        }

        DestroyIfAlive(bubbleObject);
    }

    void EnsureLocalControls()
    {
        if (canvas == null || canvasRect == null)
            ResolveCanvas();

        if (canvas == null || canvasRect == null)
            return;

        if (buttonObject == null)
            CreateChatButton();

        if (menuRootObject == null)
            CreateCommandMenu();
    }

    void ResolveCanvas()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
            canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            return;
        }

        canvas = canvasObject.GetComponent<Canvas>();
        canvasRect = canvasObject.GetComponent<RectTransform>();
    }

    void CreateChatButton()
    {
        buttonObject = new GameObject(ChatButtonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
        buttonObject.transform.SetParent(canvas.transform, false);

        buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(ChatButtonSize, ChatButtonSize);

        Image background = buttonObject.GetComponent<Image>();
        background.sprite = GetCircleSprite();
        background.color = new Color(0.025f, 0.055f, 0.085f, 0.88f);

        Shadow shadow = buttonObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(3f, -4f);
        shadow.useGraphicAlpha = true;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        button.onClick.AddListener(ToggleMenu);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 2f);
        iconRect.sizeDelta = new Vector2(74f, 74f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = GetChatButtonSprite();
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.raycastTarget = false;

        buttonObject.transform.SetAsLastSibling();
    }

    void CreateCommandMenu()
    {
        menuRootObject = new GameObject(ChatMenuRootName, typeof(RectTransform), typeof(CanvasGroup));
        menuRootObject.transform.SetParent(canvas.transform, false);

        menuRootRect = menuRootObject.GetComponent<RectTransform>();
        menuRootRect.anchorMin = new Vector2(1f, 0f);
        menuRootRect.anchorMax = new Vector2(1f, 0f);
        menuRootRect.pivot = new Vector2(0.5f, 0.5f);
        menuRootRect.sizeDelta = Vector2.zero;

        menuGroup = menuRootObject.GetComponent<CanvasGroup>();
        menuGroup.interactable = true;
        menuGroup.blocksRaycasts = true;

        for (int i = 0; i < Commands.Length; i++)
            CreateCommandButton(i);

        menuRootObject.SetActive(false);
        menuRootObject.transform.SetAsLastSibling();
        if (buttonObject != null)
            buttonObject.transform.SetAsLastSibling();
    }

    void CreateCommandButton(int commandIndex)
    {
        GameObject commandObject = new GameObject("RoundChatCommand_" + commandIndex.ToString("00"), typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
        commandObject.transform.SetParent(menuRootObject.transform, false);

        RectTransform rect = commandObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(CommandButtonSize, CommandButtonSize);
        rect.anchoredPosition = GetCommandButtonOffset(commandIndex);

        Image background = commandObject.GetComponent<Image>();
        background.sprite = GetCircleSprite();
        background.color = new Color(0.025f, 0.055f, 0.085f, 0.86f);

        Shadow shadow = commandObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(2f, -3f);
        shadow.useGraphicAlpha = true;

        Button button = commandObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        int capturedIndex = commandIndex;
        button.onClick.AddListener(() => HandleCommandClicked(capturedIndex));

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(commandObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(76f, 76f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = GetCommandMenuSprite(commandIndex);
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.raycastTarget = false;
    }

    void RefreshLocalControls()
    {
        bool visible = IsGameplayHudVisible();
        if (!visible && menuOpen)
            SetMenuOpen(false);

        if (buttonObject != null && buttonObject.activeSelf != visible)
            buttonObject.SetActive(visible);

        if (menuRootObject != null && menuRootObject.activeSelf != visible && menuOpen)
            menuRootObject.SetActive(visible);

        if (!visible)
            return;

        UpdateLocalControlLayout();
        if (menuRootObject != null)
            menuRootObject.transform.SetAsLastSibling();
        if (buttonObject != null)
            buttonObject.transform.SetAsLastSibling();
    }

    void UpdateLocalControlLayout()
    {
        RectTransform shootJoystickRect = FindShootJoystickRect();
        if (shootJoystickRect == null)
            return;

        Joystick shootJoystick = shootJoystickRect.GetComponent<Joystick>();
        Vector2 basePosition = shootJoystick != null ? shootJoystick.DefaultAnchoredPosition : shootJoystickRect.anchoredPosition;
        Vector2 chatPosition = basePosition + new Vector2(150f, 310f);

        buttonRect.anchorMin = shootJoystickRect.anchorMin;
        buttonRect.anchorMax = shootJoystickRect.anchorMax;
        buttonRect.anchoredPosition = chatPosition;
        buttonRect.sizeDelta = new Vector2(ChatButtonSize, ChatButtonSize);

        if (menuRootRect != null)
        {
            menuRootRect.anchorMin = shootJoystickRect.anchorMin;
            menuRootRect.anchorMax = shootJoystickRect.anchorMax;
            menuRootRect.anchoredPosition = chatPosition;
        }
    }

    RectTransform FindShootJoystickRect()
    {
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (shootJoystickObject == null)
            return null;

        return shootJoystickObject.GetComponent<RectTransform>();
    }

    void ToggleMenu()
    {
        SetMenuOpen(!menuOpen);
    }

    void SetMenuOpen(bool open)
    {
        if (open && !IsGameplayHudVisible())
            open = false;

        if (menuOpen == open)
            return;

        menuOpen = open;
        if (menuRootObject != null)
            menuRootObject.SetActive(menuOpen);

        if (photonView != null && photonView.IsMine)
        {
            localMenuOwner = menuOpen ? this : localMenuOwner == this ? null : localMenuOwner;
            SetShootJoystickBlocked(menuOpen);
        }
    }

    void SetShootJoystickBlocked(bool blocked)
    {
        if (blocked)
        {
            RectTransform shootRect = FindShootJoystickRect();
            blockedShootJoystick = shootRect != null ? shootRect.GetComponent<Joystick>() : null;
            if (blockedShootJoystick != null && blockedShootJoystick.enabled)
            {
                blockedShootJoystick.enabled = false;
                blockedShootJoystickByChat = true;
            }

            return;
        }

        if (blockedShootJoystickByChat && blockedShootJoystick != null)
            blockedShootJoystick.enabled = true;

        blockedShootJoystickByChat = false;
        blockedShootJoystick = null;
    }

    void HandleCommandClicked(int commandIndex)
    {
        if (!IsValidCommand(commandIndex))
            return;

        SetMenuOpen(false);
        if (PhotonNetwork.InRoom && photonView != null)
            photonView.RPC(nameof(PlayChatCommandRpc), RpcTarget.All, commandIndex);
        else
            PlayChatCommandRpc(commandIndex);
    }

    [PunRPC]
    void PlayChatCommandRpc(int commandIndex)
    {
        if (!IsValidCommand(commandIndex))
            return;

        ChatCommandDefinition command = Commands[commandIndex];
        ShowWorldBubble(commandIndex);

        if (photonView != null && photonView.IsMine)
        {
            ShowPortraitCallout(command.Text);
            return;
        }

        if (ShouldShowRemoteFeed())
            RoundChatFeedUI.Show(command.Text, GetSenderPortraitSprite());
    }

    bool ShouldShowRemoteFeed()
    {
        if (!IsRoundHudVisible())
            return false;

        if (health == null || !GameTimer.IsActiveRoundPlayer(health))
            return false;

        PlayerHealth localHealth = FindLocalPlayerHealth();
        if (!GameTimer.IsActiveRoundPlayer(localHealth))
            return false;

        return Vector2.Distance(localHealth.transform.position, transform.position) <= NearbyBroadcastDistance;
    }

    static PlayerHealth FindLocalPlayerHealth()
    {
        GameObject tagged = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.TagObject as GameObject : null;
        PlayerHealth taggedHealth = tagged != null ? tagged.GetComponent<PlayerHealth>() : null;
        if (taggedHealth != null && taggedHealth.photonView != null && taggedHealth.photonView.IsMine)
            return taggedHealth;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate != null && candidate.photonView != null && candidate.photonView.IsMine)
                return candidate;
        }

        return null;
    }

    void ShowPortraitCallout(string text)
    {
        EnsurePortraitCallout();
        if (portraitCalloutObject == null || portraitCalloutText == null)
            return;

        portraitCalloutText.text = text;
        portraitCalloutUntil = Time.time + LocalCalloutDuration;
        portraitCalloutObject.SetActive(true);
        portraitCalloutObject.transform.SetAsLastSibling();
    }

    void EnsurePortraitCallout()
    {
        if (portraitCalloutObject != null && portraitCalloutText != null)
            return;

        GameObject hudRoot = GameObject.Find("RoundPilotHud");
        if (hudRoot == null)
            return;

        Transform existing = hudRoot.transform.Find(PortraitCalloutName);
        if (existing != null)
        {
            portraitCalloutObject = existing.gameObject;
            portraitCalloutText = portraitCalloutObject.GetComponentInChildren<TMP_Text>(true);
            return;
        }

        portraitCalloutObject = new GameObject(PortraitCalloutName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        portraitCalloutObject.transform.SetParent(hudRoot.transform, false);

        RectTransform rect = portraitCalloutObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, 68f);

        Image background = portraitCalloutObject.GetComponent<Image>();
        background.sprite = GetRoundedPanelSprite();
        background.color = new Color(0.02f, 0.04f, 0.065f, 0.86f);
        background.raycastTarget = false;

        CanvasGroup group = portraitCalloutObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(portraitCalloutObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        portraitCalloutText = textObject.GetComponent<TextMeshProUGUI>();
        ConfigureText(portraitCalloutText, 20f, TextAlignmentOptions.Center);
        portraitCalloutText.fontStyle = FontStyles.Bold;
        portraitCalloutText.enableAutoSizing = true;
        portraitCalloutText.fontSizeMin = 12f;
        portraitCalloutText.fontSizeMax = 20f;
        portraitCalloutText.textWrappingMode = TextWrappingModes.Normal;
        portraitCalloutText.raycastTarget = false;

        portraitCalloutObject.SetActive(false);
    }

    void RefreshPortraitCallout()
    {
        if (portraitCalloutObject == null || !portraitCalloutObject.activeSelf)
            return;

        if (Time.time >= portraitCalloutUntil || !IsGameplayHudVisible())
            portraitCalloutObject.SetActive(false);
    }

    void ShowWorldBubble(int commandIndex)
    {
        EnsureBubble();
        if (bubbleObject == null || bubbleIcon == null)
            return;

        bubbleIcon.sprite = GetCommandSprite(commandIndex);
        bubbleIcon.enabled = bubbleIcon.sprite != null;
        bubbleUntil = Time.time + BubbleDuration;
        bubbleObject.SetActive(true);
        bubbleObject.transform.SetAsLastSibling();
        RefreshBubble();
    }

    void EnsureBubble()
    {
        if (bubbleObject != null && bubbleRect != null && bubbleIcon != null && canvasRect != null)
            return;

        if (canvas == null || canvasRect == null)
            ResolveCanvas();

        if (canvas == null || canvasRect == null)
            return;

        bubbleObject = new GameObject(BubbleName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Shadow));
        bubbleObject.transform.SetParent(canvas.transform, false);

        bubbleRect = bubbleObject.GetComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.pivot = new Vector2(0.5f, 0.12f);
        bubbleRect.sizeDelta = new Vector2(118f, 96f);

        Image background = bubbleObject.GetComponent<Image>();
        background.sprite = GetBubbleSprite();
        background.color = new Color(0.96f, 0.99f, 1f, 0.94f);
        background.raycastTarget = false;

        CanvasGroup group = bubbleObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Shadow shadow = bubbleObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(2f, -3f);
        shadow.useGraphicAlpha = true;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(bubbleObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 10f);
        iconRect.sizeDelta = new Vector2(78f, 78f);

        bubbleIcon = iconObject.GetComponent<Image>();
        bubbleIcon.preserveAspect = true;
        bubbleIcon.color = new Color(0.015f, 0.02f, 0.03f, 1f);
        bubbleIcon.raycastTarget = false;

        bubbleObject.SetActive(false);
    }

    void RefreshBubble()
    {
        if (bubbleObject == null || !bubbleObject.activeSelf)
            return;

        if (Time.time >= bubbleUntil || Camera.main == null || canvasRect == null || !IsRoundHudVisible())
        {
            HideBubble();
            return;
        }

        Vector3 worldPosition = transform.position + Vector3.up * ShipBubbleWorldOffset;
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        if (screenPosition.z < 0f)
        {
            bubbleObject.SetActive(false);
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 canvasPosition))
            bubbleRect.anchoredPosition = canvasPosition;
    }

    void HideBubble()
    {
        if (bubbleObject != null && bubbleObject.activeSelf)
            bubbleObject.SetActive(false);
    }

    Sprite GetSenderPortraitSprite()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : null;
        string pilotId = RoomSettings.GetPlayerPilotId(owner, PilotCatalog.JakeId);
        if (portraitSpriteCache.TryGetValue(pilotId, out Sprite cached) && cached != null)
            return cached;

        PilotDefinition definition = PilotCatalog.GetDefinition(pilotId);
        Sprite sprite = definition != null ? LoadSpriteFromResources(definition.PortraitResourcePath) : null;
        if (sprite == null)
            sprite = LoadSpriteFromResources(PilotCatalog.GetDefinition(PilotCatalog.JakeId).PortraitResourcePath);

        portraitSpriteCache[pilotId] = sprite;
        return sprite;
    }

    bool IsGameplayHudVisible()
    {
        if (health == null || !GameTimer.IsActiveRoundPlayer(health))
            return false;

        return IsRoundHudVisible();
    }

    static bool IsRoundHudVisible()
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

    bool IsRealPilotShip()
    {
        return health != null && !health.IsBotControlled && !health.IsAstronautControlled;
    }

    static Vector2 GetCommandButtonOffset(int index)
    {
        int row = index / 5;
        int column = index % 5;
        if (index >= 15)
        {
            row = 3;
            column = index - 15;
        }

        return new Vector2(-(column + 1) * CommandButtonGap, -row * CommandButtonGap);
    }

    static bool IsValidCommand(int commandIndex)
    {
        return commandIndex >= 0 && commandIndex < Commands.Length;
    }

    static void ConfigureText(TMP_Text text, float fontSize, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.characterSpacing = 0f;
    }

    static Sprite GetChatButtonSprite()
    {
        if (chatButtonSprite == null)
            chatButtonSprite = LoadSpriteFromResources(ChatButtonResourcePath);

        return chatButtonSprite;
    }

    static Sprite GetCommandSprite(int commandIndex)
    {
        if (!IsValidCommand(commandIndex))
            return null;

        if (commandSpriteCache[commandIndex] == null)
            commandSpriteCache[commandIndex] = LoadSpriteFromResources(Commands[commandIndex].IconResourcePath);

        return commandSpriteCache[commandIndex];
    }

    static Sprite GetCommandMenuSprite(int commandIndex)
    {
        if (!IsValidCommand(commandIndex))
            return null;

        if (commandLightSpriteCache[commandIndex] == null)
            commandLightSpriteCache[commandIndex] = LoadSpriteFromResources(Commands[commandIndex].LightIconResourcePath);

        return commandLightSpriteCache[commandIndex] != null
            ? commandLightSpriteCache[commandIndex]
            : GetCommandSprite(commandIndex);
    }

    static Sprite LoadSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
            return null;

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

    public static Sprite GetRoundedPanelSprite()
    {
        if (roundedPanelSprite == null)
            roundedPanelSprite = CreateRoundedRectSprite(128, 128, 20f, "RoundChatRoundedPanel");

        return roundedPanelSprite;
    }

    static Sprite GetCircleSprite()
    {
        if (circleSprite == null)
            circleSprite = CreateCircleSprite(128, "RoundChatCircle");

        return circleSprite;
    }

    static Sprite GetBubbleSprite()
    {
        if (bubbleSprite == null)
            bubbleSprite = CreateBubbleSprite();

        return bubbleSprite;
    }

    static Sprite CreateCircleSprite(int size, string name)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        float feather = size * 0.055f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - feather, radius, distance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite CreateRoundedRectSprite(int width, int height, float radius, string name)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 half = new Vector2(width * 0.5f - radius, height * 0.5f - radius);
        const float feather = 2.2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 distance = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - half;
                float outside = new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f)).magnitude - radius;
                float alpha = 1f - Mathf.InverseLerp(0f, feather, outside);
                pixels[(y * width) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), Mathf.Max(width, height));
    }

    static Sprite CreateBubbleSprite()
    {
        const int width = 128;
        const int height = 104;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "RoundChatBubble";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = RoundedRectMask(x, y, width, height - 20, 18f, 20f);
                float tailY = y;
                float tailHalfWidth = Mathf.Lerp(0f, 22f, Mathf.Clamp01(tailY / 24f));
                bool inTail = y <= 28 && Mathf.Abs(x - width * 0.5f) <= tailHalfWidth;
                if (inTail)
                    alpha = Mathf.Max(alpha, 1f);

                pixels[(y * width) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.08f), width);
    }

    static float RoundedRectMask(int x, int y, int width, int height, float bottomOffset, float radius)
    {
        float localY = y - bottomOffset;
        if (localY < 0f)
            return 0f;

        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 half = new Vector2(width * 0.5f - radius, height * 0.5f - radius);
        Vector2 point = new Vector2(x + 0.5f, localY + 0.5f);
        Vector2 distance = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - half;
        float outside = new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f)).magnitude - radius;
        return 1f - Mathf.InverseLerp(0f, 2f, outside);
    }

    static void DestroyIfAlive(GameObject obj)
    {
        if (obj != null)
            Destroy(obj);
    }
}

sealed class RoundChatFeedUI : MonoBehaviour
{
    sealed class FeedEntry
    {
        public GameObject Root;
        public RectTransform Rect;
        public Image Portrait;
        public TMP_Text Text;
        public float ExpiresAt;
    }

    const string RootName = "RoundChatRemoteFeed";
    const int MaxEntries = 2;
    const float EntryDuration = 3f;
    const float CardWidth = 410f;
    const float CardHeight = 166f;
    const float PortraitSize = 154f;
    const float Gap = 16f;
    const float RightGapFromTimer = 184f;
    const float TopInset = 14f;

    static RoundChatFeedUI instance;

    readonly List<FeedEntry> entries = new List<FeedEntry>(MaxEntries);
    RectTransform rootRect;

    public static void Show(string text, Sprite portrait)
    {
        RoundChatFeedUI feed = EnsureInstance();
        if (feed == null)
            return;

        feed.AddEntry(text, portrait);
    }

    static RoundChatFeedUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
            return null;

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(canvasObject.transform, false);
        }

        instance = root.GetComponent<RoundChatFeedUI>();
        if (instance == null)
            instance = root.AddComponent<RoundChatFeedUI>();

        instance.ConfigureRoot();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ConfigureRoot();
    }

    void Update()
    {
        bool changed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] == null || entries[i].Root == null || Time.time < entries[i].ExpiresAt)
                continue;

            Destroy(entries[i].Root);
            entries.RemoveAt(i);
            changed = true;
        }

        if (changed)
            Reflow();
    }

    void ConfigureRoot()
    {
        rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
            return;

        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;
        transform.SetAsLastSibling();
    }

    void AddEntry(string text, Sprite portrait)
    {
        if (entries.Count >= MaxEntries)
        {
            FeedEntry oldest = entries[0];
            if (oldest != null && oldest.Root != null)
                Destroy(oldest.Root);
            entries.RemoveAt(0);
        }

        FeedEntry entry = CreateEntry(text, portrait);
        if (entry == null)
            return;

        entries.Add(entry);
        Reflow();
        transform.SetAsLastSibling();
    }

    FeedEntry CreateEntry(string text, Sprite portrait)
    {
        if (rootRect == null)
            ConfigureRoot();

        if (rootRect == null)
            return null;

        FeedEntry entry = new FeedEntry();
        entry.Root = new GameObject("RoundChatRemoteFeedEntry", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Shadow));
        entry.Root.transform.SetParent(transform, false);
        entry.Rect = entry.Root.GetComponent<RectTransform>();
        entry.Rect.anchorMin = new Vector2(1f, 1f);
        entry.Rect.anchorMax = new Vector2(1f, 1f);
        entry.Rect.pivot = new Vector2(1f, 1f);
        entry.Rect.sizeDelta = new Vector2(CardWidth, CardHeight);

        Image background = entry.Root.GetComponent<Image>();
        background.sprite = RoundChatCommandUI.GetRoundedPanelSprite();
        background.color = new Color(0.025f, 0.045f, 0.07f, 0.88f);
        background.raycastTarget = false;

        CanvasGroup group = entry.Root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Shadow shadow = entry.Root.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.46f);
        shadow.effectDistance = new Vector2(3f, -4f);
        shadow.useGraphicAlpha = true;

        GameObject portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(entry.Root.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(8f, 0f);
        portraitRect.sizeDelta = new Vector2(PortraitSize, PortraitSize);

        entry.Portrait = portraitObject.GetComponent<Image>();
        entry.Portrait.sprite = portrait;
        entry.Portrait.preserveAspect = true;
        entry.Portrait.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(entry.Root.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(PortraitSize + 20f, 18f);
        textRect.offsetMax = new Vector2(-18f, -18f);

        entry.Text = textObject.GetComponent<TextMeshProUGUI>();
        ConfigureFeedText(entry.Text);
        entry.Text.text = text;

        entry.ExpiresAt = Time.time + EntryDuration;
        return entry;
    }

    void Reflow()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            FeedEntry entry = entries[i];
            if (entry == null || entry.Rect == null)
                continue;

            entry.Rect.anchoredPosition = new Vector2(-RightGapFromTimer - i * (CardWidth + Gap), -TopInset);
        }
    }

    static void ConfigureFeedText(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        text.fontSize = 27f;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 27f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.characterSpacing = 0f;
        text.color = new Color(0.92f, 0.98f, 1f, 1f);
        text.raycastTarget = false;
    }
}
