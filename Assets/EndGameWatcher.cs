using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EndGameWatcher : MonoBehaviour
{
    private static EndGameWatcher instance;

    private bool hasSeenRunningGame = false;
    private bool endScreenShown = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("EndGameWatcher");
        instance = go.AddComponent<EndGameWatcher>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            hasSeenRunningGame = false;
            endScreenShown = false;
            return;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value))
        {
            return;
        }

        bool isGameStarted = (bool)value;

        if (isGameStarted)
        {
            hasSeenRunningGame = true;
            endScreenShown = false;
            return;
        }

        if (!hasSeenRunningGame || endScreenShown)
        {
            return;
        }

        endScreenShown = true;
        StartCoroutine(ShowEndScreenNextFrame());
    }

    IEnumerator ShowEndScreenNextFrame()
    {
        yield return null;

        float waitUntil = Time.unscaledTime + PlayerHealth.EvacuationAnimationDurationSeconds + 1f;
        while (IsLocalPlayerEvacuationAnimating() && Time.unscaledTime < waitUntil)
            yield return null;

        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        EarlyRoundExitUI.HideAll();
        HideLobbyUnderSummary();
        AwardRoundXpIfNeeded();

        GameObject endScreenRoot = FindObjectEvenIfDisabled("EndScreen");
        if (endScreenRoot != null)
        {
            endScreenRoot.transform.localScale = Vector3.one;
            endScreenRoot.SetActive(true);
        }

        EndScreenUI ui = FindAnyObjectByType<EndScreenUI>();

        if (ui != null)
        {
            EnsureEndScreenBackdrop(ui);
            ShowEndScreenUi(ui);
            FixEndScreenLayout(ui);
            EnsureBackButton(ui);
            DisableRestartButton();
            PopulateScoreboard(ui);
            RoundPilotHudUI.HideAllRuntimeObjects();
        }
        else
        {
            Debug.LogError("EndGameWatcher: EndScreenUI NOT FOUND");
        }
    }

    bool IsLocalPlayerEvacuationAnimating()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            PhotonView view = player != null ? player.photonView : null;
            if (view != null && view.IsMine && player.IsEvacuationAnimating)
                return true;
        }

        return false;
    }

    GameObject FindObjectEvenIfDisabled(string name)
    {
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (var go in allObjects)
        {
            if (go.name == name)
            {
                return go;
            }
        }

        return null;
    }

    void ShowEndScreenUi(EndScreenUI ui)
    {
        if (ui.panel != null)
        {
            ui.panel.SetActive(true);
        }

        if (ui.endMessage != null)
        {
            ui.endMessage.text = "ROUND COMPLETE";
        }

        GameObject listObject = FindObjectEvenIfDisabled("PlayerListContent");
        if (listObject != null && listObject.scene.IsValid())
        {
            ui.playerListParent = listObject.transform;
        }

        if (ui.playerItemPrefab == null)
        {
            ui.playerItemPrefab = Resources.Load<GameObject>("PlayerListItem");
        }
    }

    void HideLobbyUnderSummary()
    {
        LobbyManager lobby = FindAnyObjectByType<LobbyManager>();
        if (lobby == null)
            return;

        CanvasGroup canvasGroup = lobby.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = lobby.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void FixEndScreenLayout(EndScreenUI ui)
    {
        GameObject summaryPanel = ResolveEndScreenPanel(ui);
        if (summaryPanel != null)
        {
            RectTransform panelRect = summaryPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(900f, 600f);
            }

            StyleSummaryPanel(summaryPanel);
            EnsurePanelAccent(summaryPanel);
            EnsureEndScreenSubtitle(ui, summaryPanel);
            DisableEndScreenInputFields(summaryPanel);
        }

        GameObject messageObject = FindObjectEvenIfDisabled("EndMessage");
        if (messageObject != null)
        {
            DisableEndMessageInput(messageObject);

            RectTransform rect = messageObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -32f);
                rect.sizeDelta = new Vector2(-112f, 54f);
            }
        }

        GameObject messageTextObject = FindObjectEvenIfDisabled("EndMessageText");
        if (messageTextObject != null)
        {
            TextMeshProUGUI messageText = messageTextObject.GetComponent<TextMeshProUGUI>();
            if (messageText != null)
            {
                messageText.fontSize = 42f;
                messageText.fontStyle = FontStyles.Bold;
                messageText.color = new Color(0.92f, 0.98f, 1f, 1f);
                messageText.alignment = TextAlignmentOptions.Left;
                messageText.characterSpacing = 1.6f;
                messageText.raycastTarget = false;
            }
        }

        GameObject listObject = FindObjectEvenIfDisabled("PlayerListContent");
        if (listObject != null)
        {
            RectTransform rect = listObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(56f, 130f);
                rect.offsetMax = new Vector2(-56f, -146f);
            }

            Image listImage = listObject.GetComponent<Image>();
            if (listImage == null)
                listImage = listObject.AddComponent<Image>();
            listImage.color = new Color(0.012f, 0.019f, 0.028f, 0.92f);
            listImage.raycastTarget = false;

            VerticalLayoutGroup layout = listObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = listObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 10f;
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
    }

    void EnsureEndScreenBackdrop(EndScreenUI ui)
    {
        if (ui == null || ui.panel == null || ui.panel.transform.parent == null)
            return;

        Transform parent = ui.panel.transform.parent;
        Transform existing = parent.Find("EndScreenRuntimeBackdrop");
        GameObject backdrop = existing != null ? existing.gameObject : new GameObject("EndScreenRuntimeBackdrop", typeof(RectTransform), typeof(Image));
        if (backdrop.transform.parent != parent)
            backdrop.transform.SetParent(parent, false);

        RectTransform rect = backdrop.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = backdrop.GetComponent<Image>();
        image.color = new Color(0.01f, 0.018f, 0.03f, 0.68f);
        image.raycastTarget = true;

        int panelIndex = ui.panel.transform.GetSiblingIndex();
        backdrop.transform.SetSiblingIndex(Mathf.Max(0, panelIndex));
        ui.panel.transform.SetAsLastSibling();
    }

    void EnsurePanelAccent(GameObject panel)
    {
        Transform existing = panel.transform.Find("EndScreenRuntimeAccent");
        GameObject accent = existing != null ? existing.gameObject : new GameObject("EndScreenRuntimeAccent", typeof(RectTransform), typeof(Image));
        if (accent.transform.parent != panel.transform)
            accent.transform.SetParent(panel.transform, false);

        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 8f);

        Image image = accent.GetComponent<Image>();
        image.color = new Color(0.08f, 0.62f, 0.78f, 1f);
        image.raycastTarget = false;
    }

    void EnsureEndScreenSubtitle(EndScreenUI ui, GameObject panel)
    {
        if (ui == null || panel == null)
            return;

        Transform existing = panel.transform.Find("EndScreenRuntimeSubtitle");
        GameObject subtitleObject = existing != null ? existing.gameObject : new GameObject("EndScreenRuntimeSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (subtitleObject.transform.parent != panel.transform)
            subtitleObject.transform.SetParent(panel.transform, false);

        RectTransform rect = subtitleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -84f);
        rect.sizeDelta = new Vector2(-96f, 26f);

        TextMeshProUGUI text = subtitleObject.GetComponent<TextMeshProUGUI>();
        text.text = "Final round results";
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Left;
        text.color = new Color(0.68f, 0.78f, 0.88f, 0.95f);
        text.raycastTarget = false;

        if (ui.endMessage != null)
        {
            text.font = ui.endMessage.font;
            text.fontSharedMaterial = ui.endMessage.fontSharedMaterial;
        }
    }

    GameObject ResolveEndScreenPanel(EndScreenUI ui)
    {
        GameObject gameOverPanel = FindObjectEvenIfDisabled("GameOver");
        if (gameOverPanel != null && gameOverPanel.scene.IsValid())
            return gameOverPanel;

        return ui != null ? ui.panel : null;
    }

    void StyleSummaryPanel(GameObject panel)
    {
        if (panel == null)
            return;

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0.035f, 0.055f, 0.082f, 0.98f);
            panelImage.type = Image.Type.Sliced;
        }
    }

    void ApplyButtonColors(Button button, Color normal, Color highlighted, Color pressed)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = highlighted;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.42f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    void DisableEndScreenInputFields(GameObject root)
    {
        if (root == null)
            return;

        TMP_InputField[] tmpInputs = root.GetComponentsInChildren<TMP_InputField>(true);
        for (int i = 0; i < tmpInputs.Length; i++)
        {
            TMP_InputField input = tmpInputs[i];
            if (input == null)
                continue;

            input.DeactivateInputField();
            input.readOnly = true;
            input.interactable = false;
            input.enabled = false;
        }

        InputField[] legacyInputs = root.GetComponentsInChildren<InputField>(true);
        for (int i = 0; i < legacyInputs.Length; i++)
        {
            InputField input = legacyInputs[i];
            if (input == null)
                continue;

            input.DeactivateInputField();
            input.readOnly = true;
            input.interactable = false;
            input.enabled = false;
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            if (graphic.GetComponentInParent<Button>() == null && graphic.GetComponentInParent<Scrollbar>() == null)
                graphic.raycastTarget = false;
        }

        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(root.transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void DisableRestartButton()
    {
        GameObject restartButton = FindObjectEvenIfDisabled("RestartButton");
        if (restartButton != null)
        {
            restartButton.SetActive(false);
        }
    }

    void DisableEndMessageInput(GameObject messageObject)
    {
        TMP_InputField input = messageObject != null ? messageObject.GetComponent<TMP_InputField>() : null;
        if (input != null)
        {
            input.DeactivateInputField();
            input.readOnly = true;
            input.interactable = false;
            input.enabled = false;
        }

        Graphic[] graphics = messageObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == messageObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void EnsureBackButton(EndScreenUI ui)
    {
        GameObject summaryPanel = ResolveEndScreenPanel(ui);
        if (summaryPanel == null)
            return;

        GameObject backButtonObject = FindObjectEvenIfDisabled("BackButton");
        Button backButton = backButtonObject != null ? backButtonObject.GetComponent<Button>() : null;

        if (backButton == null)
        {
            backButtonObject = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backButtonObject.transform.SetParent(summaryPanel.transform, false);
            backButton = backButtonObject.GetComponent<Button>();

            GameObject textObject = new GameObject("BackButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(backButtonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = "BACK";
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            TMP_Text reference = FindAnyObjectByType<TMP_Text>();
            if (reference != null)
            {
                text.font = reference.font;
                text.fontSharedMaterial = reference.fontSharedMaterial;
            }
        }
        else if (backButtonObject.transform.parent != summaryPanel.transform)
        {
            backButtonObject.transform.SetParent(summaryPanel.transform, false);
        }

        backButtonObject.SetActive(true);

        RectTransform rect = backButtonObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            rect.sizeDelta = new Vector2(340f, 86f);
        }

        Image image = backButtonObject.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.08f, 0.42f, 0.5f, 0.98f);
            image.type = Image.Type.Sliced;
        }
        ApplyButtonColors(backButton, new Color(0.08f, 0.42f, 0.5f, 0.98f), new Color(0.12f, 0.58f, 0.68f, 1f), new Color(0.05f, 0.28f, 0.34f, 1f));

        TextMeshProUGUI buttonText = backButtonObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
        {
            buttonText.text = "BACK";
            buttonText.fontSize = 34f;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            buttonText.characterSpacing = 1.6f;
        }

        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(OnBackButtonClicked);
    }

    void OnBackButtonClicked()
    {
        NetworkManager.ReturnToSessionBrowserFromRound();
    }

    void PopulateScoreboard(EndScreenUI ui)
    {
        Transform listParent = ResolveOrCreateScoreboardParent(ui);

        if (listParent == null)
        {
            Debug.LogError("EndGameWatcher: scoreboard references missing");
            return;
        }

        foreach (Transform child in listParent)
        {
            Destroy(child.gameObject);
        }

        RoundResultsSnapshotData snapshot = GetSnapshotFromRoom();
        int rowIndex = 0;
        CreateScoreHeader(listParent, ui);
        if (snapshot != null && snapshot.entries != null && snapshot.entries.Length > 0)
        {
            foreach (RoundResultEntry entry in snapshot.entries.OrderBy(result => result.placement).ThenByDescending(result => result.finalScore))
            {
                int cargoValueAstrons = GetEndScreenCargoValueAstrons(entry);
                int placement = entry.placement > 0 ? entry.placement : rowIndex + 1;
                string nickname = string.IsNullOrWhiteSpace(entry.nickname) ? "Player " + entry.actorNumber : entry.nickname;
                CreateScoreRow(
                    listParent,
                    placement,
                    nickname,
                    entry.finalScore,
                    FormatOutcome(entry.outcome).ToUpperInvariant(),
                    cargoValueAstrons,
                    true,
                    ui,
                    rowIndex);
                rowIndex++;
            }
        }
        else
        {
            var sortedPlayers = PhotonNetwork.PlayerList
                .OrderByDescending(GetPlayerScore)
                .ThenBy(GetDisplayName);

            foreach (Player player in sortedPlayers)
            {
                CreateScoreRow(
                    listParent,
                    rowIndex + 1,
                    GetDisplayName(player),
                    GetPlayerScore(player),
                    "ACTIVE",
                    0,
                    false,
                    ui,
                    rowIndex);
                rowIndex++;
            }
        }

        if (ui.endMessage != null)
        {
            ui.endMessage.text = "ROUND COMPLETE";
        }
    }

    async void AwardRoundXpIfNeeded()
    {
        if (PhotonNetwork.LocalPlayer == null || PhotonNetwork.CurrentRoom == null)
            return;

        int roundXp = 0;
        RoundResultsSnapshotData snapshot = GetSnapshotFromRoom();
        if (snapshot != null && snapshot.entries != null)
        {
            for (int i = 0; i < snapshot.entries.Length; i++)
            {
                if (snapshot.entries[i].actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    roundXp = Mathf.Max(0, snapshot.entries[i].finalScore);
                    break;
                }
            }
        }
        else
        {
            roundXp = Mathf.Max(0, RoomSettings.GetPlayerRoundXp(PhotonNetwork.LocalPlayer));
        }

        string matchToken = PhotonNetwork.CurrentRoom.Name + "_" +
                            (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("startTime", out object startTime)
                                ? startTime.ToString()
                                : "nostart");

        await PlayerProfileService.Instance.RecordRoundXpAsync(roundXp, matchToken);
    }

    RoundResultsSnapshotData GetSnapshotFromRoom()
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundResultsKey, out object value) ||
            value is not string rawSnapshot)
        {
            return null;
        }

        return RoundResultsTracker.DeserializeSnapshot(rawSnapshot);
    }

    Transform ResolveOrCreateScoreboardParent(EndScreenUI ui)
    {
        GameObject listObject = FindObjectEvenIfDisabled("PlayerListContent");
        if (listObject != null && listObject.scene.IsValid())
        {
            ui.playerListParent = listObject.transform;
            return ui.playerListParent;
        }

        if (ui.playerListParent != null && ui.playerListParent.gameObject.scene.IsValid())
        {
            return ui.playerListParent;
        }

        GameObject summaryPanel = ResolveEndScreenPanel(ui);
        if (summaryPanel == null)
        {
            return null;
        }

        Transform existing = summaryPanel.transform.Find("RuntimeScoreboard");
        if (existing != null)
        {
            ui.playerListParent = existing;
            return existing;
        }

        GameObject runtimeList = new GameObject("RuntimeScoreboard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        runtimeList.transform.SetParent(summaryPanel.transform, false);

        RectTransform rect = runtimeList.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(56f, 130f);
        rect.offsetMax = new Vector2(-56f, -146f);

        Image image = runtimeList.GetComponent<Image>();
        image.color = new Color(0.012f, 0.019f, 0.028f, 0.92f);
        image.raycastTarget = false;

        VerticalLayoutGroup layout = runtimeList.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 10f;
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ui.playerListParent = runtimeList.transform;
        return ui.playerListParent;
    }

    void CreateScoreHeader(Transform parent, EndScreenUI ui)
    {
        GameObject row = new GameObject("ScoreHeader", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 26f);

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredHeight = 26f;
        layout.flexibleWidth = 1f;

        CreateScoreCell(row.transform, "HeaderPlace", "#", 0f, 0.08f, 14f, new Color(0.48f, 0.67f, 0.77f, 1f), TextAlignmentOptions.Center, ui);
        CreateScoreCell(row.transform, "HeaderPilot", "PILOT", 0.09f, 0.44f, 14f, new Color(0.48f, 0.67f, 0.77f, 1f), TextAlignmentOptions.Left, ui);
        CreateScoreCell(row.transform, "HeaderXp", "XP", 0.45f, 0.58f, 14f, new Color(0.48f, 0.67f, 0.77f, 1f), TextAlignmentOptions.Right, ui);
        CreateScoreCell(row.transform, "HeaderStatus", "STATUS", 0.60f, 0.78f, 14f, new Color(0.48f, 0.67f, 0.77f, 1f), TextAlignmentOptions.Left, ui);
        CreateScoreCell(row.transform, "HeaderCargo", "CARGO", 0.80f, 1f, 14f, new Color(0.48f, 0.67f, 0.77f, 1f), TextAlignmentOptions.Right, ui);
    }

    void CreateScoreRow(
        Transform parent,
        int placement,
        string nickname,
        int finalScore,
        string outcome,
        int cargoValueAstrons,
        bool includeCargo,
        EndScreenUI ui,
        int rowIndex)
    {
        GameObject row = new GameObject("ScoreRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 48f);

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredHeight = 48f;
        layout.flexibleWidth = 1f;

        Image rowImage = row.GetComponent<Image>();
        rowImage.color = rowIndex % 2 == 0
            ? new Color(0.075f, 0.12f, 0.16f, 0.92f)
            : new Color(0.055f, 0.09f, 0.13f, 0.92f);
        rowImage.raycastTarget = false;

        CreateScoreCell(row.transform, "ScoreRowPlace", "#" + placement, 0f, 0.08f, 20f, new Color(0.72f, 0.84f, 0.92f, 1f), TextAlignmentOptions.Center, ui);
        CreateScoreCell(row.transform, "ScoreRowPilot", string.IsNullOrWhiteSpace(nickname) ? "Player" : nickname, 0.09f, 0.44f, 20f, new Color(0.9f, 0.97f, 1f, 1f), TextAlignmentOptions.Left, ui);
        CreateScoreCell(row.transform, "ScoreRowXp", Mathf.Max(0, finalScore) + " XP", 0.45f, 0.58f, 20f, new Color(1f, 0.88f, 0.5f, 1f), TextAlignmentOptions.Right, ui);
        CreateScoreCell(row.transform, "ScoreRowStatus", string.IsNullOrWhiteSpace(outcome) ? "ACTIVE" : outcome, 0.60f, 0.78f, 19f, GetOutcomeColor(outcome), TextAlignmentOptions.Left, ui);
        CreateScoreCell(row.transform, "ScoreRowCargo", includeCargo ? Mathf.Max(0, cargoValueAstrons) + " ASTRONS" : "-", 0.80f, 1f, 19f, new Color(0.72f, 0.84f, 0.92f, 1f), TextAlignmentOptions.Right, ui);
    }

    TextMeshProUGUI CreateScoreCell(Transform parent, string objectName, string value, float anchorMinX, float anchorMaxX, float fontSize, Color color, TextAlignmentOptions alignment, EndScreenUI ui)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(anchorMinX, 0f);
        textRect.anchorMax = new Vector2(anchorMaxX, 1f);
        textRect.offsetMin = new Vector2(4f, 0f);
        textRect.offsetMax = new Vector2(-4f, 0f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.characterSpacing = 0f;
        text.raycastTarget = false;

        if (ui.endMessage != null)
        {
            text.font = ui.endMessage.font;
            text.fontSharedMaterial = ui.endMessage.fontSharedMaterial;
        }

        return text;
    }

    int GetPlayerScore(Player player)
    {
        int score = RoomSettings.GetPlayerRoundXp(player);
        if (score > 0)
            return score;

        if (player.TagObject is GameObject go && go != null)
        {
            TreasureCollector collector = go.GetComponent<TreasureCollector>();
            if (collector != null)
            {
                return collector.totalScore;
            }
        }

        return 0;
    }

    string GetDisplayName(Player player)
    {
        if (!string.IsNullOrWhiteSpace(player.NickName))
        {
            return player.NickName;
        }

        return "Player " + player.ActorNumber;
    }

    string FormatOutcome(string outcome)
    {
        switch (outcome)
        {
            case "extracted":
                return "extracted";
            case "evacuated":
                return "evacuated";
            case "dead":
                return "destroyed";
            case "lost_in_space":
            case "time_up":
                return "lost in space";
            default:
                return "active";
        }
    }

    Color GetOutcomeColor(string outcome)
    {
        switch ((outcome ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "dead":
            case "destroyed":
            case "lost_in_space":
            case "time_up":
                return new Color(1f, 0.46f, 0.36f, 1f);
            case "extracted":
            case "evacuated":
                return new Color(0.42f, 1f, 0.68f, 1f);
            default:
                return new Color(0.86f, 0.94f, 1f, 1f);
        }
    }

    int GetEndScreenCargoValueAstrons(RoundResultEntry entry)
    {
        if (entry == null)
            return 0;

        switch (entry.outcome)
        {
            case "dead":
            case "lost_in_space":
            case "time_up":
            case "active":
                return 0;
        }

        Player player = PhotonNetwork.PlayerList.FirstOrDefault(p => p != null && p.ActorNumber == entry.actorNumber);
        if (player == null)
            return 0;

        string[] shipSlots = PlayerProfileService.GetPlayerShipInventorySlots(player);
        int totalValue = 0;
        for (int i = 0; i < shipSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(shipSlots[i]))
                continue;

            totalValue += InventoryItemCatalog.GetSellValueAstrons(shipSlots[i]);
        }

        return Mathf.Max(0, totalValue);
    }
}
