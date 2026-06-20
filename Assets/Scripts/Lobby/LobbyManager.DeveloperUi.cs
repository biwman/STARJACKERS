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
    void EnsureLobbyDeveloperSettingsRoot()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (developerSettingsRootObject == null || !developerSettingsRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyDeveloperSettingsScreen");
            if (misplaced != null)
            {
                developerSettingsRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    developerSettingsRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                developerSettingsRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyDeveloperSettingsScreen", typeof(RectTransform));
            }
            RectTransform rootRect = developerSettingsRootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }
        else if (fullScreenRoot != null && developerSettingsRootObject.transform.parent != fullScreenRoot.transform)
        {
            developerSettingsRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }
    }

    void EnsureLobbyCheatOverlay()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (developerCheatOverlayObject != null && developerCheatOverlayObject.scene.IsValid())
        {
            if (fullScreenRoot != null && developerCheatOverlayObject.transform.parent != fullScreenRoot.transform)
                developerCheatOverlayObject.transform.SetParent(fullScreenRoot.transform, false);
            return;
        }

        GameObject overlayObject = new GameObject("LobbyDeveloperCheatOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(fullScreenRoot != null ? fullScreenRoot.transform : transform, false);
        developerCheatOverlayObject = overlayObject;

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = overlayObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

        GameObject panel = new GameObject("LobbyDeveloperCheatPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlayObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(620f, 960f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.11f, 0.1f, 0.14f, 0.98f);

        TMP_Text title = CreateStandaloneLabel(panel.transform, "CheatBrowserTitle", "CHEAT", new Vector2(100f, -40f), new Vector2(420f, 34f), 30f, TextAlignmentOptions.Center);
        title.characterSpacing = 3f;
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        TMP_Text hint = CreateStandaloneLabel(panel.transform, "CheatBrowserHint", "This is temporary solution to speed up tests.", new Vector2(50f, -106f), new Vector2(520f, 64f), 19f, TextAlignmentOptions.Center);
        hint.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        hint.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        hint.rectTransform.pivot = new Vector2(0.5f, 1f);
        hint.fontStyle = FontStyles.Normal;
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.color = new Color(0.86f, 0.9f, 0.96f, 0.96f);

        developerCheatAstronsText = CreateStandaloneLabel(panel.transform, "CheatAstronsText", "Astrons: 0", new Vector2(90f, -156f), new Vector2(440f, 28f), 20f, TextAlignmentOptions.Center);
        developerCheatAstronsText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        developerCheatAstronsText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        developerCheatAstronsText.rectTransform.pivot = new Vector2(0.5f, 1f);
        developerCheatAstronsText.fontStyle = FontStyles.Normal;
        developerCheatAstronsText.color = new Color(0.94f, 0.84f, 0.44f, 1f);

        developerCheatXpText = CreateStandaloneLabel(panel.transform, "CheatXpText", "Level: 1  XP: 0", new Vector2(90f, -190f), new Vector2(440f, 28f), 20f, TextAlignmentOptions.Center);
        developerCheatXpText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        developerCheatXpText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        developerCheatXpText.rectTransform.pivot = new Vector2(0.5f, 1f);
        developerCheatXpText.fontStyle = FontStyles.Normal;
        developerCheatXpText.color = new Color(0.74f, 0.92f, 1f, 1f);

        developerCheatAddMoneyButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatAddMoneyButton", "ADD MONEY", new Vector2(0f, -238f), new Vector2(260f, 54f), new Color(0.5f, 0.22f, 0.18f, 1f), new Color(0.7f, 0.3f, 0.22f, 1f), OnDeveloperCheatAddMoneyClicked);
        developerCheatAddXpButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatAddXpButton", "ADD XP", new Vector2(0f, -300f), new Vector2(260f, 54f), new Color(0.16f, 0.38f, 0.5f, 1f), new Color(0.22f, 0.52f, 0.7f, 1f), OnDeveloperCheatAddXpClicked);
        developerCheatUnlockBlueprintsButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatUnlockBlueprintsButton", "UNLOCK ALL BLUEPRINTS", new Vector2(0f, -362f), new Vector2(340f, 54f), new Color(0.12f, 0.42f, 0.46f, 1f), new Color(0.18f, 0.58f, 0.64f, 1f), OnDeveloperCheatUnlockBlueprintsClicked);
        developerCheatLockBlueprintsButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatLockBlueprintsButton", "LOCK ALL BLUEPRINTS", new Vector2(0f, -424f), new Vector2(340f, 54f), new Color(0.42f, 0.23f, 0.13f, 1f), new Color(0.58f, 0.34f, 0.18f, 1f), OnDeveloperCheatLockBlueprintsClicked);
        developerCheatUnlockShipsButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatUnlockShipsButton", "UNLOCK ALL SHIPS", new Vector2(0f, -486f), new Vector2(340f, 54f), new Color(0.14f, 0.34f, 0.48f, 1f), new Color(0.2f, 0.48f, 0.68f, 1f), OnDeveloperCheatUnlockShipsClicked);
        developerCheatLockShipsButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatLockShipsButton", "LOCK ALL SHIPS", new Vector2(0f, -548f), new Vector2(340f, 54f), new Color(0.34f, 0.22f, 0.34f, 1f), new Color(0.5f, 0.32f, 0.5f, 1f), OnDeveloperCheatLockShipsClicked);
        developerCheatUnlockMapsButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatUnlockMapsButton", "UNLOCK ALL MAPS", new Vector2(0f, -610f), new Vector2(340f, 54f), new Color(0.14f, 0.36f, 0.26f, 1f), new Color(0.2f, 0.54f, 0.38f, 1f), OnDeveloperCheatUnlockMapsClicked);
        developerCheatLockMapsButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatLockMapsButton", "LOCK ALL MAPS", new Vector2(0f, -672f), new Vector2(340f, 54f), new Color(0.36f, 0.28f, 0.16f, 1f), new Color(0.5f, 0.4f, 0.22f, 1f), OnDeveloperCheatLockMapsClicked);
        developerCheatResetAccountButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatResetAccountButton", "RESET ACCOUNT", new Vector2(0f, -740f), new Vector2(260f, 54f), new Color(0.52f, 0.14f, 0.18f, 1f), new Color(0.72f, 0.2f, 0.25f, 1f), OnDeveloperCheatResetAccountClicked);

        developerCheatStatusText = CreateStandaloneLabel(panel.transform, "CheatStatusText", string.Empty, new Vector2(60f, -810f), new Vector2(500f, 28f), 17f, TextAlignmentOptions.Center);
        developerCheatStatusText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        developerCheatStatusText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        developerCheatStatusText.rectTransform.pivot = new Vector2(0.5f, 1f);
        developerCheatStatusText.fontStyle = FontStyles.Normal;
        developerCheatStatusText.color = new Color(0.74f, 0.86f, 0.94f, 0.96f);

        developerCheatCloseButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatCloseButton", "CLOSE", new Vector2(0f, -858f), new Vector2(220f, 52f), new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f), HideDeveloperCheatOverlay);

        CreateDeveloperCheatResetConfirm(overlayObject.transform);

        developerCheatOverlayObject.SetActive(false);
    }

    void CreateDeveloperCheatResetConfirm(Transform parent)
    {
        developerCheatResetConfirmObject = new GameObject("LobbyDeveloperCheatResetConfirm", typeof(RectTransform), typeof(Image));
        developerCheatResetConfirmObject.transform.SetParent(parent, false);
        RectTransform overlayRect = developerCheatResetConfirmObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = developerCheatResetConfirmObject.GetComponent<Image>();
        overlay.color = new Color(0.01f, 0.015f, 0.025f, 0.78f);
        overlay.raycastTarget = true;

        GameObject panel = new GameObject("LobbyDeveloperCheatResetConfirmPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(developerCheatResetConfirmObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(640f, 330f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.13f, 0.09f, 0.11f, 0.98f);

        TMP_Text title = CreateStandaloneLabel(panel.transform, "LobbyDeveloperCheatResetTitle", "RESET ACCOUNT", new Vector2(40f, -36f), new Vector2(560f, 36f), 26f, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        TMP_Text body = CreateStandaloneLabel(panel.transform, "LobbyDeveloperCheatResetText", "This will reset XP, level, Astrons to " + PlayerProfileService.DefaultStartingAstrons + ", inventory, equipment, blueprints, unlocked pilots and map progress. Continue?", new Vector2(40f, -102f), new Vector2(560f, 96f), 20f, TextAlignmentOptions.Center);
        body.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        body.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.fontStyle = FontStyles.Normal;
        body.textWrappingMode = TextWrappingModes.Normal;

        developerCheatResetConfirmYesButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatResetYesButton", "YES", new Vector2(-122f, -238f), new Vector2(190f, 56f), new Color(0.56f, 0.12f, 0.16f, 1f), new Color(0.74f, 0.18f, 0.22f, 1f), OnDeveloperCheatResetConfirmClicked);
        developerCheatResetConfirmCancelButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatResetCancelButton", "CANCEL", new Vector2(122f, -238f), new Vector2(190f, 56f), new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f), HideDeveloperCheatResetConfirm);

        developerCheatResetConfirmObject.SetActive(false);
    }

    Button CreateLobbyOverlayButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color baseColor, Color highlightedColor, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = baseColor;
        image.type = Image.Type.Sliced;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PlayUiClickAndInvoke(callback));
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = highlightedColor;
        colors.selectedColor = highlightedColor;
        colors.pressedColor = baseColor * 0.82f;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateStandaloneLabel(buttonObject.transform, name + "Text", label, Vector2.zero, size, 24f, TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 2f;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        return button;
    }

    void PlayUiClickAndInvoke(UnityEngine.Events.UnityAction callback)
    {
        AudioManager.Instance?.PlayClick();
        callback?.Invoke();
    }

    void HideDeveloperCheatOverlay()
    {
        if (developerCheatOverlayObject != null)
            developerCheatOverlayObject.SetActive(false);
        HideDeveloperCheatResetConfirm();
    }

    void HideDeveloperCheatResetConfirm()
    {
        if (developerCheatResetConfirmObject != null)
            developerCheatResetConfirmObject.SetActive(false);
    }

    void RefreshDeveloperCheatOverlay(string statusMessage = null, bool busy = false)
    {
        if (developerCheatOverlayObject == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance != null ? PlayerProfileService.Instance.CurrentProfile : null;
        int astrons = profile != null ? profile.Astrons : 0;
        if (developerCheatAstronsText != null)
            developerCheatAstronsText.text = "Astrons: " + astrons;

        int totalXp = profile != null ? profile.TotalXp : 0;
        if (developerCheatXpText != null)
            developerCheatXpText.text = "Level: " + RoundXpBalance.GetLevelForTotalXp(totalXp) + "  XP: " + totalXp;

        if (statusMessage != null && developerCheatStatusText != null)
            developerCheatStatusText.text = statusMessage;

        if (developerCheatAddMoneyButton != null)
            developerCheatAddMoneyButton.interactable = !busy;
        if (developerCheatAddXpButton != null)
            developerCheatAddXpButton.interactable = !busy;
        if (developerCheatUnlockBlueprintsButton != null)
            developerCheatUnlockBlueprintsButton.interactable = !busy;
        if (developerCheatLockBlueprintsButton != null)
            developerCheatLockBlueprintsButton.interactable = !busy;
        if (developerCheatUnlockShipsButton != null)
            developerCheatUnlockShipsButton.interactable = !busy;
        if (developerCheatLockShipsButton != null)
            developerCheatLockShipsButton.interactable = !busy;
        if (developerCheatUnlockMapsButton != null)
            developerCheatUnlockMapsButton.interactable = !busy;
        if (developerCheatLockMapsButton != null)
            developerCheatLockMapsButton.interactable = !busy;
        if (developerCheatResetAccountButton != null)
            developerCheatResetAccountButton.interactable = !busy;
        if (developerCheatResetConfirmYesButton != null)
            developerCheatResetConfirmYesButton.interactable = !busy;
        if (developerCheatResetConfirmCancelButton != null)
            developerCheatResetConfirmCancelButton.interactable = !busy;
        if (developerCheatCloseButton != null)
            developerCheatCloseButton.interactable = !busy;

        EnsureDeveloperCheatLayering();
    }

    void EnsureDeveloperCheatLayering()
    {
        if (developerCheatOverlayObject == null || !developerCheatOverlayObject.activeSelf)
            return;

        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (fullScreenRoot != null && developerCheatOverlayObject.transform.parent != fullScreenRoot.transform)
            developerCheatOverlayObject.transform.SetParent(fullScreenRoot.transform, false);

        developerCheatOverlayObject.transform.SetAsLastSibling();

        if (developerCheatResetConfirmObject != null && developerCheatResetConfirmObject.activeSelf)
            developerCheatResetConfirmObject.transform.SetAsLastSibling();
    }

    void RefreshDeveloperSettingsUi()
    {
        EnsureHostSettingsUiExists();
        EnsureWeaponSettingsPanel();
        LayoutDeveloperSettingsRoots();
        LayoutDeveloperWeaponButtons();
        EnsureDeveloperCheatLayering();
    }

    void EnsureWeaponSettingsPanel()
    {
        if (weaponSettingsRootRect != null && weaponSettingsRootRect.gameObject.scene.IsValid())
            return;

        GameObject panelObject = FindOrCreateChild(developerSettingsRootObject != null ? developerSettingsRootObject : gameObject, "WeaponSettingsPanel", typeof(RectTransform), typeof(Image));
        weaponSettingsRootRect = panelObject.GetComponent<RectTransform>();
        weaponSettingsRootRect.anchorMin = new Vector2(0.5f, 1f);
        weaponSettingsRootRect.anchorMax = new Vector2(0.5f, 1f);
        weaponSettingsRootRect.pivot = new Vector2(0.5f, 1f);

        Image bg = panelObject.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;

        Transform staleTitle = panelObject.transform.Find("WeaponSettingsTitle");
        if (staleTitle != null)
            staleTitle.gameObject.SetActive(false);
    }

    void LayoutDeveloperSettingsRoots()
    {
        if (developerSettingsRootObject != null)
            developerSettingsRootObject.transform.SetAsLastSibling();

        if (leftSettingsViewportRect != null)
        {
            if (leftSettingsViewportRect.transform.parent != developerSettingsRootObject.transform)
                leftSettingsViewportRect.transform.SetParent(developerSettingsRootObject.transform, false);
            leftSettingsViewportRect.anchorMin = new Vector2(0f, 1f);
            leftSettingsViewportRect.anchorMax = new Vector2(0f, 1f);
            leftSettingsViewportRect.pivot = new Vector2(0f, 1f);
            leftSettingsViewportRect.anchoredPosition = new Vector2(FullScreenSideMargin, -118f);
            leftSettingsViewportRect.sizeDelta = new Vector2(660f, 840f);
        }

        RectTransform enemyLayoutRect = enemyTableViewportRect != null ? enemyTableViewportRect : enemyTableRootRect;
        if (enemyLayoutRect != null)
        {
            if (enemyLayoutRect.transform.parent != developerSettingsRootObject.transform)
                enemyLayoutRect.transform.SetParent(developerSettingsRootObject.transform, false);
            enemyLayoutRect.anchorMin = new Vector2(0f, 1f);
            enemyLayoutRect.anchorMax = new Vector2(0f, 1f);
            enemyLayoutRect.pivot = new Vector2(0f, 1f);
            enemyLayoutRect.anchoredPosition = new Vector2(740f, -118f);
            enemyLayoutRect.sizeDelta = new Vector2(1120f, 620f);
        }

        if (enemyTableRootRect != null && enemyTableViewportRect != null)
        {
            if (enemyTableRootRect.transform.parent != enemyTableViewportRect.transform)
                enemyTableRootRect.transform.SetParent(enemyTableViewportRect, false);
            enemyTableRootRect.anchorMin = new Vector2(0f, 1f);
            enemyTableRootRect.anchorMax = new Vector2(0f, 1f);
            enemyTableRootRect.pivot = new Vector2(0f, 1f);
            enemyTableRootRect.anchoredPosition = Vector2.zero;
            enemyTableRootRect.sizeDelta = new Vector2(RightTableWidth, ResolveEnemyTableContentHeight());
        }

        RectTransform mapEffectChanceLayoutRect = mapEffectChanceTableViewportRect != null ? mapEffectChanceTableViewportRect : mapEffectChanceTableRootRect;
        if (mapEffectChanceLayoutRect != null)
        {
            if (mapEffectChanceLayoutRect.transform.parent != developerSettingsRootObject.transform)
                mapEffectChanceLayoutRect.transform.SetParent(developerSettingsRootObject.transform, false);
            mapEffectChanceLayoutRect.anchorMin = new Vector2(0f, 1f);
            mapEffectChanceLayoutRect.anchorMax = new Vector2(0f, 1f);
            mapEffectChanceLayoutRect.pivot = new Vector2(0f, 1f);
            mapEffectChanceLayoutRect.anchoredPosition = new Vector2(740f, -760f);
            mapEffectChanceLayoutRect.sizeDelta = new Vector2(RightTableWidth, MapEffectChanceTableHeight);
        }

        if (mapEffectChanceTableRootRect != null && mapEffectChanceTableViewportRect != null)
        {
            if (mapEffectChanceTableRootRect.transform.parent != mapEffectChanceTableViewportRect.transform)
                mapEffectChanceTableRootRect.transform.SetParent(mapEffectChanceTableViewportRect.transform, false);
            mapEffectChanceTableRootRect.anchorMin = new Vector2(0f, 1f);
            mapEffectChanceTableRootRect.anchorMax = new Vector2(0f, 1f);
            mapEffectChanceTableRootRect.pivot = new Vector2(0f, 1f);
            mapEffectChanceTableRootRect.anchoredPosition = Vector2.zero;
            mapEffectChanceTableRootRect.sizeDelta = new Vector2(RightTableWidth, ResolveMapEffectChanceTableContentHeight());
        }

        if (weaponSettingsRootRect != null)
        {
            if (weaponSettingsRootRect.transform.parent != developerSettingsRootObject.transform)
                weaponSettingsRootRect.transform.SetParent(developerSettingsRootObject.transform, false);
            weaponSettingsRootRect.anchorMin = new Vector2(0f, 1f);
            weaponSettingsRootRect.anchorMax = new Vector2(0f, 1f);
            weaponSettingsRootRect.pivot = new Vector2(0f, 1f);
            weaponSettingsRootRect.anchoredPosition = new Vector2(740f, -760f);
            weaponSettingsRootRect.sizeDelta = new Vector2(1120f, 190f);
        }
    }

    void LayoutDeveloperWeaponButtons()
    {
        if (weaponSettingsRootRect != null)
            weaponSettingsRootRect.gameObject.SetActive(false);
    }

    void AttachWeaponSettingToPanel(Button button, float x, float y, float width)
    {
        if (button == null || weaponSettingsRootRect == null)
            return;

        button.transform.SetParent(weaponSettingsRootRect, false);
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 60f);
    }
}
