using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoundAnnouncementUI : MonoBehaviour
{
    static RoundAnnouncementUI instance;

    Canvas canvas;
    TMP_Text messageText;
    Coroutine activeRoutine;

    public static void Show(string message, float seconds = 2.6f)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureInstance();
        if (instance != null)
            instance.ShowInternal(message, seconds);
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("RoundAnnouncementUI");
        instance = root.AddComponent<RoundAnnouncementUI>();
        DontDestroyOnLoad(root);
        instance.EnsureUi();
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
        EnsureUi();
    }

    void EnsureUi()
    {
        if (canvas != null && messageText != null)
            return;

        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        Transform existing = transform.Find("RoundAnnouncementText");
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject("RoundAnnouncementText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -74f);
        rect.sizeDelta = new Vector2(980f, 82f);

        messageText = textObject.GetComponent<TMP_Text>();
        messageText.fontSize = 42f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = new Color(1f, 0.88f, 0.28f, 0f);
        messageText.raycastTarget = false;
        messageText.text = string.Empty;
    }

    void ShowInternal(string message, float seconds)
    {
        EnsureUi();
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ShowRoutine(message, seconds));
    }

    IEnumerator ShowRoutine(string message, float seconds)
    {
        messageText.text = message;
        float duration = Mathf.Max(0.5f, seconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fadeIn = Mathf.Clamp01(elapsed / 0.22f);
            float fadeOut = Mathf.Clamp01((duration - elapsed) / 0.48f);
            float alpha = Mathf.Min(fadeIn, fadeOut);
            messageText.color = new Color(1f, 0.88f, 0.28f, alpha);
            yield return null;
        }

        messageText.color = new Color(1f, 0.88f, 0.28f, 0f);
        messageText.text = string.Empty;
        activeRoutine = null;
    }
}
