using System.Text;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EarlyRoundExitUI : MonoBehaviour
{
    static EarlyRoundExitUI instance;
    static bool buttonRequested;
    static bool summaryRequested;
    static int cachedFinalScore;
    static string cachedOutcome = "dead";
    static bool awardRequested;

    GameObject buttonObject;
    GameObject summaryObject;
    TextMeshProUGUI summaryListText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void ShowEndRoundButton(int finalScore, string outcome)
    {
        EnsureInstance();
        cachedFinalScore = Mathf.Max(0, finalScore);
        cachedOutcome = string.IsNullOrWhiteSpace(outcome) ? "dead" : outcome;
        buttonRequested = true;
        summaryRequested = false;
        awardRequested = false;
        instance.Refresh();
    }

    public static void ShowFinishedRoundSummary(int finalScore, string outcome)
    {
        EnsureInstance();
        cachedFinalScore = Mathf.Max(0, finalScore);
        cachedOutcome = string.IsNullOrWhiteSpace(outcome) ? "finished" : outcome;
        buttonRequested = false;
        summaryRequested = true;
        awardRequested = false;
        NetworkManager.MarkCurrentRoundEndedForLocalPlayer(cachedOutcome);
        instance.Refresh();
        instance.AwardRoundXpIfNeeded();
    }

    public static void HideAll()
    {
        buttonRequested = false;
        summaryRequested = false;
        if (instance != null)
            instance.Refresh();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("EarlyRoundExitUI");
        instance = root.AddComponent<EarlyRoundExitUI>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        Refresh();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Refresh();
    }

    void Refresh()
    {
        if (!ShouldRemainVisible())
        {
            buttonRequested = false;
            summaryRequested = false;
            if (buttonObject != null)
                buttonObject.SetActive(false);
            if (summaryObject != null)
                summaryObject.SetActive(false);
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        if (buttonRequested)
            EnsureButton(canvas.transform);

        if (summaryRequested)
        {
            EnsureSummary(canvas.transform);
            UpdateSummaryText();
        }

        if (buttonObject != null)
            buttonObject.SetActive(buttonRequested && !summaryRequested);

        if (summaryObject != null)
            summaryObject.SetActive(summaryRequested);
    }

    bool ShouldRemainVisible()
    {
        if (!buttonRequested && !summaryRequested)
            return false;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInPlay)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) ||
            value is not bool started ||
            !started)
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundResultsKey, out object snapshotValue) &&
            snapshotValue is string snapshot &&
            !string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        return true;
    }

    void EnsureButton(Transform parent)
    {
        if (buttonObject != null && buttonObject.scene.IsValid())
        {
            if (buttonObject.transform.parent != parent)
                buttonObject.transform.SetParent(parent, false);

            buttonObject.transform.SetAsLastSibling();
            return;
        }

        buttonObject = new GameObject("EarlyEndRoundButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-28f, -112f);
        rect.sizeDelta = new Vector2(230f, 58f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.48f, 0.08f, 0.09f, 0.96f);
        image.type = Image.Type.Sliced;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnEndRoundClicked);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "EarlyEndRoundButtonText", "END ROUND", 24f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 1.5f;
        text.raycastTarget = false;
    }

    void EnsureSummary(Transform parent)
    {
        if (summaryObject != null && summaryObject.scene.IsValid())
        {
            if (summaryObject.transform.parent != parent)
                summaryObject.transform.SetParent(parent, false);

            summaryObject.transform.SetAsLastSibling();
            CacheSummaryText();
            return;
        }

        summaryObject = new GameObject("EarlyRoundSummaryPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        summaryObject.transform.SetParent(parent, false);

        RectTransform rootRect = summaryObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImage = summaryObject.GetComponent<Image>();
        rootImage.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);

        GameObject cardObject = new GameObject("EarlyRoundSummaryCard", typeof(RectTransform), typeof(Image));
        cardObject.transform.SetParent(summaryObject.transform, false);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(760f, 500f);

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.94f, 0.94f, 0.9f, 0.98f);
        cardImage.type = Image.Type.Sliced;

        TextMeshProUGUI title = CreateText(cardObject.transform, "EarlyRoundSummaryTitle", "Round Summary", 40f, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -32f);
        title.rectTransform.sizeDelta = new Vector2(-40f, 62f);
        title.color = new Color(0.15f, 0.18f, 0.24f, 1f);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 2f;

        summaryListText = CreateText(cardObject.transform, "EarlyRoundSummaryList", BuildSummaryText(), 24f, TextAlignmentOptions.Left);
        summaryListText.rectTransform.anchorMin = new Vector2(0f, 0f);
        summaryListText.rectTransform.anchorMax = new Vector2(1f, 1f);
        summaryListText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        summaryListText.rectTransform.offsetMin = new Vector2(56f, 112f);
        summaryListText.rectTransform.offsetMax = new Vector2(-56f, -118f);
        summaryListText.color = new Color(0.14f, 0.17f, 0.23f, 1f);
        summaryListText.fontStyle = FontStyles.Bold;
        summaryListText.textWrappingMode = TextWrappingModes.NoWrap;

        Button backButton = CreateButton(cardObject.transform, "EarlyRoundSummaryBackButton", "BACK", new Vector2(0f, 32f), new Vector2(220f, 56f), OnBackClicked);
        Image backImage = backButton.GetComponent<Image>();
        if (backImage != null)
        {
            backImage.color = new Color(0.18f, 0.22f, 0.28f, 0.96f);
            backImage.type = Image.Type.Sliced;
        }
    }

    void CacheSummaryText()
    {
        if (summaryListText != null || summaryObject == null)
            return;

        Transform textTransform = summaryObject.transform.Find("EarlyRoundSummaryCard/EarlyRoundSummaryList");
        if (textTransform != null)
            summaryListText = textTransform.GetComponent<TextMeshProUGUI>();
    }

    void UpdateSummaryText()
    {
        CacheSummaryText();
        if (summaryListText != null)
            summaryListText.text = BuildSummaryText();
    }

    string BuildSummaryLine()
    {
        string name = PhotonNetwork.LocalPlayer != null && !string.IsNullOrWhiteSpace(PhotonNetwork.LocalPlayer.NickName)
            ? PhotonNetwork.LocalPlayer.NickName
            : "Player";

        return name + " - " + Mathf.Max(0, cachedFinalScore) + " XP - " + cachedOutcome;
    }

    string BuildSummaryText()
    {
        RoundResultsSnapshotData snapshot = RoundResultsTracker.BuildFinishedSnapshotForEarlySummary(
            PhotonNetwork.LocalPlayer,
            cachedFinalScore,
            cachedOutcome);

        if (snapshot == null || snapshot.entries == null || snapshot.entries.Length == 0)
            return BuildSummaryLine();

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < snapshot.entries.Length; i++)
        {
            RoundResultEntry entry = snapshot.entries[i];
            if (entry == null)
                continue;

            int placement = entry.placement > 0 ? entry.placement : i + 1;
            string nickname = string.IsNullOrWhiteSpace(entry.nickname) ? "Player " + entry.actorNumber : entry.nickname;
            builder.Append('#').Append(placement)
                .Append("  ")
                .Append(nickname)
                .Append("  -  ")
                .Append(Mathf.Max(0, entry.finalScore))
                .Append(" XP  -  ")
                .Append(FormatOutcome(entry.outcome));

            if (i < snapshot.entries.Length - 1)
                builder.AppendLine();
        }

        return builder.Length > 0 ? builder.ToString() : BuildSummaryLine();
    }

    string FormatOutcome(string outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
            return "finished";

        switch (outcome.Trim().ToLowerInvariant())
        {
            case "dead":
                return "destroyed";
            case "lost_in_space":
                return "lost in space";
            case "time_up":
                return "time up";
            case "extracted":
                return "extracted";
            case "evacuated":
                return "evacuated";
            default:
                return outcome;
        }
    }

    void OnEndRoundClicked()
    {
        NetworkManager.MarkCurrentRoundEndedForLocalPlayer(cachedOutcome);
        buttonRequested = false;
        summaryRequested = true;
        Refresh();
        AwardRoundXpIfNeeded();
    }

    void OnBackClicked()
    {
        HideAll();
        NetworkManager.ReturnToSessionBrowserFromFinishedRound();
    }

    async void AwardRoundXpIfNeeded()
    {
        if (awardRequested)
            return;

        awardRequested = true;
        string matchToken = BuildMatchToken();
        try
        {
            await PlayerProfileService.Instance.RecordRoundXpAsync(cachedFinalScore, matchToken);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("EarlyRoundExitUI: failed to record early round XP: " + ex);
        }
    }

    string BuildMatchToken()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return "early_round_" + Time.realtimeSinceStartup.ToString("F2");

        string startValue = "nostart";
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startValue = value.ToString();

        return PhotonNetwork.CurrentRoom.Name + "_" + startValue;
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(buttonObject.transform, objectName + "Text", label, 24f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        return button;
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        TMP_Text reference = FindAnyObjectByType<TextMeshProUGUI>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }
}
