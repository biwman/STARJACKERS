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
            ShowEndScreenUi(ui);
            FixEndScreenLayout(ui);
            EnsureBackButton(ui);
            DisableRestartButton();
            PopulateScoreboard(ui);
        }
        else
        {
            Debug.LogError("EndGameWatcher: EndScreenUI NOT FOUND");
        }
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
            ui.endMessage.text = "Round XP";
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
        if (ui.panel != null)
        {
            RectTransform panelRect = ui.panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(720f, 520f);
            }

            Image panelImage = ui.panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.94f, 0.94f, 0.9f, 0.98f);
                panelImage.type = Image.Type.Sliced;
            }
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
                rect.anchoredPosition = new Vector2(0f, -20f);
                rect.sizeDelta = new Vector2(-40f, 70f);
            }
        }

        GameObject messageTextObject = FindObjectEvenIfDisabled("EndMessageText");
        if (messageTextObject != null)
        {
            TextMeshProUGUI messageText = messageTextObject.GetComponent<TextMeshProUGUI>();
            if (messageText != null)
            {
                messageText.fontSize = 40f;
                messageText.fontStyle = FontStyles.Bold;
                messageText.color = new Color(0.15f, 0.18f, 0.24f, 1f);
                messageText.alignment = TextAlignmentOptions.Center;
                messageText.characterSpacing = 2f;
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
                rect.offsetMin = new Vector2(40f, 100f);
                rect.offsetMax = new Vector2(-40f, -100f);
            }
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
        if (ui.panel == null)
            return;

        GameObject backButtonObject = FindObjectEvenIfDisabled("BackButton");
        Button backButton = backButtonObject != null ? backButtonObject.GetComponent<Button>() : null;

        if (backButton == null)
        {
            backButtonObject = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backButtonObject.transform.SetParent(ui.panel.transform, false);
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

        backButtonObject.SetActive(true);

        RectTransform rect = backButtonObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(220f, 56f);
        }

        Image image = backButtonObject.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.18f, 0.22f, 0.28f, 0.96f);
            image.type = Image.Type.Sliced;
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
        if (snapshot != null && snapshot.entries != null && snapshot.entries.Length > 0)
        {
            foreach (RoundResultEntry entry in snapshot.entries.OrderBy(result => result.placement).ThenByDescending(result => result.finalScore))
            {
                int cargoValueAstrons = GetEndScreenCargoValueAstrons(entry);
                CreateScoreRow(
                    listParent,
                    entry.nickname + " - " + entry.finalScore + " XP - " + FormatOutcome(entry.outcome) + " - " + cargoValueAstrons + " Astrons",
                    ui);
            }
        }
        else
        {
            var sortedPlayers = PhotonNetwork.PlayerList
                .OrderByDescending(GetPlayerScore)
                .ThenBy(GetDisplayName);

            foreach (Player player in sortedPlayers)
            {
                CreateScoreRow(listParent, GetDisplayName(player) + " - " + GetPlayerScore(player) + " XP", ui);
            }
        }

        if (ui.endMessage != null)
        {
            ui.endMessage.text = "Round XP";
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

        if (ui.panel == null)
        {
            return null;
        }

        Transform existing = ui.panel.transform.Find("RuntimeScoreboard");
        if (existing != null)
        {
            ui.playerListParent = existing;
            return existing;
        }

        GameObject runtimeList = new GameObject("RuntimeScoreboard", typeof(RectTransform), typeof(VerticalLayoutGroup));
        runtimeList.transform.SetParent(ui.panel.transform, false);

        RectTransform rect = runtimeList.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(40f, 100f);
        rect.offsetMax = new Vector2(-40f, -100f);

        VerticalLayoutGroup layout = runtimeList.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ui.playerListParent = runtimeList.transform;
        return ui.playerListParent;
    }

    void CreateScoreRow(Transform parent, string textValue, EndScreenUI ui)
    {
        GameObject row = new GameObject("ScoreRow", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 42f);

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredHeight = 42f;
        layout.flexibleWidth = 1f;

        TextMeshProUGUI text = row.GetComponent<TextMeshProUGUI>();
        text.text = textValue;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 30f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.14f, 0.17f, 0.23f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.horizontalAlignment = HorizontalAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.characterSpacing = 1.2f;

        if (ui.endMessage != null)
        {
            text.font = ui.endMessage.font;
            text.fontSharedMaterial = ui.endMessage.fontSharedMaterial;
        }
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
                return "dead";
            case "lost_in_space":
            case "time_up":
                return "lost in space";
            default:
                return "active";
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
