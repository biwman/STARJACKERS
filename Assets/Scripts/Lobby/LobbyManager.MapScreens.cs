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
    void EnsureLobbyMapSelectionScreen()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (mapSelectionRootObject == null || !mapSelectionRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyMapSelectionScreen");
            if (misplaced != null)
            {
                mapSelectionRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    mapSelectionRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                mapSelectionRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyMapSelectionScreen", typeof(RectTransform), typeof(Image));
            }
            RectTransform rootRect = mapSelectionRootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image rootImage = mapSelectionRootObject.GetComponent<Image>();
            rootImage.color = new Color(0.006f, 0.011f, 0.02f, 0.98f);
            rootImage.raycastTarget = true;

            mapSelectionScreenTitleText = CreateStandaloneLabel(mapSelectionRootObject.transform, "MapSelectionHeader", "SELECT MAP", new Vector2(FullScreenSideMargin, -116f), new Vector2(420f, 34f), 30f, TextAlignmentOptions.Left);

            ConfigureFullScreenMapSelectionScrollContainers();

            fullscreenMapTileButtons.Clear();
            IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
            for (int i = 0; i < maps.Count; i++)
            {
                LobbyMapDefinition map = maps[i];
                GameObject tileObject = new GameObject("FullScreenMapTile_" + map.Id, typeof(RectTransform), typeof(Image), typeof(Button));
                tileObject.transform.SetParent(mapSelectionTilesRootRect, false);
                Image tileImage = tileObject.GetComponent<Image>();
                tileImage.color = new Color(0.1f, 0.16f, 0.24f, 0.96f);
                tileImage.type = Image.Type.Sliced;
                Button tileButton = tileObject.GetComponent<Button>();
                string mapId = map.Id;
                tileButton.onClick.AddListener(() =>
                {
                    OnMapTileSelected(mapId);
                });
                fullscreenMapTileButtons.Add(tileButton);
            }
        }
        else if (fullScreenRoot != null && mapSelectionRootObject.transform.parent != fullScreenRoot.transform)
        {
            mapSelectionRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }

        ConfigureFullScreenMapSelectionScrollContainers();
    }

    void ConfigureFullScreenMapSelectionScrollContainers()
    {
        if (mapSelectionRootObject == null)
            return;

        GameObject viewportObject = FindOrCreateChild(mapSelectionRootObject, "MapSelectionViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        fullScreenMapSelectionViewportRect = viewportObject.GetComponent<RectTransform>();
        if (fullScreenMapSelectionViewportRect != null)
        {
            fullScreenMapSelectionViewportRect.anchorMin = new Vector2(0f, 1f);
            fullScreenMapSelectionViewportRect.anchorMax = new Vector2(0f, 1f);
            fullScreenMapSelectionViewportRect.pivot = new Vector2(0f, 1f);
        }

        Image viewportImage = viewportObject.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = new Color(0.03f, 0.05f, 0.08f, 0.18f);
            viewportImage.raycastTarget = true;
        }

        Transform tilesTransform = viewportObject.transform.Find("MapSelectionTilesRoot");
        if (tilesTransform == null)
        {
            Transform legacyTilesTransform = mapSelectionRootObject.transform.Find("MapSelectionTilesRoot");
            if (legacyTilesTransform != null)
            {
                legacyTilesTransform.SetParent(viewportObject.transform, false);
                tilesTransform = legacyTilesTransform;
            }
        }

        GameObject tilesRoot = tilesTransform != null
            ? tilesTransform.gameObject
            : FindOrCreateChild(viewportObject, "MapSelectionTilesRoot", typeof(RectTransform));
        mapSelectionTilesRootRect = tilesRoot.GetComponent<RectTransform>();
        if (mapSelectionTilesRootRect != null)
        {
            mapSelectionTilesRootRect.anchorMin = new Vector2(0f, 1f);
            mapSelectionTilesRootRect.anchorMax = new Vector2(0f, 1f);
            mapSelectionTilesRootRect.pivot = new Vector2(0f, 1f);
            mapSelectionTilesRootRect.anchoredPosition = Vector2.zero;
        }

        GameObject scrollbarObject = FindOrCreateChild(mapSelectionRootObject, "MapSelectionScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        fullScreenMapSelectionScrollbar = RuntimeScrollbarStyler.ApplyVertical(scrollbarObject, RuntimeScrollbarStyler.Size.Large, RuntimeScrollbarStyler.Tone.Mint);

        fullScreenMapSelectionScrollRect = viewportObject.GetComponent<ScrollRect>();
        if (fullScreenMapSelectionScrollRect != null)
        {
            fullScreenMapSelectionScrollRect.horizontal = false;
            fullScreenMapSelectionScrollRect.vertical = true;
            fullScreenMapSelectionScrollRect.movementType = ScrollRect.MovementType.Clamped;
            fullScreenMapSelectionScrollRect.viewport = fullScreenMapSelectionViewportRect;
            fullScreenMapSelectionScrollRect.content = mapSelectionTilesRootRect;
            fullScreenMapSelectionScrollRect.verticalScrollbar = fullScreenMapSelectionScrollbar;
            fullScreenMapSelectionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            fullScreenMapSelectionScrollRect.scrollSensitivity = 38f;
        }
    }

    void EnsureLobbyMapDetailsScreen()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (mapDetailsRootObject == null || !mapDetailsRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyMapDetailsScreen");
            if (misplaced != null)
            {
                mapDetailsRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    mapDetailsRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                mapDetailsRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyMapDetailsScreen", typeof(RectTransform), typeof(Image));
            }
            RectTransform rootRect = mapDetailsRootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image rootImage = mapDetailsRootObject.GetComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;

            GameObject previewObject = FindOrCreateChild(mapDetailsRootObject, "MapDetailsPreview", typeof(RectTransform), typeof(Image));
            RectTransform previewRect = previewObject.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0f, 1f);
            previewRect.anchorMax = new Vector2(0f, 1f);
            previewRect.pivot = new Vector2(0f, 1f);
            previewRect.anchoredPosition = new Vector2(FullScreenSideMargin, -170f);
            previewRect.sizeDelta = new Vector2(980f, 720f);
            mapDetailsPreviewImage = previewObject.GetComponent<Image>();
            mapDetailsPreviewImage.color = Color.white;
            mapDetailsPreviewImage.raycastTarget = false;

            mapDetailsNameText = CreateStandaloneLabel(mapDetailsRootObject.transform, "MapDetailsName", "MAP", new Vector2(1048f, -170f), new Vector2(420f, 40f), 30f, TextAlignmentOptions.Left);
            mapDetailsDescriptionText = CreateStandaloneLabel(mapDetailsRootObject.transform, "MapDetailsDescription", string.Empty, new Vector2(1048f, -234f), new Vector2(460f, 540f), 27f, TextAlignmentOptions.TopLeft);
            mapDetailsDescriptionText.fontStyle = FontStyles.Normal;
            mapDetailsDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            mapDetailsDescriptionText.lineSpacing = 8f;
        }
        else if (fullScreenRoot != null && mapDetailsRootObject.transform.parent != fullScreenRoot.transform)
        {
            mapDetailsRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }

        EnsureMapDetailsLandingSitesText();
        EnsureMapDetailsLossBadge();
        ConfigureMapDetailsTextStyles();
    }

    void EnsureMapDetailsLandingSitesText()
    {
        if (mapDetailsRootObject == null)
            return;

        Transform existing = mapDetailsRootObject.transform.Find("MapDetailsLandingSites");
        mapDetailsLandingSitesText = existing != null
            ? existing.GetComponent<TMP_Text>()
            : CreateStandaloneLabel(mapDetailsRootObject.transform, "MapDetailsLandingSites", string.Empty, new Vector2(1048f, -620f), new Vector2(460f, 156f), 22f, TextAlignmentOptions.TopLeft);

        if (mapDetailsLandingSitesText == null)
            return;

        mapDetailsLandingSitesText.fontSize = 22f;
        mapDetailsLandingSitesText.fontStyle = FontStyles.Bold;
        mapDetailsLandingSitesText.textWrappingMode = TextWrappingModes.Normal;
        mapDetailsLandingSitesText.lineSpacing = 4f;
        mapDetailsLandingSitesText.color = new Color(0.78f, 0.9f, 1f, 0.98f);
        mapDetailsLandingSitesText.raycastTarget = false;
    }

    void EnsureMapDetailsLossBadge()
    {
        if (mapDetailsRootObject == null)
            return;

        mapDetailsLossBadgeObject = FindOrCreateChild(mapDetailsRootObject, "MapDetailsLossBadge", typeof(RectTransform), typeof(Image));
        mapDetailsLossBadgeImage = mapDetailsLossBadgeObject.GetComponent<Image>();
        if (mapDetailsLossBadgeImage != null)
        {
            mapDetailsLossBadgeImage.raycastTarget = false;
            mapDetailsLossBadgeImage.color = new Color(0.02f, 0.03f, 0.04f, 0.82f);
        }

        Shadow badgeShadow = mapDetailsLossBadgeObject.GetComponent<Shadow>();
        if (badgeShadow == null)
            badgeShadow = mapDetailsLossBadgeObject.AddComponent<Shadow>();
        badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        badgeShadow.effectDistance = new Vector2(3f, -3f);

        GameObject skullImageObject = FindOrCreateChild(mapDetailsLossBadgeObject, "LossSkullImage", typeof(RectTransform), typeof(Image));
        mapDetailsLossSkullImage = skullImageObject.GetComponent<Image>();
        if (mapDetailsLossSkullImage != null)
        {
            RectTransform skullImageRect = mapDetailsLossSkullImage.rectTransform;
            skullImageRect.anchorMin = new Vector2(0.5f, 1f);
            skullImageRect.anchorMax = new Vector2(0.5f, 1f);
            skullImageRect.pivot = new Vector2(0.5f, 1f);
            skullImageRect.anchoredPosition = new Vector2(0f, -10f);
            skullImageRect.sizeDelta = new Vector2(MapDeathLossBadgeIconWidth, MapDeathLossBadgeIconHeight);
            mapDetailsLossSkullImage.raycastTarget = false;
            mapDetailsLossSkullImage.preserveAspect = true;
            mapDetailsLossSkullImage.sprite = LoadMapDeathSkullBadgeSprite();
        }

        Transform skullTransform = mapDetailsLossBadgeObject.transform.Find("LossSkull");
        mapDetailsLossSkullText = skullTransform != null
            ? skullTransform.GetComponent<TMP_Text>()
            : CreateStandaloneLabel(mapDetailsLossBadgeObject.transform, "LossSkull", "\u2620", new Vector2(0f, -12f), new Vector2(MapDeathLossBadgeWidth, 152f), 140f, TextAlignmentOptions.Center);

        Transform labelTransform = mapDetailsLossBadgeObject.transform.Find("LossLabel");
        mapDetailsLossLabelText = labelTransform != null
            ? labelTransform.GetComponent<TMP_Text>()
            : CreateStandaloneLabel(mapDetailsLossBadgeObject.transform, "LossLabel", string.Empty, new Vector2(0f, -180f), new Vector2(MapDeathLossBadgeWidth, 42f), 24f, TextAlignmentOptions.Center);

        if (mapDetailsLossSkullText != null)
        {
            RectTransform skullTextRect = mapDetailsLossSkullText.rectTransform;
            skullTextRect.anchoredPosition = new Vector2(0f, -12f);
            skullTextRect.sizeDelta = new Vector2(MapDeathLossBadgeWidth, 152f);
            mapDetailsLossSkullText.text = "\u2620";
            mapDetailsLossSkullText.fontSize = 140f;
            mapDetailsLossSkullText.fontStyle = FontStyles.Bold;
            mapDetailsLossSkullText.textWrappingMode = TextWrappingModes.NoWrap;
            mapDetailsLossSkullText.raycastTarget = false;
            mapDetailsLossSkullText.gameObject.SetActive(mapDetailsLossSkullImage == null || mapDetailsLossSkullImage.sprite == null);
        }

        if (mapDetailsLossLabelText != null)
        {
            RectTransform labelRect = mapDetailsLossLabelText.rectTransform;
            labelRect.anchoredPosition = new Vector2(0f, -180f);
            labelRect.sizeDelta = new Vector2(MapDeathLossBadgeWidth, 42f);
            mapDetailsLossLabelText.fontSize = 24f;
            mapDetailsLossLabelText.fontStyle = FontStyles.Bold;
            mapDetailsLossLabelText.textWrappingMode = TextWrappingModes.NoWrap;
            mapDetailsLossLabelText.raycastTarget = false;
        }
    }

    void ConfigureMapDetailsTextStyles()
    {
        if (mapDetailsDescriptionText != null)
        {
            mapDetailsDescriptionText.fontSize = 27f;
            mapDetailsDescriptionText.fontStyle = FontStyles.Normal;
            mapDetailsDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            mapDetailsDescriptionText.lineSpacing = 8f;
        }

        if (mapDetailsLandingSitesText != null)
        {
            mapDetailsLandingSitesText.fontSize = 22f;
            mapDetailsLandingSitesText.fontStyle = FontStyles.Bold;
            mapDetailsLandingSitesText.textWrappingMode = TextWrappingModes.Normal;
            mapDetailsLandingSitesText.lineSpacing = 4f;
        }
    }

    bool IsMapUnlockedForLocalPlayer(LobbyMapDefinition map)
    {
        LobbyMapUnlockStatus status = GetLocalMapUnlockStatus(map);
        return status != null && status.IsUnlocked;
    }

    Material GetGrayscaleUiMaterial()
    {
        if (grayscaleUiMaterial != null)
            return grayscaleUiMaterial;

        Shader shader = Resources.Load<Shader>("UI/GrayscaleUI");
        if (shader == null)
            shader = Shader.Find("UI/Grayscale");

        if (shader == null)
            return null;

        grayscaleUiMaterial = new Material(shader)
        {
            name = "Runtime UI Grayscale Material"
        };
        return grayscaleUiMaterial;
    }

    void RefreshFullScreenMapSelectionUi()
    {
        EnsureFullScreenLobbyUiExists();
        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(selectedMapId) ?? LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId()) ?? LobbyMapCatalog.GetDefault();
        bool isHost = PhotonNetwork.IsMasterClient;

        for (int i = 0; i < fullscreenMapTileButtons.Count && i < LobbyMapCatalog.AllMaps.Count; i++)
        {
            LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
            Button tileButton = fullscreenMapTileButtons[i];
            EnsureFullScreenMapTileVisual(tileButton, map);
            LobbyMapUnlockStatus unlockStatus = GetLocalMapUnlockStatus(map);
            bool unlocked = unlockStatus != null && unlockStatus.IsUnlocked;

            Image tileImage = tileButton.GetComponent<Image>();
            Image previewImage = tileButton.transform.Find("TilePreviewImage")?.GetComponent<Image>();
            if (previewImage != null)
            {
                previewImage.sprite = LoadMapPreviewSprite(map);
                previewImage.material = unlocked ? null : GetGrayscaleUiMaterial();
                previewImage.color = unlocked ? Color.white : new Color(0.64f, 0.64f, 0.64f, 1f);
            }

            bool isSelected = selectedMap != null && selectedMap.Id == map.Id;
            tileButton.interactable = isHost;
            ColorBlock tileColors = tileButton.colors;
            tileColors.normalColor = isSelected
                ? (unlocked ? new Color(0.3f, 0.78f, 0.98f, 1f) : new Color(0.34f, 0.42f, 0.48f, 1f))
                : (unlocked ? new Color(0.1f, 0.16f, 0.24f, 0.96f) : new Color(0.08f, 0.09f, 0.11f, 0.96f));
            tileColors.highlightedColor = isSelected
                ? (unlocked ? new Color(0.38f, 0.86f, 1f, 1f) : new Color(0.42f, 0.5f, 0.56f, 1f))
                : (unlocked ? new Color(0.16f, 0.24f, 0.36f, 1f) : new Color(0.14f, 0.16f, 0.18f, 1f));
            tileColors.selectedColor = tileColors.highlightedColor;
            tileColors.pressedColor = new Color(0.08f, 0.12f, 0.18f, 1f);
            tileColors.disabledColor = new Color(0.16f, 0.18f, 0.22f, 0.72f);
            tileColors.colorMultiplier = 1f;
            tileButton.colors = tileColors;

            if (tileImage != null)
                tileImage.color = tileColors.normalColor;

            RefreshMapTileLockVisual(tileButton, map, unlockStatus, true);
        }

        if (mapSelectionScreenTitleText != null)
            mapSelectionScreenTitleText.text = "MAP SELECTION";
    }

    void EnsureFullScreenMapTileVisual(Button tileButton, LobbyMapDefinition map)
    {
        if (tileButton == null)
            return;

        Image rootImage = tileButton.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.raycastTarget = true;
            rootImage.sprite = null;
            rootImage.type = Image.Type.Sliced;
        }

        GameObject previewObject = FindOrCreateChild(tileButton.gameObject, "TilePreviewImage", typeof(RectTransform), typeof(Image));
        Image previewImage = previewObject.GetComponent<Image>();
        RectTransform previewRect = previewImage.rectTransform;
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.offsetMin = new Vector2(4f, 4f);
        previewRect.offsetMax = new Vector2(-4f, -4f);
        previewImage.type = Image.Type.Simple;
        previewImage.preserveAspect = false;
        previewImage.raycastTarget = false;
        previewImage.color = Color.white;

        GameObject labelBackdropObject = FindOrCreateChild(tileButton.gameObject, "TileLabelBackdrop", typeof(RectTransform), typeof(Image));
        Image labelBackdrop = labelBackdropObject.GetComponent<Image>();
        RectTransform labelBackdropRect = labelBackdrop.rectTransform;
        labelBackdropRect.anchorMin = new Vector2(0f, 1f);
        labelBackdropRect.anchorMax = new Vector2(1f, 1f);
        labelBackdropRect.pivot = new Vector2(0.5f, 1f);
        labelBackdropRect.anchoredPosition = Vector2.zero;
        labelBackdropRect.sizeDelta = new Vector2(0f, 58f);
        labelBackdrop.color = new Color(0.02f, 0.04f, 0.08f, 0.7f);
        labelBackdrop.raycastTarget = false;

        Transform existingTitle = tileButton.transform.Find("TileTitle");
        TMP_Text tileTitle = existingTitle != null
            ? existingTitle.GetComponent<TMP_Text>()
            : CreateStandaloneLabel(tileButton.transform, "TileTitle", map.DisplayName, new Vector2(0f, -14f), new Vector2(360f, 30f), 26f, TextAlignmentOptions.Center);
        if (tileTitle != null)
        {
            tileTitle.text = map.DisplayName;
            RectTransform titleRect = tileTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -14f);
            titleRect.sizeDelta = new Vector2(420f, 30f);
            tileTitle.fontSize = 26f;
            tileTitle.alignment = TextAlignmentOptions.Center;
        }

        Transform existingSubtitle = tileButton.transform.Find("TileSubtitle");
        if (existingSubtitle != null)
            existingSubtitle.gameObject.SetActive(false);
    }

    void RefreshMapTileLockVisual(Button tileButton, LobbyMapDefinition map, LobbyMapUnlockStatus unlockStatus, bool fullScreenTile)
    {
        if (tileButton == null || map == null)
            return;

        bool unlocked = unlockStatus != null && unlockStatus.IsUnlocked;
        Transform existingTitle = tileButton.transform.Find("TileTitle");
        TMP_Text tileTitle = existingTitle != null ? existingTitle.GetComponent<TMP_Text>() : null;
        if (tileTitle != null)
            tileTitle.color = unlocked ? Color.white : new Color(0.82f, 0.84f, 0.86f, 1f);

        GameObject overlayObject = FindOrCreateChild(tileButton.gameObject, "TileLockOverlay", typeof(RectTransform), typeof(Image));
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = new Vector2(4f, 4f);
        overlayRect.offsetMax = new Vector2(-4f, -4f);
        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.012f, 0.016f, 0.56f);
        overlayImage.raycastTarget = false;
        overlayObject.SetActive(!unlocked);

        if (unlocked)
            return;

        overlayObject.transform.SetAsLastSibling();

        RectTransform tileRect = tileButton.GetComponent<RectTransform>();
        float tileWidth = tileRect != null && tileRect.rect.width > 0f ? tileRect.rect.width : (fullScreenTile ? MapTileWidth : 540f);
        float tileHeight = tileRect != null && tileRect.rect.height > 0f ? tileRect.rect.height : 250f;
        float requirementFontSize = fullScreenTile ? MapUnlockRequirementFullScreenFontSize : MapUnlockRequirementOverlayFontSize;
        float requirementHorizontalPadding = fullScreenTile ? 42f : 34f;
        float requirementWidth = Mathf.Max(280f, tileWidth - requirementHorizontalPadding * 2f);
        float requirementHeight = Mathf.Clamp(tileHeight * 0.38f, MapUnlockRequirementMinHeight, fullScreenTile ? 98f : 104f);
        float lockTextHeight = fullScreenTile ? 36f : 34f;
        float lockTopOffset = Mathf.Clamp(
            tileHeight - MapUnlockRequirementBottomOffset - requirementHeight - lockTextHeight - 18f,
            44f,
            fullScreenTile ? 86f : 82f);
        float lockTextWidth = Mathf.Max(280f, Mathf.Min(tileWidth - 36f, fullScreenTile ? 420f : 360f));

        TMP_Text lockText = overlayObject.transform.Find("TileLockText")?.GetComponent<TMP_Text>();
        if (lockText == null)
            lockText = CreateStandaloneLabel(overlayObject.transform, "TileLockText", "LOCKED", new Vector2(0f, -lockTopOffset), new Vector2(lockTextWidth, lockTextHeight), fullScreenTile ? 27f : 24f, TextAlignmentOptions.Center);
        RectTransform lockRect = lockText.rectTransform;
        lockRect.anchorMin = new Vector2(0.5f, 1f);
        lockRect.anchorMax = new Vector2(0.5f, 1f);
        lockRect.pivot = new Vector2(0.5f, 1f);
        lockRect.anchoredPosition = new Vector2(0f, -lockTopOffset);
        lockRect.sizeDelta = new Vector2(lockTextWidth, lockTextHeight);
        lockText.text = "LOCKED";
        lockText.fontSize = fullScreenTile ? 27f : 24f;
        lockText.fontStyle = FontStyles.Bold;
        lockText.characterSpacing = 2.2f;
        lockText.color = new Color(0.96f, 0.98f, 1f, 1f);
        lockText.alignment = TextAlignmentOptions.Center;

        TMP_Text requirementText = overlayObject.transform.Find("TileRequirementText")?.GetComponent<TMP_Text>();
        if (requirementText == null)
            requirementText = CreateStandaloneLabel(overlayObject.transform, "TileRequirementText", string.Empty, new Vector2(0f, MapUnlockRequirementBottomOffset), new Vector2(requirementWidth, requirementHeight), requirementFontSize, TextAlignmentOptions.Center);
        RectTransform requirementRect = requirementText.rectTransform;
        requirementRect.anchorMin = new Vector2(0.5f, 0f);
        requirementRect.anchorMax = new Vector2(0.5f, 0f);
        requirementRect.pivot = new Vector2(0.5f, 0f);
        requirementRect.anchoredPosition = new Vector2(0f, MapUnlockRequirementBottomOffset);
        requirementRect.sizeDelta = new Vector2(requirementWidth, requirementHeight);
        requirementText.text = unlockStatus != null ? unlockStatus.RequirementText : "Unlock requirement unavailable.";
        requirementText.fontSize = requirementFontSize;
        requirementText.fontStyle = FontStyles.Normal;
        requirementText.enableAutoSizing = false;
        requirementText.textWrappingMode = TextWrappingModes.Normal;
        requirementText.alignment = TextAlignmentOptions.Center;
        requirementText.color = new Color(0.9f, 0.94f, 0.98f, 0.98f);
    }

    void RefreshMapDetailsUi()
    {
        EnsureFullScreenLobbyUiExists();
        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(selectedMapId) ?? LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId()) ?? LobbyMapCatalog.GetDefault();
        if (selectedMap == null)
            return;

        LobbyMapUnlockStatus unlockStatus = GetLocalMapUnlockStatus(selectedMap);
        bool unlocked = unlockStatus != null && unlockStatus.IsUnlocked;

        if (mapDetailsPreviewImage != null)
        {
            mapDetailsPreviewImage.sprite = LoadMapPreviewSprite(selectedMap);
            mapDetailsPreviewImage.material = unlocked ? null : GetGrayscaleUiMaterial();
            mapDetailsPreviewImage.color = unlocked ? Color.white : new Color(0.64f, 0.64f, 0.64f, 1f);
        }

        if (mapDetailsNameText != null)
            mapDetailsNameText.text = unlocked ? selectedMap.DisplayName : selectedMap.DisplayName + "  LOCKED";
        if (mapDetailsDescriptionText != null)
        {
            string unlockText = !unlocked && unlockStatus != null && !string.IsNullOrWhiteSpace(unlockStatus.RequirementText)
                ? unlockStatus.RequirementText + "\n\n"
                : string.Empty;
            mapDetailsDescriptionText.text = unlockText + selectedMap.Description;
        }
        if (mapDetailsLandingSitesText != null)
        {
            string landingSitesText = BuildMapLandingSitesText(selectedMap);
            mapDetailsLandingSitesText.text = landingSitesText;
            mapDetailsLandingSitesText.gameObject.SetActive(!string.IsNullOrWhiteSpace(landingSitesText));
        }
        RefreshMapDeathLossBadge(selectedMap);

        if (launchButton != null)
        {
            LobbyMapDefinition activeRoomMap = LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId());
            launchButton.interactable = PhotonNetwork.IsMasterClient &&
                                        PhotonNetwork.InRoom &&
                                        RoomSettings.GetSessionState() == RoomSettings.SessionStateInLobby &&
                                        unlocked &&
                                        IsMapUnlockedForLocalPlayer(activeRoomMap);
        }
    }

    string BuildMapLandingSitesText(LobbyMapDefinition selectedMap)
    {
        IReadOnlyList<LobbyMapLandingSiteSummary> landingSites = ResolveMapLandingSites(selectedMap);
        if (landingSites == null || landingSites.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder("LANDING SITES");
        for (int i = 0; i < landingSites.Count; i++)
        {
            LobbyMapLandingSiteSummary site = landingSites[i];
            if (site == null || site.Count <= 0 || string.IsNullOrWhiteSpace(site.Label))
                continue;

            builder.Append('\n');
            builder.Append(site.Label);
            builder.Append(": ");
            builder.Append(site.Count);
        }

        return builder.Length > "LANDING SITES".Length ? builder.ToString() : string.Empty;
    }

    IReadOnlyList<LobbyMapLandingSiteSummary> ResolveMapLandingSites(LobbyMapDefinition selectedMap)
    {
        if (selectedMap == null)
            return System.Array.Empty<LobbyMapLandingSiteSummary>();

        bool selectedRoomMap = PhotonNetwork.CurrentRoom != null &&
                               string.Equals(RoomSettings.GetSelectedLobbyMapId(), selectedMap.Id, System.StringComparison.Ordinal);
        if (!selectedRoomMap)
            return LobbyMapCatalog.GetDefaultLandingSites(selectedMap);

        return LobbyMapCatalog.BuildLandingSites(
            LobbyMapCatalog.GetExtractionLandingSiteLabel(RoomSettings.GetExtractionType()),
            RoomSettings.GetExtractionCount(),
            RoomSettings.GetRepairBayCount(),
            RoomSettings.GetScienceStationCount(),
            RoomSettings.GetSpaceFactoryCount());
    }

    void RefreshMapDeathLossBadge(LobbyMapDefinition selectedMap)
    {
        if (selectedMap == null)
            return;

        EnsureMapDetailsLossBadge();

        bool inventoryLoss = selectedMap.InventoryLossEnabled;
        bool equipmentLoss = selectedMap.EquipmentLossEnabled;
        if (PhotonNetwork.CurrentRoom != null && string.Equals(RoomSettings.GetSelectedLobbyMapId(), selectedMap.Id, System.StringComparison.Ordinal))
        {
            inventoryLoss = RoomSettings.IsInventoryLossEnabled();
            equipmentLoss = RoomSettings.IsEquipmentLossEnabled();
        }

        Color skullColor;
        string label;
        if (equipmentLoss)
        {
            skullColor = new Color(1f, 0.18f, 0.12f, 1f);
            label = inventoryLoss ? "FULL LOSS" : "EQUIP LOSS";
        }
        else if (inventoryLoss)
        {
            skullColor = new Color(1f, 0.82f, 0.12f, 1f);
            label = "INV LOSS";
        }
        else
        {
            skullColor = new Color(0.24f, 1f, 0.46f, 1f);
            label = "NO LOSS";
        }

        if (mapDetailsLossSkullText != null)
        {
            mapDetailsLossSkullText.text = "\u2620";
            mapDetailsLossSkullText.color = skullColor;
            mapDetailsLossSkullText.gameObject.SetActive(mapDetailsLossSkullImage == null || mapDetailsLossSkullImage.sprite == null);
        }

        if (mapDetailsLossSkullImage != null)
        {
            if (mapDetailsLossSkullImage.sprite == null)
                mapDetailsLossSkullImage.sprite = LoadMapDeathSkullBadgeSprite();

            mapDetailsLossSkullImage.color = skullColor;
            mapDetailsLossSkullImage.gameObject.SetActive(mapDetailsLossSkullImage.sprite != null);
        }

        if (mapDetailsLossLabelText != null)
        {
            mapDetailsLossLabelText.text = label;
            mapDetailsLossLabelText.color = skullColor;
        }

        if (mapDetailsLossBadgeImage != null)
            mapDetailsLossBadgeImage.color = new Color(0.02f, 0.03f, 0.04f, 0.86f);

        if (mapDetailsLossBadgeObject != null)
            mapDetailsLossBadgeObject.SetActive(true);
    }

    Sprite LoadMapDeathSkullBadgeSprite()
    {
        if (mapDeathSkullBadgeSprite != null)
            return mapDeathSkullBadgeSprite;

        mapDeathSkullBadgeSprite = Resources.Load<Sprite>("UI/map_death_skull_badge");
        if (mapDeathSkullBadgeSprite != null)
            return mapDeathSkullBadgeSprite;

        Texture2D texture = Resources.Load<Texture2D>("UI/map_death_skull_badge");
        if (texture == null)
            return null;

        mapDeathSkullBadgeSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return mapDeathSkullBadgeSprite;
    }
}
