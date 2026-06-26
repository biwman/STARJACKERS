using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class PlayerProfilePanelUI
{
    void CreateShipSelectionView(Transform parent)
    {
        shipSelectionViewObject = new GameObject("ShipSelectionView", typeof(RectTransform), typeof(Image));
        shipSelectionViewObject.transform.SetParent(parent, false);

        RectTransform rootRect = shipSelectionViewObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = shipSelectionViewObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.05f, 0.08f, 0.96f);

        ProfileShipSelectionSwipeHandler swipeHandler = shipSelectionViewObject.AddComponent<ProfileShipSelectionSwipeHandler>();
        swipeHandler.owner = this;

        shipSelectionBackButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionBackButton", "BACK", new Vector2(-806f, -46f), new Vector2(214f, 62f), () =>
        {
            SwitchToScreen(ProfileScreen.Home);
        });
        StyleButton(shipSelectionBackButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));

        shipSelectionTitleText = CreateText(shipSelectionViewObject.transform, "ShipSelectionTitle", "CHOOSE SHIP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(640f, 48f), 38f, TextAlignmentOptions.Center);
        shipSelectionTitleText.raycastTarget = false;
        shipSelectionSubtitleText = CreateText(shipSelectionViewObject.transform, "ShipSelectionSubtitle", "Swipe with arrows, pick a skin, then tap the centered ship.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(760f, 28f), 17f, TextAlignmentOptions.Center);
        shipSelectionSubtitleText.fontStyle = FontStyles.Normal;
        shipSelectionSubtitleText.color = new Color(0.8f, 0.87f, 0.95f, 0.92f);
        shipSelectionSkinLabelText = CreateText(shipSelectionViewObject.transform, "ShipSelectionSkinLabel", "SKINS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(420f, 24f), 20f, TextAlignmentOptions.Center);

        shipSelectionSkinButtons = new Button[3];
        for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
        {
            int capturedIndex = i;
            shipSelectionSkinButtons[i] = CreateButton(shipSelectionViewObject.transform, "ShipSelectionSkinButton" + i, "SKIN", new Vector2(-248f + (248f * i), -56f), new Vector2(220f, 48f), () =>
            {
                SetShipSelectionSkinByButton(capturedIndex);
            });
            StyleButton(shipSelectionSkinButtons[i], new Color(0.16f, 0.2f, 0.27f, 0.95f), new Color(0.19f, 0.61f, 0.5f, 0.98f));
        }

        shipSelectionPrevButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionPrevButton", "<", new Vector2(-666f, -438f), new Vector2(92f, 92f), MoveShipSelectionLeft);
        shipSelectionNextButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionNextButton", ">", new Vector2(666f, -438f), new Vector2(92f, 92f), MoveShipSelectionRight);
        StyleButton(shipSelectionPrevButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(shipSelectionNextButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));

        shipSelectionCardObjects = new GameObject[3];
        shipSelectionCardImages = new Image[3];
        shipSelectionCardTitles = new TMP_Text[3];
        shipSelectionCardLockTexts = new TMP_Text[3];
        shipSelectionCardDonateButtons = new Button[3];
        shipSelectionCardStatLabelTexts = new TMP_Text[3][];
        shipSelectionCardStatValueTexts = new TMP_Text[3][];
        shipSelectionCardStatFillImages = new Image[3][];
        shipSelectionCardSlotObjects = new GameObject[3][];
        Vector2[] positions =
        {
            new Vector2(-560f, -108f),
            new Vector2(0f, -86f),
            new Vector2(560f, -108f)
        };
        Vector2[] sizes =
        {
            new Vector2(500f, 860f),
            new Vector2(700f, 960f),
            new Vector2(500f, 860f)
        };

        for (int i = 0; i < shipSelectionCardObjects.Length; i++)
        {
            bool centerCard = i == 1;
            shipSelectionCardObjects[i] = CreateShipSelectionCard(
                shipSelectionViewObject.transform,
                i,
                positions[i],
                sizes[i],
                centerCard,
                out shipSelectionCardImages[i],
                out shipSelectionCardTitles[i],
                out shipSelectionCardLockTexts[i],
                out shipSelectionCardDonateButtons[i],
                out shipSelectionCardStatLabelTexts[i],
                out shipSelectionCardStatValueTexts[i],
                out shipSelectionCardStatFillImages[i],
                out shipSelectionCardSlotObjects[i]);
        }

        shipSelectionStatusText = CreateText(shipSelectionViewObject.transform, "ShipSelectionStatus", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(720f, 28f), 18f, TextAlignmentOptions.Center);
        shipSelectionStatusText.fontStyle = FontStyles.Normal;
        shipSelectionStatusText.color = new Color(0.86f, 0.92f, 0.98f, 0.98f);

        shipSelectionDetailsButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionMissionDetailsButton", "MISSION DETAILS", new Vector2(0f, -984f), new Vector2(330f, 58f), ShowShipSelectionMissionDetails);
        StyleButton(shipSelectionDetailsButton, new Color(0.13f, 0.2f, 0.3f, 0.98f), new Color(0.22f, 0.34f, 0.48f, 1f));
        TMP_Text detailsButtonText = shipSelectionDetailsButton.GetComponentInChildren<TMP_Text>(true);
        if (detailsButtonText != null)
        {
            detailsButtonText.fontSize = 20f;
            detailsButtonText.characterSpacing = 1.2f;
            detailsButtonText.margin = new Vector4(12f, 4f, 12f, 4f);
        }

        CreateShipMissionDetailsPanel(shipSelectionViewObject.transform);

        if (shipSelectionSubtitleText != null)
            shipSelectionSubtitleText.gameObject.SetActive(false);
        if (shipSelectionSkinLabelText != null)
            shipSelectionSkinLabelText.gameObject.SetActive(false);
        if (shipSelectionPrevButton != null)
            shipSelectionPrevButton.gameObject.SetActive(false);
        if (shipSelectionNextButton != null)
            shipSelectionNextButton.gameObject.SetActive(false);

        shipSelectionViewObject.SetActive(false);
    }

    void CreateShipMissionDetailsPanel(Transform parent)
    {
        shipMissionDetailsPanelObject = new GameObject("ShipMissionDetailsPanel", typeof(RectTransform), typeof(Image), typeof(Shadow));
        shipMissionDetailsPanelObject.transform.SetParent(parent, false);

        RectTransform panelRect = shipMissionDetailsPanelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -12f);
        panelRect.sizeDelta = new Vector2(900f, 620f);

        Image panelImage = shipMissionDetailsPanelObject.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.06f, 0.09f, 0.98f);
        panelImage.raycastTarget = true;

        Shadow shadow = shipMissionDetailsPanelObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
        shadow.effectDistance = new Vector2(8f, -8f);
        shadow.useGraphicAlpha = true;

        shipMissionDetailsTitleText = CreateText(shipMissionDetailsPanelObject.transform, "ShipMissionDetailsTitle", "MISSION DETAILS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(720f, 46f), 34f, TextAlignmentOptions.Center);
        shipMissionDetailsTitleText.characterSpacing = 1.5f;
        shipMissionDetailsTitleText.raycastTarget = false;

        GameObject viewportObject = new GameObject("ShipMissionDetailsViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(shipMissionDetailsPanelObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.offsetMin = new Vector2(42f, 94f);
        viewportRect.offsetMax = new Vector2(-42f, -100f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("ShipMissionDetailsContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        shipMissionDetailsContentRect = contentObject.GetComponent<RectTransform>();
        shipMissionDetailsContentRect.anchorMin = new Vector2(0f, 1f);
        shipMissionDetailsContentRect.anchorMax = new Vector2(1f, 1f);
        shipMissionDetailsContentRect.pivot = new Vector2(0.5f, 1f);
        shipMissionDetailsContentRect.anchoredPosition = Vector2.zero;
        shipMissionDetailsContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = viewportObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 32f;
        scrollRect.viewport = viewportRect;
        scrollRect.content = shipMissionDetailsContentRect;

        shipMissionDetailsBodyText = CreateText(contentObject.transform, "ShipMissionDetailsBody", string.Empty, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.TopLeft);
        shipMissionDetailsBodyText.fontStyle = FontStyles.Normal;
        shipMissionDetailsBodyText.textWrappingMode = TextWrappingModes.Normal;
        shipMissionDetailsBodyText.overflowMode = TextOverflowModes.Overflow;
        shipMissionDetailsBodyText.lineSpacing = 8f;
        shipMissionDetailsBodyText.margin = Vector4.zero;
        shipMissionDetailsBodyText.raycastTarget = false;

        shipMissionDetailsTextLayoutElement = shipMissionDetailsBodyText.gameObject.AddComponent<LayoutElement>();
        shipMissionDetailsTextLayoutElement.flexibleWidth = 1f;
        shipMissionDetailsTextLayoutElement.minHeight = 1f;
        shipMissionDetailsTextLayoutElement.preferredHeight = 430f;

        shipMissionDetailsCloseButton = CreateButton(shipMissionDetailsPanelObject.transform, "ShipMissionDetailsCloseButton", "CLOSE", new Vector2(0f, -548f), new Vector2(220f, 54f), HideShipSelectionMissionDetails);
        StyleButton(shipMissionDetailsCloseButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));

        shipMissionDetailsPanelObject.SetActive(false);
    }

    void ShowShipSelectionMissionDetails()
    {
        if (!TryBuildShipSelectionMissionDetails(shipSelectionCenterType, out string title, out string body))
            return;

        UpdateShipSelectionMissionDetailsContent(title, body);

        if (shipMissionDetailsPanelObject != null)
        {
            shipMissionDetailsPanelObject.SetActive(true);
            shipMissionDetailsPanelObject.transform.SetAsLastSibling();
        }
    }

    void HideShipSelectionMissionDetails()
    {
        if (shipMissionDetailsPanelObject != null)
            shipMissionDetailsPanelObject.SetActive(false);
    }

    void RefreshShipSelectionMissionDetailsControls()
    {
        bool hasDetails = TryBuildShipSelectionMissionDetails(shipSelectionCenterType, out string title, out string body);

        if (shipSelectionDetailsButton != null)
        {
            shipSelectionDetailsButton.gameObject.SetActive(hasDetails);
            shipSelectionDetailsButton.interactable = hasDetails && inventoryControlsInteractable && !inventoryActionInProgress;
        }

        if (!hasDetails)
        {
            HideShipSelectionMissionDetails();
            return;
        }

        if (shipMissionDetailsPanelObject != null && shipMissionDetailsPanelObject.activeSelf)
            UpdateShipSelectionMissionDetailsContent(title, body);
    }

    bool TryBuildShipSelectionMissionDetails(ShipType shipType, out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;

        bool shipUnlocked = IsShipTypeUnlockedForUi(shipType);
        ViperRecoveryStage viperStage = shipType == ShipType.Viper && PlayerProfileService.HasInstance
            ? PlayerProfileService.Instance.GetViperRecoveryStage()
            : ViperRecoveryStage.Complete;
        bool hasActiveMission = !shipUnlocked || (shipType == ShipType.Viper && viperStage == ViperRecoveryStage.Testing);
        if (!hasActiveMission)
            return false;

        string details = BuildShipSelectionProgressText(shipType, shipUnlocked, viperStage);
        if (string.IsNullOrWhiteSpace(details))
            return false;

        title = ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant() + " MISSION";
        body = details;
        return true;
    }

    void UpdateShipSelectionMissionDetailsContent(string title, string body)
    {
        if (shipMissionDetailsTitleText != null)
            shipMissionDetailsTitleText.text = title;

        if (shipMissionDetailsBodyText != null)
        {
            shipMissionDetailsBodyText.text = body;
            UpdateShipSelectionMissionDetailsBodyHeight(body);
        }
    }

    void UpdateShipSelectionMissionDetailsBodyHeight(string body)
    {
        if (shipMissionDetailsBodyText == null || shipMissionDetailsTextLayoutElement == null)
            return;

        float textWidth = 780f;
        if (shipMissionDetailsContentRect != null && shipMissionDetailsContentRect.rect.width > 1f)
            textWidth = Mathf.Max(1f, shipMissionDetailsContentRect.rect.width - 8f);

        float preferredHeight = shipMissionDetailsBodyText.GetPreferredValues(body ?? string.Empty, textWidth, Mathf.Infinity).y;
        shipMissionDetailsTextLayoutElement.preferredHeight = Mathf.Max(430f, preferredHeight + 8f);

        if (shipMissionDetailsContentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(shipMissionDetailsContentRect);
    }

    void CreatePilotSelectionView(Transform parent)
    {
        pilotSelectionViewObject = new GameObject("PilotSelectionView", typeof(RectTransform), typeof(Image));
        pilotSelectionViewObject.transform.SetParent(parent, false);

        RectTransform rootRect = pilotSelectionViewObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = pilotSelectionViewObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.05f, 0.08f, 0.96f);

        ProfilePilotSelectionSwipeHandler swipeHandler = pilotSelectionViewObject.AddComponent<ProfilePilotSelectionSwipeHandler>();
        swipeHandler.owner = this;

        pilotSelectionBackButton = CreateButton(pilotSelectionViewObject.transform, "PilotSelectionBackButton", "BACK", new Vector2(-116f, -106f), new Vector2(216f, 62f), () =>
        {
            SwitchToScreen(ProfileScreen.Home);
        });
        StyleButton(pilotSelectionBackButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));
        pilotSelectionBackButton.gameObject.SetActive(false);

        pilotSelectionTitleText = CreateText(pilotSelectionViewObject.transform, "PilotSelectionTitle", "JAKE", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(720f, 56f), 42f, TextAlignmentOptions.Center);
        pilotSelectionTitleText.raycastTarget = false;

        pilotSelectionPrevButton = CreateButton(pilotSelectionViewObject.transform, "PilotSelectionPrevButton", "<", new Vector2(-666f, -438f), new Vector2(92f, 92f), MovePilotSelectionLeft);
        pilotSelectionNextButton = CreateButton(pilotSelectionViewObject.transform, "PilotSelectionNextButton", ">", new Vector2(666f, -438f), new Vector2(92f, 92f), MovePilotSelectionRight);
        StyleButton(pilotSelectionPrevButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(pilotSelectionNextButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));

        pilotSelectionCardObjects = new GameObject[3];
        pilotSelectionCardImages = new Image[3];
        pilotSelectionCardNames = new TMP_Text[3];
        pilotSelectionCardLockTexts = new TMP_Text[3];
        Vector2[] positions =
        {
            new Vector2(-560f, -190f),
            new Vector2(0f, -162f),
            new Vector2(560f, -190f)
        };
        Vector2[] sizes =
        {
            new Vector2(440f, 540f),
            new Vector2(560f, 620f),
            new Vector2(440f, 540f)
        };

        for (int i = 0; i < pilotSelectionCardObjects.Length; i++)
        {
            pilotSelectionCardObjects[i] = CreatePilotSelectionCard(
                pilotSelectionViewObject.transform,
                i,
                positions[i],
                sizes[i],
                i == 1,
                out pilotSelectionCardImages[i],
                out pilotSelectionCardNames[i],
                out pilotSelectionCardLockTexts[i]);
        }

        pilotSelectionAbilitiesText = CreateText(pilotSelectionViewObject.transform, "PilotSelectionAbilities", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(1380f, 250f), 25f, TextAlignmentOptions.Center);
        pilotSelectionAbilitiesText.fontStyle = FontStyles.Normal;
        pilotSelectionAbilitiesText.textWrappingMode = TextWrappingModes.Normal;
        pilotSelectionAbilitiesText.color = new Color(0.86f, 0.92f, 0.98f, 0.98f);
        pilotSelectionAbilitiesText.raycastTarget = false;

        pilotSelectionStatusText = CreateText(pilotSelectionViewObject.transform, "PilotSelectionStatus", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(820f, 32f), 18f, TextAlignmentOptions.Center);
        pilotSelectionStatusText.fontStyle = FontStyles.Normal;
        pilotSelectionStatusText.color = new Color(0.98f, 0.82f, 0.5f, 0.98f);
        pilotSelectionStatusText.gameObject.SetActive(false);

        pilotSelectionViewObject.SetActive(false);
    }

    GameObject CreatePilotSelectionCard(Transform parent, int cardIndex, Vector2 anchoredPosition, Vector2 size, bool centerCard, out Image previewImage, out TMP_Text nameText, out TMP_Text lockText)
    {
        GameObject card = new GameObject("PilotSelectionCard" + cardIndex, typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = centerCard
            ? new Color(0.11f, 0.16f, 0.22f, 0.96f)
            : new Color(0.08f, 0.11f, 0.16f, 0.86f);

        Button button = card.GetComponent<Button>();
        int capturedCardIndex = cardIndex;
        button.onClick.AddListener(() =>
        {
            if (ConsumePilotSelectionClickSuppression())
                return;

            AudioManager.Instance?.PlayClick();
            OnPilotSelectionCardClicked(capturedCardIndex);
        });

        ProfilePilotSelectionSwipeHandler swipeHandler = card.AddComponent<ProfilePilotSelectionSwipeHandler>();
        swipeHandler.owner = this;

        GameObject imageObject = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(card.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = centerCard ? new Vector2(22f, 58f) : new Vector2(18f, 50f);
        imageRect.offsetMax = centerCard ? new Vector2(-22f, -28f) : new Vector2(-18f, -24f);
        previewImage = imageObject.GetComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;

        nameText = CreateText(card.transform, "Name", "PILOT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(0f, 38f), centerCard ? 28f : 22f, TextAlignmentOptions.Center);
        nameText.raycastTarget = false;
        nameText.gameObject.SetActive(false);

        lockText = CreateText(card.transform, "LockText", string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 56f, 118f), centerCard ? 22f : 16f, TextAlignmentOptions.Center);
        lockText.textWrappingMode = TextWrappingModes.Normal;
        lockText.color = new Color(1f, 0.92f, 0.74f, 1f);
        lockText.raycastTarget = false;

        return card;
    }

    GameObject CreateShipSelectionCard(Transform parent, int cardIndex, Vector2 anchoredPosition, Vector2 size, bool centerCard, out Image previewImage, out TMP_Text titleText, out TMP_Text lockText, out Button donateButton, out TMP_Text[] statLabelTexts, out TMP_Text[] statValueTexts, out Image[] statFillImages, out GameObject[] slotObjects)
    {
        GameObject card = new GameObject("ShipSelectionCard" + cardIndex, typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = centerCard
            ? new Color(0.11f, 0.16f, 0.22f, 0.96f)
            : new Color(0.09f, 0.13f, 0.18f, 0.9f);

        Button button = card.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        int capturedCardIndex = cardIndex;
        button.onClick.AddListener(() =>
        {
            if (ConsumeShipSelectionClickSuppression())
                return;

            AudioManager.Instance?.PlayClick();
            OnShipSelectionCardClicked(capturedCardIndex);
        });

        ProfileShipSelectionSwipeHandler swipeHandler = card.AddComponent<ProfileShipSelectionSwipeHandler>();
        swipeHandler.owner = this;

        titleText = CreateText(card.transform, "Title", "SHIP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(size.x - 40f, 40f), centerCard ? 34f : 26f, TextAlignmentOptions.Center);
        titleText.raycastTarget = false;

        GameObject imageObject = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(card.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = centerCard ? new Vector2(18f, 36f) : new Vector2(10f, 26f);
        imageRect.sizeDelta = centerCard ? new Vector2(680f, 820f) : new Vector2(470f, 560f);
        previewImage = imageObject.GetComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;

        lockText = CreateText(card.transform, "LockText", string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), centerCard ? new Vector2(18f, 18f) : new Vector2(10f, 12f), new Vector2(size.x - 80f, 118f), centerCard ? 28f : 20f, TextAlignmentOptions.Center);
        lockText.textWrappingMode = TextWrappingModes.Normal;
        lockText.color = new Color(1f, 0.92f, 0.72f, 0.98f);
        lockText.raycastTarget = false;

        donateButton = CreateButton(card.transform, "ViperDonateButton", "Repair with spare parts", centerCard ? new Vector2(18f, -218f) : new Vector2(10f, -162f), centerCard ? new Vector2(118f, 118f) : new Vector2(92f, 92f), OnViperRepairDonateClicked);
        ConfigureViperRepairButtonLabel(donateButton, centerCard);
        StyleButton(donateButton, new Color(0.12f, 0.38f, 0.22f, 0.98f), new Color(0.18f, 0.58f, 0.34f, 1f));
        donateButton.gameObject.SetActive(false);

        slotObjects = new GameObject[PlayerInventoryData.EquipmentSlotCount];
        Vector2[] slotLayout = BuildShipSelectionSlotLayout(centerCard);
        for (int i = 0; i < slotObjects.Length; i++)
        {
            GameObject slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Outline));
            slot.transform.SetParent(card.transform, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = slotLayout[i];
            slotRect.sizeDelta = GetShipSelectionSlotSize(centerCard);
            Image slotImage = slot.GetComponent<Image>();
            slotImage.color = GetShipSelectionSlotColor(i);
            slotImage.raycastTarget = false;
            Outline outline = slot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = GetShipSelectionSlotOutlineColor(i);
                outline.effectDistance = new Vector2(2.2f, -2.2f);
            }

            TMP_Text slotText = CreateText(slot.transform, "SlotText", GetShipSelectionSlotLabel(i), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, EquipmentSlotPreviewFontSize, TextAlignmentOptions.Center);
            ApplyEquipmentSlotPreviewTextStyle(slotText);
            slotText.raycastTarget = false;
            RectTransform slotTextRect = slotText.rectTransform;
            slotTextRect.anchorMin = Vector2.zero;
            slotTextRect.anchorMax = Vector2.one;
            slotTextRect.pivot = new Vector2(0.5f, 0.5f);
            slotTextRect.offsetMin = new Vector2(4f, 4f);
            slotTextRect.offsetMax = new Vector2(-4f, -4f);
            slotTextRect.anchoredPosition = Vector2.zero;
            slotObjects[i] = slot;
        }

        CreateShipSelectionStatCards(card.transform, out statLabelTexts, out statValueTexts, out statFillImages);

        return card;
    }

    void ConfigureViperRepairButtonLabel(Button button, bool centerCard)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            return;

        text.text = "Repair with spare parts";
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = centerCard ? 10f : 8f;
        text.fontSizeMax = centerCard ? 17f : 13f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.margin = centerCard
            ? new Vector4(8f, 8f, 8f, 8f)
            : new Vector4(6f, 6f, 6f, 6f);
    }

    Vector2[] BuildShipSelectionSlotLayout(bool centerCard)
    {
        return BuildShipSelectionSlotLayout(centerCard ? 1f : 0f);
    }

    Vector2[] BuildShipSelectionSlotLayout(float centerAmount)
    {
        centerAmount = Mathf.Clamp01(centerAmount);
        float leftColumnX = Mathf.Lerp(-250f, -346f, centerAmount);
        float rightColumnX = Mathf.Lerp(-130f, -208f, centerAmount);
        float topY = Mathf.Lerp(252f, 300f, centerAmount);
        float rowSpacing = EquipmentSlotPreviewSize + 12f;

        Vector2[] result = new Vector2[PlayerInventoryData.EquipmentSlotCount];
        int[][] rowOrder =
        {
            new[] { 0, 1 },
            new[] { 2, 3 },
            new[] { 4, 5 },
            new[] { 8, 9 },
            new[] { 10, 11 },
            new[] { 6, 7 }
        };

        for (int row = 0; row < rowOrder.Length; row++)
        {
            for (int col = 0; col < rowOrder[row].Length; col++)
            {
                int slotIndex = rowOrder[row][col];
                result[slotIndex] = new Vector2(col == 0 ? leftColumnX : rightColumnX, topY - (row * rowSpacing));
            }
        }

        return result;
    }

    Vector2 GetShipSelectionSlotSize(bool centerCard)
    {
        float size = EquipmentSlotPreviewSize;
        return new Vector2(size, size);
    }

    string GetShipSelectionSlotLabel(int slotIndex)
    {
        return slotIndex switch
        {
            0 or 1 => "MAIN GUN",
            2 or 3 => "SHIELD",
            4 or 5 => "ENGINE",
            6 or 7 => "GADGET",
            8 or 9 => "SUPPORT",
            10 or 11 => "RESCUE",
            _ => "SLOT"
        };
    }

    Color GetShipSelectionSlotColor(int slotIndex)
    {
        return InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex) switch
        {
            InventoryItemCategory.Weapon => new Color(0.19f, 0.22f, 0.34f, 0.96f),
            InventoryItemCategory.Shield => new Color(0.17f, 0.29f, 0.28f, 0.96f),
            InventoryItemCategory.Engine => new Color(0.24f, 0.29f, 0.18f, 0.96f),
            InventoryItemCategory.Support => new Color(0.13f, 0.31f, 0.32f, 0.96f),
            InventoryItemCategory.Rescue => new Color(0.34f, 0.19f, 0.2f, 0.96f),
            InventoryItemCategory.Gadget => new Color(0.24f, 0.21f, 0.31f, 0.96f),
            _ => new Color(0.17f, 0.22f, 0.28f, 0.96f)
        };
    }

    Color GetShipSelectionSlotOutlineColor(int slotIndex)
    {
        return InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex) switch
        {
            InventoryItemCategory.Weapon => new Color(0.5f, 0.56f, 0.78f, 0.9f),
            InventoryItemCategory.Shield => new Color(0.43f, 0.68f, 0.64f, 0.9f),
            InventoryItemCategory.Engine => new Color(0.62f, 0.7f, 0.42f, 0.9f),
            InventoryItemCategory.Support => new Color(0.34f, 0.72f, 0.7f, 0.9f),
            InventoryItemCategory.Rescue => new Color(0.74f, 0.45f, 0.46f, 0.9f),
            InventoryItemCategory.Gadget => new Color(0.61f, 0.51f, 0.72f, 0.9f),
            _ => new Color(0.55f, 0.63f, 0.7f, 0.82f)
        };
    }

    void CreateShipSelectionStatCards(Transform parent, out TMP_Text[] labelTexts, out TMP_Text[] valueTexts, out Image[] fillImages)
    {
        int count = ShipStatLabels.Length;
        labelTexts = new TMP_Text[count];
        valueTexts = new TMP_Text[count];
        fillImages = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject cardObject = new GameObject("ShipSelectionStatCard_" + ShipStatLabels[i], typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(parent, false);

            Image cardImage = cardObject.GetComponent<Image>();
            cardImage.color = new Color(0.11f, 0.15f, 0.2f, 0.84f);
            cardImage.raycastTarget = false;

            labelTexts[i] = CreateText(cardObject.transform, "Label", ShipStatLabels[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -12f), new Vector2(88f, 18f), 14f, TextAlignmentOptions.Left);
            labelTexts[i].fontStyle = FontStyles.Bold;
            labelTexts[i].color = new Color(0.82f, 0.88f, 0.94f, 0.94f);
            labelTexts[i].raycastTarget = false;

            valueTexts[i] = CreateText(cardObject.transform, "Value", string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -12f), new Vector2(76f, 18f), 14f, TextAlignmentOptions.Right);
            valueTexts[i].fontStyle = FontStyles.Bold;
            valueTexts[i].color = new Color(0.96f, 0.98f, 1f, 0.98f);
            valueTexts[i].raycastTarget = false;

            GameObject barBgObject = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgObject.transform.SetParent(cardObject.transform, false);
            RectTransform barBgRect = barBgObject.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot = new Vector2(0.5f, 0f);
            barBgRect.anchoredPosition = new Vector2(0f, 8f);
            barBgRect.sizeDelta = new Vector2(-18f, 12f);

            Image barBgImage = barBgObject.GetComponent<Image>();
            barBgImage.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);
            barBgImage.raycastTarget = false;

            GameObject barFillObject = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillObject.transform.SetParent(barBgObject.transform, false);
            RectTransform barFillRect = barFillObject.GetComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0f);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = Vector2.zero;
            barFillRect.sizeDelta = new Vector2(0f, 0f);

            fillImages[i] = barFillObject.GetComponent<Image>();
            fillImages[i].color = new Color(0.28f, 0.86f, 0.36f, 0.98f);
            fillImages[i].raycastTarget = false;
        }
    }

    void CreateShipPreview(Transform parent)
    {
        GameObject previewRoot = new GameObject("ShipPreviewRoot", typeof(RectTransform), typeof(Image));
        previewRoot.transform.SetParent(parent, false);

        RectTransform rootRect = previewRoot.GetComponent<RectTransform>();
        shipPreviewRootRect = rootRect;
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-40f, -556f);
        rootRect.sizeDelta = new Vector2(640f, 380f);

        Image rootImage = previewRoot.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0f);
        rootImage.raycastTarget = false;
        rootImage.enabled = false;

        GameObject hitboxObject = new GameObject("ShipPreviewHitbox", typeof(RectTransform), typeof(Image), typeof(Button));
        hitboxObject.transform.SetParent(previewRoot.transform, false);
        RectTransform hitboxRect = hitboxObject.GetComponent<RectTransform>();
        hitboxRect.anchorMin = new Vector2(0.5f, 0.5f);
        hitboxRect.anchorMax = new Vector2(0.5f, 0.5f);
        hitboxRect.pivot = new Vector2(0.5f, 0.5f);
        hitboxRect.anchoredPosition = new Vector2(0f, 0f);
        hitboxRect.sizeDelta = new Vector2(430f, 280f);

        Image hitboxImage = hitboxObject.GetComponent<Image>();
        hitboxImage.color = new Color(1f, 1f, 1f, 0f);
        shipPreviewButton = hitboxObject.GetComponent<Button>();
        shipPreviewButton.transition = Selectable.Transition.None;
        shipPreviewButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnShipPreviewClicked();
        });

        GameObject imageObject = new GameObject("ShipPreviewImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(previewRoot.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = ShipPreviewImagePosition;
        imageRect.sizeDelta = new Vector2(294f, 188f);
        shipPreviewImage = imageObject.GetComponent<Image>();
        shipPreviewImage.preserveAspect = true;
        shipPreviewImage.raycastTarget = false;

        CreateShipStatsPanel(parent);

        equipmentSlotRects = new RectTransform[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotButtons = new Button[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotPreviewTexts = new TMP_Text[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotPreviewIcons = new Image[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotButtons[0] = CreateEquipmentSlotButton(previewRoot.transform, "MainGunA", Vector2.zero, 0, "MAIN GUN", out equipmentSlotPreviewTexts[0], out equipmentSlotPreviewIcons[0]);
        equipmentSlotButtons[1] = CreateEquipmentSlotButton(previewRoot.transform, "MainGunB", Vector2.zero, 1, "MAIN GUN", out equipmentSlotPreviewTexts[1], out equipmentSlotPreviewIcons[1]);
        equipmentSlotButtons[2] = CreateEquipmentSlotButton(previewRoot.transform, "ShieldA", Vector2.zero, 2, "SHIELD", out equipmentSlotPreviewTexts[2], out equipmentSlotPreviewIcons[2]);
        equipmentSlotButtons[3] = CreateEquipmentSlotButton(previewRoot.transform, "ShieldB", Vector2.zero, 3, "SHIELD", out equipmentSlotPreviewTexts[3], out equipmentSlotPreviewIcons[3]);
        equipmentSlotButtons[4] = CreateEquipmentSlotButton(previewRoot.transform, "EngineA", Vector2.zero, 4, "ENGINE", out equipmentSlotPreviewTexts[4], out equipmentSlotPreviewIcons[4]);
        equipmentSlotButtons[5] = CreateEquipmentSlotButton(previewRoot.transform, "EngineB", Vector2.zero, 5, "ENGINE", out equipmentSlotPreviewTexts[5], out equipmentSlotPreviewIcons[5]);
        equipmentSlotButtons[6] = CreateEquipmentSlotButton(previewRoot.transform, "GadgetA", Vector2.zero, 6, "GADGET", out equipmentSlotPreviewTexts[6], out equipmentSlotPreviewIcons[6]);
        equipmentSlotButtons[7] = CreateEquipmentSlotButton(previewRoot.transform, "GadgetB", Vector2.zero, 7, "GADGET", out equipmentSlotPreviewTexts[7], out equipmentSlotPreviewIcons[7]);
        equipmentSlotButtons[8] = CreateEquipmentSlotButton(previewRoot.transform, "SupportA", Vector2.zero, 8, "SUPPORT", out equipmentSlotPreviewTexts[8], out equipmentSlotPreviewIcons[8]);
        equipmentSlotButtons[9] = CreateEquipmentSlotButton(previewRoot.transform, "SupportB", Vector2.zero, 9, "SUPPORT", out equipmentSlotPreviewTexts[9], out equipmentSlotPreviewIcons[9]);
        equipmentSlotButtons[10] = CreateEquipmentSlotButton(previewRoot.transform, "RescueA", Vector2.zero, 10, "RESCUE", out equipmentSlotPreviewTexts[10], out equipmentSlotPreviewIcons[10]);
        equipmentSlotButtons[11] = CreateEquipmentSlotButton(previewRoot.transform, "RescueB", Vector2.zero, 11, "RESCUE", out equipmentSlotPreviewTexts[11], out equipmentSlotPreviewIcons[11]);

        for (int i = 0; i < equipmentSlotButtons.Length; i++)
        {
            if (equipmentSlotButtons[i] != null)
                equipmentSlotRects[i] = equipmentSlotButtons[i].GetComponent<RectTransform>();
        }

        UpdateEquipmentSlotLayout();
    }

    void CreateShipImageModal(Transform parent)
    {
        shipImageModalObject = new GameObject("ShipImageModal", typeof(RectTransform), typeof(Image));
        shipImageModalObject.transform.SetParent(parent, false);

        RectTransform rootRect = shipImageModalObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = shipImageModalObject.GetComponent<Image>();
        overlay.color = new Color(0.02f, 0.03f, 0.05f, 0.9f);

        GameObject panel = new GameObject("ShipImageModalPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(shipImageModalObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(1080f, 760f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

        CreateText(panel.transform, "ShipImageModalTitle", "SHIP PREVIEW", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(360f, 36f), 28f, TextAlignmentOptions.Center);

        GameObject imageObject = new GameObject("ShipImageModalPreview", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(panel.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(0f, 18f);
        imageRect.sizeDelta = new Vector2(840f, 520f);

        shipImageModalImage = imageObject.GetComponent<Image>();
        shipImageModalImage.preserveAspect = true;

        Button closeButton = CreateButton(panel.transform, "ShipImageModalCloseButton", "CLOSE", new Vector2(0f, -660f), new Vector2(210f, 60f), HideShipImageModal);
        StyleButton(closeButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        shipImageModalObject.SetActive(false);
    }

    void CreatePilotPortrait(Transform parent)
    {
        pilotPortraitRootObject = new GameObject("PilotPortraitRoot", typeof(RectTransform));
        pilotPortraitRootObject.transform.SetParent(parent, false);

        GameObject buttonObject = new GameObject("PilotPortraitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(pilotPortraitRootObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(278f, 278f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.08f, 0.12f, 0.17f, 0.94f);

        pilotPortraitButton = buttonObject.GetComponent<Button>();
        pilotPortraitButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnPilotPortraitClicked();
        });

        GameObject portraitObject = new GameObject("PilotPortraitImage", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(buttonObject.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = new Vector2(8f, 8f);
        portraitRect.offsetMax = new Vector2(-8f, -8f);
        pilotPortraitImage = portraitObject.GetComponent<Image>();
        pilotPortraitImage.preserveAspect = true;
        pilotPortraitImage.raycastTarget = false;

        pilotPortraitNameText = CreateText(pilotPortraitRootObject.transform, "PilotPortraitName", "JAKE", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -42f), new Vector2(340f, 34f), 24f, TextAlignmentOptions.Center);
        pilotPortraitNameText.raycastTarget = false;

        pilotPortraitCaptionText = CreateText(pilotPortraitRootObject.transform, "PilotPortraitCaption", "PILOT", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 294f), new Vector2(260f, 52f), 36f, TextAlignmentOptions.Center);
        pilotPortraitCaptionText.fontStyle = FontStyles.Normal;
        pilotPortraitCaptionText.color = new Color(0.78f, 0.86f, 0.94f, 0.94f);
        pilotPortraitCaptionText.raycastTarget = false;
    }

    void CreateProjectsHomeButton(Transform parent)
    {
        projectsButtonRootObject = new GameObject("ProjectsButtonRoot", typeof(RectTransform));
        projectsButtonRootObject.transform.SetParent(parent, false);

        GameObject buttonObject = new GameObject("ProjectsScreenButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(projectsButtonRootObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(342f, 210f);

        Image buttonBackgroundImage = buttonObject.GetComponent<Image>();
        buttonBackgroundImage.color = new Color(0.02f, 0.04f, 0.07f, 1f);
        buttonBackgroundImage.raycastTarget = true;

        GameObject previewObject = new GameObject("ProjectsScreenButtonPreview", typeof(RectTransform), typeof(Image));
        previewObject.transform.SetParent(buttonObject.transform, false);
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = new Vector2(6f, 6f);
        previewRect.offsetMax = new Vector2(-6f, -6f);

        projectsButtonImage = previewObject.GetComponent<Image>();
        projectsButtonImage.sprite = LoadStandaloneSprite("PROJECTS_SCREEN.png");
        projectsButtonImage.color = projectsButtonImage.sprite != null ? Color.white : new Color(0.08f, 0.12f, 0.17f, 1f);
        projectsButtonImage.preserveAspect = false;
        projectsButtonImage.raycastTarget = false;

        projectsButton = buttonObject.GetComponent<Button>();
        projectsButton.targetGraphic = buttonBackgroundImage;
        ColorBlock buttonColors = projectsButton.colors;
        buttonColors.normalColor = new Color(0.02f, 0.04f, 0.07f, 1f);
        buttonColors.selectedColor = new Color(0.04f, 0.08f, 0.12f, 1f);
        buttonColors.highlightedColor = new Color(0.04f, 0.08f, 0.12f, 1f);
        buttonColors.pressedColor = new Color(0.01f, 0.025f, 0.045f, 1f);
        buttonColors.disabledColor = new Color(0.02f, 0.03f, 0.04f, 0.95f);
        projectsButton.colors = buttonColors;
        projectsButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnProjectsHomeButtonClicked();
        });

        projectsButtonCaptionText = CreateText(projectsButtonRootObject.transform, "ProjectsButtonCaption", "PROJECTS", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 226f), new Vector2(300f, 52f), 36f, TextAlignmentOptions.Center);
        projectsButtonCaptionText.fontStyle = FontStyles.Normal;
        projectsButtonCaptionText.color = new Color(0.78f, 0.86f, 0.94f, 0.94f);
        projectsButtonCaptionText.raycastTarget = false;
    }

    void CreateShipStatsPanel(Transform parent)
    {
        shipStatsPanelObject = new GameObject("ShipStatsPanel", typeof(RectTransform));
        shipStatsPanelObject.transform.SetParent(parent, false);

        RectTransform panelRect = shipStatsPanelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-38f, -500f);
        panelRect.sizeDelta = new Vector2(504f, 180f);

        int cardCount = ShipStatLabels.Length;
        const int columns = 3;
        const float cardWidth = 156f;
        const float cardHeight = 52f;
        const float cardSpacingX = 12f;
        const float cardSpacingY = 12f;

        shipStatLabelTexts = new TMP_Text[cardCount];
        shipStatValueTexts = new TMP_Text[cardCount];
        shipStatFillImages = new Image[cardCount];

        for (int i = 0; i < cardCount; i++)
        {
            int row = i / columns;
            int col = i % columns;
            float x = col * (cardWidth + cardSpacingX);
            float y = -row * (cardHeight + cardSpacingY);

            GameObject cardObject = new GameObject("ShipStatCard_" + ShipStatLabels[i], typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(shipStatsPanelObject.transform, false);

            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = new Vector2(x, y);
            cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);

            Image cardImage = cardObject.GetComponent<Image>();
            cardImage.color = new Color(0.11f, 0.15f, 0.2f, 0.84f);

            TMP_Text labelText = CreateText(cardObject.transform, "Label", ShipStatLabels[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -12f), new Vector2(88f, 18f), 14f, TextAlignmentOptions.Left);
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = new Color(0.82f, 0.88f, 0.94f, 0.94f);
            shipStatLabelTexts[i] = labelText;

            TMP_Text valueText = CreateText(cardObject.transform, "Value", string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -12f), new Vector2(76f, 18f), 14f, TextAlignmentOptions.Right);
            valueText.fontStyle = FontStyles.Bold;
            valueText.color = new Color(0.96f, 0.98f, 1f, 0.98f);
            shipStatValueTexts[i] = valueText;

            GameObject barBgObject = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgObject.transform.SetParent(cardObject.transform, false);
            RectTransform barBgRect = barBgObject.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot = new Vector2(0.5f, 0f);
            barBgRect.anchoredPosition = new Vector2(0f, 8f);
            barBgRect.sizeDelta = new Vector2(-18f, 12f);

            Image barBgImage = barBgObject.GetComponent<Image>();
            barBgImage.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);

            GameObject barFillObject = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillObject.transform.SetParent(barBgObject.transform, false);
            RectTransform barFillRect = barFillObject.GetComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0f);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = Vector2.zero;
            barFillRect.sizeDelta = new Vector2(0f, 0f);

            Image barFillImage = barFillObject.GetComponent<Image>();
            barFillImage.color = new Color(0.28f, 0.86f, 0.36f, 0.98f);
            shipStatFillImages[i] = barFillImage;
        }
    }

    void CreateSplashScreen(Transform parent)
    {
        splashScreenObject = new GameObject("StartupSplashScreen", typeof(RectTransform), typeof(Image));
        splashScreenObject.transform.SetParent(parent, false);

        RectTransform rect = splashScreenObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image splashBackground = splashScreenObject.GetComponent<Image>();
        splashBackground.color = Color.black;
        splashBackground.raycastTarget = false;

        GameObject logoObject = new GameObject("StartupSplashLogo", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(splashScreenObject.transform, false);
        RectTransform logoRect = logoObject.GetComponent<RectTransform>();
        logoRect.anchorMin = Vector2.zero;
        logoRect.anchorMax = Vector2.one;
        logoRect.offsetMin = Vector2.zero;
        logoRect.offsetMax = Vector2.zero;
        logoRect.sizeDelta = Vector2.zero;

        splashScreenImage = logoObject.GetComponent<Image>();
        splashScreenImage.color = Color.white;
        splashScreenImage.preserveAspect = false;
        splashScreenImage.raycastTarget = false;
        splashScreenImage.sprite = LoadStandaloneSprite("STARJACKERS_screen.png");

        if (!splashShownOnce)
        {
            splashHideTime = Time.unscaledTime + 3f;
            splashShownOnce = true;
        }
        else
        {
            splashHideTime = -1f;
            splashScreenObject.SetActive(false);
        }
    }
}
