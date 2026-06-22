using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class LobbyManager
{
    RectTransform EnsureFullScreenLobbyRoot()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return transform as RectTransform;

        bool createdRoot = false;
        if (fullScreenLobbyRootObject == null || !fullScreenLobbyRootObject.scene.IsValid())
        {
            Transform existing = canvas.transform.Find("LobbyFullScreenRoot");
            if (existing != null)
                fullScreenLobbyRootObject = existing.gameObject;
            else
            {
                fullScreenLobbyRootObject = new GameObject("LobbyFullScreenRoot", typeof(RectTransform));
                createdRoot = true;
            }
        }

        if (fullScreenLobbyRootObject.transform.parent != canvas.transform)
            fullScreenLobbyRootObject.transform.SetParent(canvas.transform, false);

        fullScreenLobbyRootRect = fullScreenLobbyRootObject.GetComponent<RectTransform>();
        fullScreenLobbyRootRect.anchorMin = Vector2.zero;
        fullScreenLobbyRootRect.anchorMax = Vector2.one;
        fullScreenLobbyRootRect.pivot = new Vector2(0.5f, 0.5f);
        fullScreenLobbyRootRect.offsetMin = Vector2.zero;
        fullScreenLobbyRootRect.offsetMax = Vector2.zero;
        fullScreenLobbyRootRect.anchoredPosition = Vector2.zero;
        fullScreenLobbyRootObject.transform.SetAsLastSibling();
        if (createdRoot)
            fullScreenLobbyRootObject.SetActive(false);
        return fullScreenLobbyRootRect;
    }

    bool ShouldShowFullScreenLobby()
    {
        if (!PhotonNetwork.InRoom)
            return false;
        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return false;

        CanvasGroup cg = EnsureCanvasGroup();
        return cg != null && cg.alpha > 0.01f && cg.interactable && cg.blocksRaycasts;
    }

    void EnsureLobbyRootFullScreen()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    void EnsureFullScreenLobbyUiExists()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (lobbyTopBarRootObject == null || !lobbyTopBarRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyTopBarRoot");
            if (misplaced != null)
            {
                lobbyTopBarRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    lobbyTopBarRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                lobbyTopBarRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyTopBarRoot", typeof(RectTransform), typeof(Image));
            }
            RectTransform rect = lobbyTopBarRootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1440f, 120f);

            Image bg = lobbyTopBarRootObject.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;
        }
        else if (fullScreenRoot != null && lobbyTopBarRootObject.transform.parent != fullScreenRoot.transform)
        {
            lobbyTopBarRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }

        ConfigureSharedLobbyTopBar();

        EnsureLobbyActionButtons();
        EnsureLobbyMapSelectionScreen();
        EnsureLobbyMapDetailsScreen();
        EnsureLobbyDeveloperSettingsRoot();
        EnsureLobbyCheatOverlay();
    }

    void ConfigureSharedLobbyTopBar()
    {
        if (lobbyTopBarRootObject == null)
            return;

        lobbyTopBar = SharedPlayerTopBarUI.Ensure(lobbyTopBarRootObject, false);
        if (lobbyTopBar == null)
            return;

        lobbyTopBarNicknameText = lobbyTopBar.NicknameText;
        lobbyTopBarGamesText = lobbyTopBar.GamesText;
        lobbyTopBarLevelXpText = lobbyTopBar.LevelXpText;
        lobbyTopBarAstronsText = lobbyTopBar.AstronsText;
    }

    TMP_Text CreateTopBarText(string name, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment)
    {
        Transform existing = lobbyTopBarRootObject.transform.Find(name);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (text == null)
            text = CreateStandaloneLabel(lobbyTopBarRootObject.transform, name, string.Empty, anchoredPosition, size, 26f, alignment);

        text.fontSize = 26f;
        text.alignment = alignment;
        text.characterSpacing = 0f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(0.95f, 0.98f, 1f, 1f);
        return text;
    }

    void EnsureLobbyActionButtons()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if ((exitLobbyButton == null || !exitLobbyButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyExitProfileBackButton");
            if (existing != null)
                exitLobbyButton = existing.GetComponent<Button>();
        }
        if ((developerSettingsButton == null || !developerSettingsButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyDeveloperSettingsWideProfileBackButton");
            if (existing == null)
                existing = fullScreenRoot.transform.Find("LobbyDeveloperSettingsProfileBackButton");
            if (existing != null)
                developerSettingsButton = existing.GetComponent<Button>();
        }
        if ((developerBackButton == null || !developerBackButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyDeveloperBackProfileBackButton");
            if (existing != null)
                developerBackButton = existing.GetComponent<Button>();
        }
        if ((launchButton == null || !launchButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyLaunchSaveAndRunButton");
            if (existing != null)
                launchButton = existing.GetComponent<Button>();
        }

        exitLobbyButton = EnsureTopActionButton(ref exitLobbyText, exitLobbyButton, "LobbyExitProfileBackButton", "LobbyExitProfileBackText", "EXIT LOBBY", OnExitLobbyClicked);
        developerSettingsButton = EnsureBottomActionButton(ref developerSettingsText, developerSettingsButton, "LobbyDeveloperSettingsWideProfileBackButton", "LobbyDeveloperSettingsWideProfileBackText", "DEVELOPER SETTINGS", OnDeveloperSettingsClicked);
        developerBackButton = EnsureTopActionButton(ref developerBackText, developerBackButton, "LobbyDeveloperBackProfileBackButton", "LobbyDeveloperBackProfileBackText", "BACK", OnDeveloperBackClicked);
        launchButton = EnsureBottomRightActionButton(ref launchText, launchButton, "LobbyLaunchSaveAndRunButton", "LobbyLaunchSaveAndRunText", "LAUNCH", OnLaunchClicked);
        developerGunSetupButton = EnsureTopActionButton(ref developerGunSetupText, developerGunSetupButton, "LobbyDeveloperGunSetupWideProfileBackButton", "LobbyDeveloperGunSetupWideProfileBackText", "GUN SETUP", OpenGunSetup);
        developerCheatButton = EnsureTopActionButton(ref developerCheatText, developerCheatButton, "LobbyDeveloperCheatWideProfileBackButton", "LobbyDeveloperCheatWideProfileBackText", "CHEAT", OnDeveloperCheatClicked);

        ApplyLobbyBackPalette(exitLobbyButton);
        ApplyLobbyBackPalette(developerSettingsButton);
        ApplyLobbyBackPalette(developerBackButton);
        ApplyLobbyBackPalette(developerGunSetupButton);
        ApplyLobbyBackPalette(developerCheatButton);

        if (fullScreenRoot != null)
        {
            if (exitLobbyButton != null && exitLobbyButton.transform.parent != fullScreenRoot.transform)
                exitLobbyButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerSettingsButton != null && developerSettingsButton.transform.parent != fullScreenRoot.transform)
                developerSettingsButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerBackButton != null && developerBackButton.transform.parent != fullScreenRoot.transform)
                developerBackButton.transform.SetParent(fullScreenRoot.transform, false);
            if (launchButton != null && launchButton.transform.parent != fullScreenRoot.transform)
                launchButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerGunSetupButton != null && developerGunSetupButton.transform.parent != fullScreenRoot.transform)
                developerGunSetupButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerCheatButton != null && developerCheatButton.transform.parent != fullScreenRoot.transform)
                developerCheatButton.transform.SetParent(fullScreenRoot.transform, false);
        }

        HideLegacyLobbyButtons();
    }

    Button EnsureTopActionButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, string label, UnityEngine.Events.UnityAction callback)
    {
        Button button = EnsureSettingButton(ref textField, existingButton, buttonName, textName, Vector2.zero, callback);
        if (button == null)
            return null;

        textField.text = label;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
        }

        return button;
    }

    Button EnsureBottomActionButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, string label, UnityEngine.Events.UnityAction callback)
    {
        Button button = EnsureSettingButton(ref textField, existingButton, buttonName, textName, Vector2.zero, callback);
        if (button == null)
            return null;

        textField.text = label;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
        }

        return button;
    }

    Button EnsureBottomRightActionButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, string label, UnityEngine.Events.UnityAction callback)
    {
        Button button = EnsureSettingButton(ref textField, existingButton, buttonName, textName, Vector2.zero, callback);
        if (button == null)
            return null;

        textField.text = label;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
        }

        return button;
    }

    void ApplyLobbyBackPalette(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.19f, 0.33f, 0.98f);
        colors.highlightedColor = new Color(0.18f, 0.28f, 0.46f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.08f, 0.12f, 0.22f, 1f);
        colors.disabledColor = new Color(0.18f, 0.24f, 0.32f, 0.48f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    void HideLegacyLobbyButtons()
    {
        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            if (button == developerSettingsButton)
                continue;

            if (button.gameObject.name == "LobbyDeveloperSettingsProfileBackButton")
                button.gameObject.SetActive(false);
        }
    }

    void SwitchLobbyScreen(LobbyScreen screen)
    {
        currentScreen = screen;
        RefreshLobbyScreenVisibility();
        RefreshLobbyTopBar();
        RefreshLobbyScreenContent();
    }

    void RefreshLobbyTopBar()
    {
        EnsureFullScreenLobbyUiExists();

        PlayerProfileData profile = PlayerProfileService.Instance != null ? PlayerProfileService.Instance.CurrentProfile : null;
        string nickname = profile != null && !string.IsNullOrWhiteSpace(profile.Nickname)
            ? profile.Nickname
            : (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName) ? PhotonNetwork.NickName : "Pilot");
        int games = profile != null ? profile.GamesPlayed : 0;
        int xp = profile != null ? profile.TotalXp : 0;
        int astrons = profile != null ? profile.Astrons : 0;
        int level = RoundXpBalance.GetLevelForTotalXp(xp);

        if (lobbyTopBarNicknameText != null)
            lobbyTopBarNicknameText.text = nickname;
        if (lobbyTopBarGamesText != null)
            lobbyTopBarGamesText.text = "Games: " + games;
        if (lobbyTopBarLevelXpText != null)
            lobbyTopBarLevelXpText.text = "Level: " + level + "  XP: " + xp;
        if (lobbyTopBarAstronsText != null)
            lobbyTopBarAstronsText.text = "Astrons: " + astrons;
        if (lobbyTopBar != null)
            lobbyTopBar.SetProfile(profile, nickname, string.Empty, true);
    }

    LobbyMapUnlockStatus GetLocalMapUnlockStatus(LobbyMapDefinition map)
    {
        if (map == null)
            return new LobbyMapUnlockStatus(string.Empty, false, "Unlock requirement unavailable.", 0, 1);

        if (PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null)
            return PlayerProfileService.Instance.GetMapUnlockStatus(map.Id);

        return LobbyMapUnlockCatalog.GetStatus(map.Id, null, 0);
    }

    void EnsureLobbyTopStatBanner(float rootWidth)
    {
        if (lobbyTopBarRootObject == null)
            return;

        if (lobbyTopStatBannerObject == null)
        {
            lobbyTopStatBannerObject = new GameObject("LobbyTopStatBanner", typeof(RectTransform), typeof(Image));
            lobbyTopStatBannerObject.transform.SetParent(lobbyTopBarRootObject.transform, false);

            GameObject innerPanelObject = new GameObject("InnerPanel", typeof(RectTransform), typeof(Image));
            innerPanelObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject topAccentObject = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
            topAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject bottomAccentObject = new GameObject("BottomAccent", typeof(RectTransform), typeof(Image));
            bottomAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject leftAccentObject = new GameObject("LeftAccent", typeof(RectTransform), typeof(Image));
            leftAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject rightAccentObject = new GameObject("RightAccent", typeof(RectTransform), typeof(Image));
            rightAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);
        }

        float bannerX = 168f;
        float bannerWidth = Mathf.Clamp(rootWidth - bannerX - 270f, 620f, 1120f);

        RectTransform rect = lobbyTopStatBannerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(bannerX, -40f);
        rect.sizeDelta = new Vector2(bannerWidth, 58f);

        Image frame = lobbyTopStatBannerObject.GetComponent<Image>();
        frame.color = new Color(0.33f, 0.39f, 0.47f, 0.94f);
        frame.raycastTarget = false;

        Transform innerPanel = lobbyTopStatBannerObject.transform.Find("InnerPanel");
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

        ConfigureLobbyTopBannerAccent("TopAccent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(128f, 4f), new Color(0.35f, 0.82f, 1f, 0.32f));
        ConfigureLobbyTopBannerAccent("BottomAccent", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(96f, 3f), new Color(0.35f, 0.82f, 1f, 0.24f));
        ConfigureLobbyTopBannerAccent("LeftAccent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));
        ConfigureLobbyTopBannerAccent("RightAccent", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));

        lobbyTopStatBannerObject.transform.SetAsFirstSibling();
    }

    void HideFullScreenLobbyFlow(bool resetScreen = true)
    {
        HideDeveloperCheatOverlay();

        if (mapSelectionRootObject != null)
            mapSelectionRootObject.SetActive(false);
        if (mapDetailsRootObject != null)
            mapDetailsRootObject.SetActive(false);
        if (developerSettingsRootObject != null)
            developerSettingsRootObject.SetActive(false);
        if (lobbyTopBarRootObject != null)
            lobbyTopBarRootObject.SetActive(false);
        if (fullScreenLobbyRootObject != null)
            fullScreenLobbyRootObject.SetActive(false);

        if (exitLobbyButton != null)
            exitLobbyButton.gameObject.SetActive(false);
        if (developerSettingsButton != null)
            developerSettingsButton.gameObject.SetActive(false);
        if (launchButton != null)
            launchButton.gameObject.SetActive(false);
        if (developerBackButton != null)
            developerBackButton.gameObject.SetActive(false);
        if (developerGunSetupButton != null)
            developerGunSetupButton.gameObject.SetActive(false);
        if (developerCheatButton != null)
            developerCheatButton.gameObject.SetActive(false);

        if (leftSettingsViewportRect != null)
            leftSettingsViewportRect.gameObject.SetActive(false);
        if (enemyTableViewportRect != null)
            enemyTableViewportRect.gameObject.SetActive(false);
        if (enemyTableRootRect != null)
            enemyTableRootRect.gameObject.SetActive(false);
        if (weaponSettingsRootRect != null)
            weaponSettingsRootRect.gameObject.SetActive(false);

        if (resetScreen)
        {
            currentScreen = LobbyScreen.MapSelection;
            if (PhotonNetwork.InRoom)
                selectedMapId = RoomSettings.GetSelectedLobbyMapId();
        }
    }

    void ConfigureLobbyTopBannerAccent(string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        if (lobbyTopStatBannerObject == null)
            return;

        Transform child = lobbyTopStatBannerObject.transform.Find(childName);
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

    void RefreshLobbyScreenVisibility()
    {
        EnsureFullScreenLobbyUiExists();

        bool shouldShowFullScreen = ShouldShowFullScreenLobby();
        if (!shouldShowFullScreen)
        {
            if (mapSelectionRootObject != null)
                mapSelectionRootObject.SetActive(false);
            if (mapDetailsRootObject != null)
                mapDetailsRootObject.SetActive(false);
            if (developerSettingsRootObject != null)
                developerSettingsRootObject.SetActive(false);
            if (lobbyTopBarRootObject != null)
                lobbyTopBarRootObject.SetActive(false);
            if (fullScreenLobbyRootObject != null)
                fullScreenLobbyRootObject.SetActive(false);
            if (exitLobbyButton != null)
                exitLobbyButton.gameObject.SetActive(false);
            if (developerSettingsButton != null)
                developerSettingsButton.gameObject.SetActive(false);
            if (launchButton != null)
                launchButton.gameObject.SetActive(false);
            if (developerBackButton != null)
                developerBackButton.gameObject.SetActive(false);
            if (developerGunSetupButton != null)
                developerGunSetupButton.gameObject.SetActive(false);
            if (developerCheatButton != null)
                developerCheatButton.gameObject.SetActive(false);
            if (leftSettingsViewportRect != null)
                leftSettingsViewportRect.gameObject.SetActive(false);
            if (enemyTableViewportRect != null)
                enemyTableViewportRect.gameObject.SetActive(false);
            if (enemyTableRootRect != null)
                enemyTableRootRect.gameObject.SetActive(false);
            if (mapEffectChanceTableViewportRect != null)
                mapEffectChanceTableViewportRect.gameObject.SetActive(false);
            if (mapEffectChanceTableRootRect != null)
                mapEffectChanceTableRootRect.gameObject.SetActive(false);
            if (weaponSettingsRootRect != null)
                weaponSettingsRootRect.gameObject.SetActive(false);
            return;
        }

        bool showMapSelection = currentScreen == LobbyScreen.MapSelection;
        bool showMapDetails = currentScreen == LobbyScreen.MapDetails;
        bool showDeveloperSettings = currentScreen == LobbyScreen.DeveloperSettings;

        if (mapSelectionRootObject != null)
        {
            mapSelectionRootObject.SetActive(showMapSelection);
            mapSelectionRootObject.transform.SetAsFirstSibling();
        }
        if (mapDetailsRootObject != null)
        {
            mapDetailsRootObject.SetActive(showMapDetails);
            mapDetailsRootObject.transform.SetAsFirstSibling();
        }
        if (developerSettingsRootObject != null)
        {
            developerSettingsRootObject.SetActive(showDeveloperSettings);
            developerSettingsRootObject.transform.SetAsFirstSibling();
        }
        if (lobbyTopBarRootObject != null)
            lobbyTopBarRootObject.SetActive(true);
        if (fullScreenLobbyRootObject != null)
        {
            fullScreenLobbyRootObject.SetActive(showMapSelection || showMapDetails || showDeveloperSettings);
            fullScreenLobbyRootObject.transform.SetAsLastSibling();
        }

        if (exitLobbyButton != null)
            exitLobbyButton.gameObject.SetActive(showMapSelection || showDeveloperSettings);
        if (developerSettingsButton != null)
            developerSettingsButton.gameObject.SetActive(showMapDetails);
        if (launchButton != null)
            launchButton.gameObject.SetActive(showMapDetails);
        if (developerBackButton != null)
            developerBackButton.gameObject.SetActive(showMapDetails || showDeveloperSettings);
        if (developerGunSetupButton != null)
            developerGunSetupButton.gameObject.SetActive(showDeveloperSettings);
        if (developerCheatButton != null)
            developerCheatButton.gameObject.SetActive(showDeveloperSettings);
        if (!showDeveloperSettings)
            HideDeveloperCheatOverlay();

        if (leftSettingsViewportRect != null)
            leftSettingsViewportRect.gameObject.SetActive(showDeveloperSettings);
        if (enemyTableViewportRect != null)
            enemyTableViewportRect.gameObject.SetActive(showDeveloperSettings);
        if (enemyTableRootRect != null)
            enemyTableRootRect.gameObject.SetActive(showDeveloperSettings);
        if (mapEffectChanceTableViewportRect != null)
            mapEffectChanceTableViewportRect.gameObject.SetActive(showDeveloperSettings);
        if (mapEffectChanceTableRootRect != null)
            mapEffectChanceTableRootRect.gameObject.SetActive(showDeveloperSettings);
        if (weaponSettingsRootRect != null)
            weaponSettingsRootRect.gameObject.SetActive(false);
        if (gunSetupSettingButton != null)
            gunSetupSettingButton.gameObject.SetActive(false);

        if (playerStatusListText != null)
            playerStatusListText.gameObject.SetActive(false);
        if (readyButton != null)
            readyButton.gameObject.SetActive(false);
        if (backToRoundsButton != null)
            backToRoundsButton.gameObject.SetActive(false);
        if (mapSelectionButton != null)
            mapSelectionButton.gameObject.SetActive(false);
        if (mapSelectionOverlayObject != null)
            mapSelectionOverlayObject.SetActive(false);

        LayoutFullScreenLobbyUi();
    }

    void RefreshLobbyScreenContent()
    {
        RefreshFullScreenMapSelectionUi();
        RefreshMapDetailsUi();
        RefreshDeveloperSettingsUi();
    }

    void LayoutFullScreenLobbyUi()
    {
        RectTransform canvasRect = EnsureFullScreenLobbyRoot();
        float canvasWidth = canvasRect != null && canvasRect.rect.width > 0f ? canvasRect.rect.width : 1920f;
        float canvasHeight = canvasRect != null && canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;
        float contentTop = FullScreenTopMargin + Mathf.Max(LobbyTopBarHeight, 110f) + 56f;
        float bottomReserved = BottomWideButtonHeight + FullScreenBottomMargin + 30f;
        float usableHeight = Mathf.Max(420f, canvasHeight - contentTop - bottomReserved);
        float tileHeight = Mathf.Min(MapTileHeight, (usableHeight - MapTileSpacingY) * 0.5f);
        float tileWidth = Mathf.Min(MapTileWidth, (canvasWidth - FullScreenSideMargin * 2f - MapTileSpacingX * 2f) / 3f);
        float previewWidth = Mathf.Min(980f, canvasWidth * 0.56f);
        float previewHeight = Mathf.Min(720f, usableHeight);
        float badgeGap = 18f;
        float detailsStartX = FullScreenSideMargin + previewWidth + badgeGap + 28f;
        float detailsWidth = Mathf.Max(320f, canvasWidth - detailsStartX - FullScreenSideMargin);
        float detailsBadgeTopOffset = Mathf.Clamp(previewHeight - MapDeathLossBadgeHeight - 16f, 330f, 430f);

        if (lobbyTopBarRootObject != null)
        {
            RectTransform rect = lobbyTopBarRootObject.GetComponent<RectTransform>();
            float rootWidth = Mathf.Max(820f, canvasWidth);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -FullScreenTopMargin);
            rect.sizeDelta = new Vector2(rootWidth, 110f);

            if (lobbyTopBar == null)
                ConfigureSharedLobbyTopBar();
            if (lobbyTopBar != null)
                lobbyTopBar.Layout(rootWidth);

            lobbyTopBarRootObject.transform.SetAsLastSibling();
        }

        LayoutActionButton(exitLobbyButton, new Vector2(-FullScreenSideMargin, -FullScreenTopMargin), new Vector2(TopActionButtonWidth, TopActionButtonHeight));
        LayoutActionButton(developerBackButton, new Vector2(-FullScreenSideMargin, -FullScreenTopMargin), new Vector2(220f, TopActionButtonHeight));
        LayoutBottomButton(developerSettingsButton, new Vector2(FullScreenSideMargin, FullScreenBottomMargin), new Vector2(390f, 66f), false);
        LayoutBottomButton(launchButton, new Vector2(-FullScreenSideMargin, FullScreenBottomMargin), new Vector2(360f, 108f), true);
        LayoutActionButton(developerGunSetupButton, new Vector2(-FullScreenSideMargin - 240f, -FullScreenTopMargin), new Vector2(220f, TopActionButtonHeight));
        LayoutActionButton(developerCheatButton, new Vector2(-FullScreenSideMargin - 480f, -FullScreenTopMargin), new Vector2(220f, TopActionButtonHeight));

        if (mapSelectionTilesRootRect != null)
        {
            int columns = 3;
            float scrollbarWidth = RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Large);
            float scrollbarGap = 14f;
            float viewportWidth = Mathf.Max(360f, canvasWidth - FullScreenSideMargin * 2f - scrollbarWidth - scrollbarGap);
            float viewportHeight = usableHeight;
            float fittedTileWidth = Mathf.Min(MapTileWidth, (viewportWidth - MapTileSpacingX * (columns - 1)) / columns);
            int rows = Mathf.CeilToInt(fullscreenMapTileButtons.Count / (float)columns);
            float contentHeight = rows > 0
                ? rows * tileHeight + Mathf.Max(0, rows - 1) * MapTileSpacingY
                : viewportHeight;
            float contentWidth = Mathf.Max(viewportWidth, columns * fittedTileWidth + (columns - 1) * MapTileSpacingX);
            float totalWidth = columns * fittedTileWidth + (columns - 1) * MapTileSpacingX;
            float startX = Mathf.Max(0f, (contentWidth - totalWidth) * 0.5f);
            bool canScroll = contentHeight > viewportHeight + 1f;
            float scrollbarVisibleSize = canScroll ? Mathf.Clamp01(viewportHeight / Mathf.Max(viewportHeight, contentHeight)) : 1f;
            float scrollbarNormalizedPosition = 1f;

            if (fullScreenMapSelectionViewportRect != null)
            {
                fullScreenMapSelectionViewportRect.anchorMin = new Vector2(0f, 1f);
                fullScreenMapSelectionViewportRect.anchorMax = new Vector2(0f, 1f);
                fullScreenMapSelectionViewportRect.pivot = new Vector2(0f, 1f);
                fullScreenMapSelectionViewportRect.anchoredPosition = new Vector2(FullScreenSideMargin, -contentTop);
                fullScreenMapSelectionViewportRect.sizeDelta = new Vector2(viewportWidth, viewportHeight);
            }

            mapSelectionTilesRootRect.anchoredPosition = Vector2.zero;
            mapSelectionTilesRootRect.sizeDelta = new Vector2(contentWidth, Mathf.Max(viewportHeight, contentHeight));
            for (int i = 0; i < fullscreenMapTileButtons.Count; i++)
            {
                RectTransform tileRect = fullscreenMapTileButtons[i].GetComponent<RectTransform>();
                int column = i % columns;
                int row = i / columns;
                tileRect.anchorMin = new Vector2(0f, 1f);
                tileRect.anchorMax = new Vector2(0f, 1f);
                tileRect.pivot = new Vector2(0f, 1f);
                tileRect.anchoredPosition = new Vector2(startX + column * (fittedTileWidth + MapTileSpacingX), -row * (tileHeight + MapTileSpacingY));
                tileRect.sizeDelta = new Vector2(fittedTileWidth, tileHeight);
            }

            if (fullScreenMapSelectionScrollbar != null)
            {
                RectTransform scrollbarRect = fullScreenMapSelectionScrollbar.GetComponent<RectTransform>();
                if (scrollbarRect != null)
                {
                    scrollbarRect.anchorMin = new Vector2(1f, 1f);
                    scrollbarRect.anchorMax = new Vector2(1f, 1f);
                    scrollbarRect.pivot = new Vector2(1f, 1f);
                    scrollbarRect.anchoredPosition = new Vector2(-FullScreenSideMargin, -contentTop);
                    scrollbarRect.sizeDelta = new Vector2(scrollbarWidth, viewportHeight);
                }

                fullScreenMapSelectionScrollbar.gameObject.SetActive(canScroll);
            }

            if (fullScreenMapSelectionScrollRect != null)
            {
                fullScreenMapSelectionScrollRect.viewport = fullScreenMapSelectionViewportRect;
                fullScreenMapSelectionScrollRect.content = mapSelectionTilesRootRect;
                fullScreenMapSelectionScrollRect.vertical = canScroll;
                fullScreenMapSelectionScrollRect.enabled = true;

                if (!canScroll)
                {
                    fullScreenMapSelectionScrollRect.StopMovement();
                    fullScreenMapSelectionScrollRect.verticalNormalizedPosition = 1f;
                    mapSelectionTilesRootRect.anchoredPosition = Vector2.zero;
                    scrollbarNormalizedPosition = 1f;
                }
                else if (!fullScreenMapSelectionScrollInitialized)
                {
                    fullScreenMapSelectionScrollRect.StopMovement();
                    fullScreenMapSelectionScrollRect.verticalNormalizedPosition = 1f;
                    fullScreenMapSelectionScrollInitialized = true;
                    scrollbarNormalizedPosition = 1f;
                }
                else
                {
                    scrollbarNormalizedPosition = Mathf.Clamp01(fullScreenMapSelectionScrollRect.verticalNormalizedPosition);
                }
            }

            RuntimeScrollbarStyler.ApplyVerticalState(fullScreenMapSelectionScrollbar, scrollbarNormalizedPosition, scrollbarVisibleSize);

            if (mapSelectionScreenTitleText != null)
                mapSelectionScreenTitleText.rectTransform.anchoredPosition = new Vector2(FullScreenSideMargin, -(contentTop - 30f));
        }

        if (mapDetailsRootObject != null)
        {
            RectTransform previewRect = mapDetailsPreviewImage != null ? mapDetailsPreviewImage.rectTransform : null;
            if (previewRect != null)
            {
                previewRect.anchoredPosition = new Vector2(FullScreenSideMargin, -contentTop);
                previewRect.sizeDelta = new Vector2(previewWidth, previewHeight);
            }

            if (mapDetailsNameText != null)
            {
                RectTransform nameRect = mapDetailsNameText.rectTransform;
                nameRect.anchoredPosition = new Vector2(detailsStartX, -contentTop);
                nameRect.sizeDelta = new Vector2(detailsWidth, 40f);
            }

            if (mapDetailsLossBadgeObject != null)
            {
                RectTransform badgeRect = mapDetailsLossBadgeObject.GetComponent<RectTransform>();
                if (badgeRect != null)
                {
                    badgeRect.anchorMin = new Vector2(0f, 1f);
                    badgeRect.anchorMax = new Vector2(0f, 1f);
                    badgeRect.pivot = new Vector2(0f, 1f);
                    float badgeX = detailsStartX + Mathf.Max(0f, (detailsWidth - MapDeathLossBadgeWidth) * 0.5f);
                    badgeRect.anchoredPosition = new Vector2(badgeX, -contentTop - detailsBadgeTopOffset);
                    badgeRect.sizeDelta = new Vector2(MapDeathLossBadgeWidth, MapDeathLossBadgeHeight);
                }

                mapDetailsLossBadgeObject.transform.SetAsLastSibling();
            }

            if (mapDetailsDescriptionText != null)
            {
                const float descriptionTopOffset = 64f;
                const float landingSitesGap = 14f;
                const float badgeSafetyGap = 18f;
                float detailsAvailableBeforeBadge = Mathf.Max(220f, detailsBadgeTopOffset - descriptionTopOffset - badgeSafetyGap);
                float landingSitesHeight = mapDetailsLandingSitesText != null
                    ? Mathf.Clamp(detailsAvailableBeforeBadge * 0.34f, 86f, 126f)
                    : 0f;
                float descriptionHeight = Mathf.Max(120f, detailsAvailableBeforeBadge - landingSitesHeight - (landingSitesHeight > 0f ? landingSitesGap : 0f));

                RectTransform descRect = mapDetailsDescriptionText.rectTransform;
                descRect.anchoredPosition = new Vector2(detailsStartX, -contentTop - descriptionTopOffset);
                descRect.sizeDelta = new Vector2(detailsWidth, descriptionHeight);

                if (mapDetailsLandingSitesText != null)
                {
                    RectTransform landingSitesRect = mapDetailsLandingSitesText.rectTransform;
                    landingSitesRect.anchorMin = new Vector2(0f, 1f);
                    landingSitesRect.anchorMax = new Vector2(0f, 1f);
                    landingSitesRect.pivot = new Vector2(0f, 1f);
                    landingSitesRect.anchoredPosition = new Vector2(detailsStartX, -contentTop - descriptionTopOffset - descriptionHeight - landingSitesGap);
                    landingSitesRect.sizeDelta = new Vector2(detailsWidth, Mathf.Max(0f, landingSitesHeight));
                }
            }
            else if (mapDetailsLandingSitesText != null)
            {
                RectTransform landingSitesRect = mapDetailsLandingSitesText.rectTransform;
                landingSitesRect.anchorMin = new Vector2(0f, 1f);
                landingSitesRect.anchorMax = new Vector2(0f, 1f);
                landingSitesRect.pivot = new Vector2(0f, 1f);
                landingSitesRect.anchoredPosition = new Vector2(detailsStartX, -contentTop - 64f);
                landingSitesRect.sizeDelta = new Vector2(detailsWidth, 126f);
            }
        }
    }

    void LayoutActionButton(Button button, Vector2 anchoredFromTopRight, Vector2 size)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredFromTopRight;
        rect.sizeDelta = size;
        button.transform.SetAsLastSibling();
    }

    void LayoutBottomButton(Button button, Vector2 anchoredFromCorner, Vector2 size, bool rightAnchored)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rect.anchorMax = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rect.pivot = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredFromCorner;
        rect.sizeDelta = size;
        button.transform.SetAsLastSibling();
    }

}
