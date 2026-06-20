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
    void EnsureLobbyMapUiExists()
    {
        if (mapSelectionButton == null || !mapSelectionButton.gameObject.scene.IsValid())
        {
            GameObject buttonObject = FindOrCreateChild(gameObject, "LobbyMapSelectionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(MapSelectionButtonX, MapSelectionButtonY);
            rect.sizeDelta = new Vector2(MapSelectionButtonWidth, MapSelectionButtonHeight);

            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;

            mapSelectionButton = buttonObject.GetComponent<Button>();
            mapSelectionButton.transition = Selectable.Transition.ColorTint;
            mapSelectionButton.onClick.RemoveAllListeners();
            mapSelectionButton.onClick.AddListener(() => PlayUiClickAndInvoke(OnMapSelectionClicked));

            GameObject labelBackdropObject = FindOrCreateChild(buttonObject, "MapLabelBackdrop", typeof(RectTransform), typeof(Image));
            RectTransform labelBackdropRect = labelBackdropObject.GetComponent<RectTransform>();
            labelBackdropRect.anchorMin = new Vector2(0f, 1f);
            labelBackdropRect.anchorMax = new Vector2(1f, 1f);
            labelBackdropRect.pivot = new Vector2(0.5f, 1f);
            labelBackdropRect.anchoredPosition = Vector2.zero;
            labelBackdropRect.sizeDelta = new Vector2(0f, 54f);
            Image labelBackdropImage = labelBackdropObject.GetComponent<Image>();
            labelBackdropImage.color = new Color(0.02f, 0.04f, 0.07f, 0.78f);

            GameObject textObject = FindOrCreateChild(buttonObject, "LobbyMapSelectionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 12f);
            textRect.offsetMax = new Vector2(-18f, -12f);

            mapSelectionText = textObject.GetComponent<TextMeshProUGUI>();
            mapSelectionText.fontSize = 24f;
            mapSelectionText.fontStyle = FontStyles.Bold;
            mapSelectionText.alignment = TextAlignmentOptions.TopLeft;
            mapSelectionText.color = Color.white;
            mapSelectionText.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text reference = FindAnyObjectByType<TMP_Text>();
            if (reference != null)
            {
                mapSelectionText.font = reference.font;
                mapSelectionText.fontSharedMaterial = reference.fontSharedMaterial;
            }
        }

        EnsureMapSelectionOverlayUiExists();
    }

    void EnsureMapSelectionOverlayUiExists()
    {
        if (mapSelectionOverlayObject != null && mapSelectionOverlayObject.scene.IsValid())
            return;

        mapSelectionOverlayObject = FindOrCreateChild(gameObject, "LobbyMapSelectionOverlay", typeof(RectTransform), typeof(Image));
        RectTransform overlayRect = mapSelectionOverlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = mapSelectionOverlayObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.02f, 0.04f, 0.58f);
        overlayImage.raycastTarget = true;

        GameObject panelObject = FindOrCreateChild(mapSelectionOverlayObject, "LobbyMapSelectionPanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -10f);
        panelRect.sizeDelta = new Vector2(1540f, 880f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);

        mapSelectionOverlayTitleText = CreateStandaloneLabel(panelObject.transform, "LobbyMapSelectionTitle", "SELECT MAP", new Vector2(34f, -26f), new Vector2(400f, 34f), 28f, TextAlignmentOptions.Left);

        TMP_Text subtitle = CreateStandaloneLabel(panelObject.transform, "LobbyMapSelectionSubtitle", "Choose a preset map for the round.", new Vector2(36f, -68f), new Vector2(540f, 24f), 16f, TextAlignmentOptions.Left);
        subtitle.fontStyle = FontStyles.Normal;
        subtitle.color = new Color(0.78f, 0.84f, 0.91f, 0.92f);

        GameObject viewportObject = FindOrCreateChild(panelObject, "LobbyMapSelectionViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 1f);
        viewportRect.anchorMax = new Vector2(0.5f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 1f);
        viewportRect.anchoredPosition = new Vector2(-18f, -116f);
        viewportRect.sizeDelta = new Vector2(1340f, 700f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.04f, 0.06f, 0.09f, 0.42f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = FindOrCreateChild(viewportObject, "LobbyMapSelectionContent", typeof(RectTransform));
        mapSelectionContentRect = contentObject.GetComponent<RectTransform>();
        mapSelectionContentRect.anchorMin = new Vector2(0f, 1f);
        mapSelectionContentRect.anchorMax = new Vector2(0f, 1f);
        mapSelectionContentRect.pivot = new Vector2(0f, 1f);
        mapSelectionContentRect.anchoredPosition = Vector2.zero;

        GameObject scrollbarObject = FindOrCreateChild(panelObject, "LobbyMapSelectionScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.5f, 1f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 1f);
        scrollbarRect.pivot = new Vector2(0f, 1f);
        scrollbarRect.anchoredPosition = new Vector2(680f, -116f);
        scrollbarRect.sizeDelta = new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Large), 700f);

        Scrollbar scrollbar = RuntimeScrollbarStyler.ApplyVertical(scrollbarObject, RuntimeScrollbarStyler.Size.Large, RuntimeScrollbarStyler.Tone.Mint);

        mapSelectionScrollRect = viewportObject.GetComponent<ScrollRect>();
        mapSelectionScrollRect.horizontal = false;
        mapSelectionScrollRect.vertical = true;
        mapSelectionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        mapSelectionScrollRect.viewport = viewportRect;
        mapSelectionScrollRect.content = mapSelectionContentRect;
        mapSelectionScrollRect.verticalScrollbar = scrollbar;
        mapSelectionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        mapSelectionScrollRect.scrollSensitivity = 36f;

        GameObject closeButtonObject = FindOrCreateChild(panelObject, "LobbyMapSelectionCloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        mapSelectionOverlayCloseButton = closeButtonObject.GetComponent<Button>();
        if (mapSelectionOverlayCloseButton != null)
        {
            mapSelectionOverlayCloseButton.onClick.RemoveAllListeners();
            mapSelectionOverlayCloseButton.onClick.AddListener(() => PlayUiClickAndInvoke(HideMapSelectionOverlay));

            RectTransform closeRect = closeButtonObject.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-28f, -22f);
            closeRect.sizeDelta = new Vector2(190f, 52f);

            Image closeImage = closeButtonObject.GetComponent<Image>();
            closeImage.color = new Color(0.16f, 0.34f, 0.58f, 0.98f);

            Transform existingCloseText = closeButtonObject.transform.Find("LobbyMapSelectionCloseText");
            TMP_Text closeText = existingCloseText != null
                ? existingCloseText.GetComponent<TMP_Text>()
                : null;
            if (closeText == null)
            {
                GameObject closeTextObject = new GameObject("LobbyMapSelectionCloseText", typeof(RectTransform), typeof(TextMeshProUGUI));
                closeTextObject.transform.SetParent(closeButtonObject.transform, false);
                RectTransform closeTextRect = closeTextObject.GetComponent<RectTransform>();
                closeTextRect.anchorMin = Vector2.zero;
                closeTextRect.anchorMax = Vector2.one;
                closeTextRect.offsetMin = Vector2.zero;
                closeTextRect.offsetMax = Vector2.zero;
                closeText = closeTextObject.GetComponent<TMP_Text>();

                TMP_Text reference = FindAnyObjectByType<TMP_Text>();
                if (reference != null)
                {
                    closeText.font = reference.font;
                    closeText.fontSharedMaterial = reference.fontSharedMaterial;
                }
            }

            closeText.text = "CLOSE";
            closeText.fontSize = 22f;
            closeText.fontStyle = FontStyles.Bold;
            closeText.characterSpacing = 1.2f;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;
        }

        mapSelectionTileButtons.Clear();
        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
        int rows = Mathf.CeilToInt(maps.Count / 2f);
        mapSelectionContentRect.sizeDelta = new Vector2(1280f, Mathf.Max(700f, rows * 280f + 20f));
        for (int i = 0; i < maps.Count; i++)
        {
            LobbyMapDefinition map = maps[i];
            GameObject tileObject = new GameObject("LobbyMapTile_" + map.Id, typeof(RectTransform), typeof(Image), typeof(Button));
            tileObject.transform.SetParent(mapSelectionContentRect, false);

            RectTransform tileRect = tileObject.GetComponent<RectTransform>();
            tileRect.anchorMin = new Vector2(0f, 1f);
            tileRect.anchorMax = new Vector2(0f, 1f);
            tileRect.pivot = new Vector2(0.5f, 1f);
            int column = i % 2;
            int row = i / 2;
            tileRect.anchoredPosition = new Vector2(290f + (column * 620f), -20f - (row * 280f));
            tileRect.sizeDelta = new Vector2(540f, 250f);

            Image tileImage = tileObject.GetComponent<Image>();
            tileImage.color = Color.white;

            Button tileButton = tileObject.GetComponent<Button>();
            string mapId = map.Id;
            tileButton.onClick.AddListener(() =>
            {
                OnMapTileSelected(mapId);
            });
            mapSelectionTileButtons.Add(tileButton);

            GameObject tileLabelBackdrop = FindOrCreateChild(tileObject, "TileLabelBackdrop", typeof(RectTransform), typeof(Image));
            RectTransform tileLabelBackdropRect = tileLabelBackdrop.GetComponent<RectTransform>();
            tileLabelBackdropRect.anchorMin = new Vector2(0f, 1f);
            tileLabelBackdropRect.anchorMax = new Vector2(1f, 1f);
            tileLabelBackdropRect.pivot = new Vector2(0.5f, 1f);
            tileLabelBackdropRect.anchoredPosition = Vector2.zero;
            tileLabelBackdropRect.sizeDelta = new Vector2(0f, 54f);
            tileLabelBackdrop.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.07f, 0.78f);

            TMP_Text tileTitle = CreateStandaloneLabel(tileObject.transform, "TileTitle", map.DisplayName, new Vector2(18f, -14f), new Vector2(320f, 30f), 24f, TextAlignmentOptions.Left);
            TMP_Text tileSubtitle = CreateStandaloneLabel(tileObject.transform, "TileSubtitle", "PRESET MAP", new Vector2(18f, -46f), new Vector2(220f, 22f), 15f, TextAlignmentOptions.Left);
            tileSubtitle.fontStyle = FontStyles.Normal;
            tileSubtitle.color = new Color(0.82f, 0.88f, 0.94f, 0.9f);
        }

        if (mapSelectionScrollRect != null)
        {
            float overlayViewportHeight = viewportRect != null ? viewportRect.sizeDelta.y : 700f;
            float overlayContentHeight = mapSelectionContentRect != null ? mapSelectionContentRect.sizeDelta.y : overlayViewportHeight;
            float overlayScrollbarSize = Mathf.Clamp01(overlayViewportHeight / Mathf.Max(overlayViewportHeight, overlayContentHeight));
            mapSelectionScrollRect.StopMovement();
            mapSelectionScrollRect.verticalNormalizedPosition = 1f;
            RuntimeScrollbarStyler.ApplyVerticalState(scrollbar, 1f, overlayScrollbarSize);
        }

        mapSelectionOverlayObject.SetActive(false);
    }

    void EnsureBottomActionButtonsLayout()
    {
        PositionBottomActionButton(readyButton, BottomActionReadyX, BottomActionButtonsY, new Vector2(BottomActionButtonWidth, BottomActionButtonHeight));
        PositionBottomActionButton(backToRoundsButton, BottomActionBackX, BottomActionButtonsY, new Vector2(BottomActionButtonWidth, BottomActionButtonHeight));

        if (readyText != null)
        {
            readyText.fontSize = 34f;
            readyText.characterSpacing = 2.2f;
        }

        if (backToRoundsText != null)
        {
            backToRoundsText.fontSize = 30f;
            backToRoundsText.characterSpacing = 2.2f;
        }
    }

    void RefreshLobbyMapSelectionUi(bool isHost)
    {
        EnsureLobbyMapUiExists();

        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId());
        LobbyMapUnlockStatus selectedUnlockStatus = GetLocalMapUnlockStatus(selectedMap);
        bool selectedUnlocked = selectedUnlockStatus != null && selectedUnlockStatus.IsUnlocked;
        Sprite previewSprite = LoadMapPreviewSprite(selectedMap);

        if (mapSelectionButton != null)
        {
            mapSelectionButton.interactable = PhotonNetwork.InRoom && RoomSettings.GetSessionState() == RoomSettings.SessionStateInLobby;
            mapSelectionButton.transform.SetAsLastSibling();

            Image image = mapSelectionButton.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = previewSprite;
                image.material = selectedUnlocked ? null : GetGrayscaleUiMaterial();
                image.color = selectedUnlocked ? Color.white : new Color(0.64f, 0.64f, 0.64f, 1f);
            }

            ColorBlock colors = mapSelectionButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.84f, 0.88f, 0.94f, 1f);
            colors.disabledColor = new Color(0.35f, 0.38f, 0.42f, 0.85f);
            colors.colorMultiplier = 1f;
            mapSelectionButton.colors = colors;
        }

        if (mapSelectionText != null)
        {
            string lockBadge = selectedUnlocked ? string.Empty : "\nLOCKED";
            mapSelectionText.text = "MAP\n" + selectedMap.DisplayName + lockBadge;
            mapSelectionText.fontSize = 24f;
            mapSelectionText.characterSpacing = 1.2f;
        }

        for (int i = 0; i < mapSelectionTileButtons.Count && i < LobbyMapCatalog.AllMaps.Count; i++)
        {
            LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
            Button tileButton = mapSelectionTileButtons[i];
            if (tileButton == null)
                continue;

            LobbyMapUnlockStatus unlockStatus = GetLocalMapUnlockStatus(map);
            bool unlocked = unlockStatus != null && unlockStatus.IsUnlocked;
            Image tileImage = tileButton.GetComponent<Image>();
            if (tileImage != null)
            {
                tileImage.sprite = LoadMapPreviewSprite(map);
                tileImage.material = unlocked ? null : GetGrayscaleUiMaterial();
                tileImage.color = unlocked ? Color.white : new Color(0.64f, 0.64f, 0.64f, 1f);
            }

            bool isSelected = selectedMap.Id == map.Id;
            tileButton.interactable = isHost;
            ColorBlock tileColors = tileButton.colors;
            tileColors.normalColor = isSelected
                ? (unlocked ? new Color(0.84f, 1f, 0.9f, 1f) : new Color(0.45f, 0.48f, 0.5f, 1f))
                : (unlocked ? Color.white : new Color(0.58f, 0.6f, 0.62f, 1f));
            tileColors.highlightedColor = unlocked ? new Color(0.92f, 0.96f, 1f, 1f) : new Color(0.68f, 0.7f, 0.72f, 1f);
            tileColors.selectedColor = tileColors.highlightedColor;
            tileColors.pressedColor = new Color(0.84f, 0.88f, 0.94f, 1f);
            tileColors.disabledColor = new Color(0.58f, 0.6f, 0.64f, 0.82f);
            tileColors.colorMultiplier = 1f;
            tileButton.colors = tileColors;

            RefreshMapTileLockVisual(tileButton, map, unlockStatus, false);
        }

        if (mapSelectionOverlayCloseButton != null)
            mapSelectionOverlayCloseButton.interactable = true;
    }

    void OnMapSelectionClicked()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        selectedMapId = RoomSettings.GetSelectedLobbyMapId();
        SwitchLobbyScreen(LobbyScreen.MapSelection);
    }

    void HideMapSelectionOverlay()
    {
        if (mapSelectionOverlayObject != null)
            mapSelectionOverlayObject.SetActive(false);
    }

    void OnMapTileSelected(string mapId)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(mapId);
        if (selectedMap == null)
            return;

        LobbyMapUnlockStatus unlockStatus = GetLocalMapUnlockStatus(selectedMap);
        if (unlockStatus == null || !unlockStatus.IsUnlocked)
        {
            selectedMapId = selectedMap.Id;
            HideMapSelectionOverlay();
            if (currentScreen == LobbyScreen.MapSelection)
                SwitchLobbyScreen(LobbyScreen.MapDetails);
            else
            {
                RefreshFullScreenMapSelectionUi();
                RefreshMapDetailsUi();
            }
            return;
        }

        selectedMapId = selectedMap != null ? selectedMap.Id : selectedMapId;
        Hashtable props = new Hashtable();
        LobbyMapCatalog.ApplyToProperties(selectedMap, props);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        HideMapSelectionOverlay();
        RefreshHostSettingsUi();
        SwitchLobbyScreen(LobbyScreen.MapDetails);
    }

    Sprite LoadMapPreviewSprite(LobbyMapDefinition map)
    {
        if (map != null && string.Equals(map.Id, LobbyMapCatalog.GravityWellMapId, System.StringComparison.Ordinal))
        {
            const string previewPath = "UI/Maps/gravity_well_preview";
            if (mapSpecificPreviewCache.TryGetValue(previewPath, out Sprite cachedSprite) && cachedSprite != null)
                return cachedSprite;

            Sprite sprite = Resources.Load<Sprite>(previewPath);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(previewPath);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }

#if UNITY_EDITOR
            if (sprite == null)
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Maps/gravity_well_preview.png");
#endif

            if (sprite != null)
            {
                mapSpecificPreviewCache[previewPath] = sprite;
                return sprite;
            }
        }

        Sprite parallaxSprite = LoadParallaxBackgroundSprite(LobbyMapCatalog.GetDefaultParallaxBackgroundId(map != null ? map.Id : RoomSettings.DefaultLobbyMapId));
        if (parallaxSprite != null)
            return parallaxSprite;

        return LoadLobbyBackgroundSprite(map != null ? map.MapBackgroundIndex : RoomSettings.DefaultMapBackground);
    }

    Sprite LoadParallaxBackgroundSprite(string backgroundId)
    {
        string normalizedId = RoomSettings.NormalizeParallaxBackgroundId(backgroundId);
        string resourcePath = "Visuals/Backgrounds/" + normalizedId + "_resource";
        if (mapSpecificPreviewCache.TryGetValue(resourcePath, out Sprite cachedSprite) && cachedSprite != null)
            return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

#if UNITY_EDITOR
        if (sprite == null)
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Visuals/Backgrounds/" + normalizedId + "_resource.png");
        }
#endif

        if (sprite == null)
            return null;

        mapSpecificPreviewCache[resourcePath] = sprite;
        return sprite;
    }

    Sprite LoadLobbyBackgroundSprite(int backgroundIndex)
    {
        int clampedIndex = Mathf.Clamp(backgroundIndex, 1, RoomSettings.MaxMapBackground);
        if (mapBackgroundPreviewCache.TryGetValue(clampedIndex, out Sprite cachedSprite) && cachedSprite != null)
            return cachedSprite;

        string resourcePath = "Visuals/Backgrounds/background" + clampedIndex + "_resource";
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

#if UNITY_EDITOR
        if (sprite == null)
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Visuals/Backgrounds/background" + clampedIndex + "_resource.png");
        }
#endif

        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>("Visuals/Backgrounds/background1_resource");
            if (sprite == null)
            {
                Texture2D fallbackTexture = Resources.Load<Texture2D>("Visuals/Backgrounds/background1_resource");
                if (fallbackTexture != null)
                    sprite = Sprite.Create(fallbackTexture, new Rect(0f, 0f, fallbackTexture.width, fallbackTexture.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        if (sprite == null)
            return null;

        mapBackgroundPreviewCache[clampedIndex] = sprite;
        return sprite;
    }

    void PositionBottomActionButton(Button button, float anchoredX, float anchoredY, Vector2 size)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(anchoredX, anchoredY);
        rect.sizeDelta = size;
    }
}
