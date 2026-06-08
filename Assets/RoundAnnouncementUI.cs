using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoundAnnouncementUI : MonoBehaviour
{
    static RoundAnnouncementUI instance;

    Canvas canvas;
    TMP_Text messageText;
    TMP_Text persistentHintText;
    Coroutine activeRoutine;
    string persistentHintOwnerKey;
    string persistentHintMessage;

    public static void Show(string message, float seconds = 2.6f)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureInstance();
        if (instance != null)
            instance.ShowInternal(message, seconds);
    }

    public static void SetPersistentHint(string ownerKey, string message)
    {
        if (string.IsNullOrWhiteSpace(ownerKey) || string.IsNullOrWhiteSpace(message))
            return;

        EnsureInstance();
        if (instance != null)
            instance.SetPersistentHintInternal(ownerKey, message);
    }

    public static void ClearPersistentHint(string ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey) || instance == null)
            return;

        instance.ClearPersistentHintInternal(ownerKey);
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
        if (canvas != null && messageText != null && persistentHintText != null)
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

        Transform persistentExisting = transform.Find("RoundPersistentHintText");
        GameObject persistentObject = persistentExisting != null
            ? persistentExisting.gameObject
            : new GameObject("RoundPersistentHintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        persistentObject.transform.SetParent(transform, false);

        RectTransform persistentRect = persistentObject.GetComponent<RectTransform>();
        persistentRect.anchorMin = new Vector2(0.5f, 1f);
        persistentRect.anchorMax = new Vector2(0.5f, 1f);
        persistentRect.pivot = new Vector2(0.5f, 1f);
        persistentRect.anchoredPosition = new Vector2(0f, -154f);
        persistentRect.sizeDelta = new Vector2(1120f, 60f);

        persistentHintText = persistentObject.GetComponent<TMP_Text>();
        persistentHintText.fontSize = 30f;
        persistentHintText.fontStyle = FontStyles.Bold;
        persistentHintText.alignment = TextAlignmentOptions.Center;
        persistentHintText.color = new Color(0.7f, 0.92f, 1f, 0f);
        persistentHintText.raycastTarget = false;
        persistentHintText.text = string.Empty;

        ApplyPersistentHintVisual();
    }

    void ShowInternal(string message, float seconds)
    {
        EnsureUi();
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ShowRoutine(message, seconds));
    }

    void SetPersistentHintInternal(string ownerKey, string message)
    {
        EnsureUi();
        persistentHintOwnerKey = ownerKey;
        persistentHintMessage = message;
        ApplyPersistentHintVisual();
    }

    void ClearPersistentHintInternal(string ownerKey)
    {
        if (!string.Equals(persistentHintOwnerKey, ownerKey, System.StringComparison.Ordinal))
            return;

        persistentHintOwnerKey = null;
        persistentHintMessage = null;
        ApplyPersistentHintVisual();
    }

    void ApplyPersistentHintVisual()
    {
        if (persistentHintText == null)
            return;

        bool visible = !string.IsNullOrWhiteSpace(persistentHintMessage);
        persistentHintText.text = visible ? persistentHintMessage : string.Empty;
        persistentHintText.color = visible
            ? new Color(0.7f, 0.92f, 1f, 0.92f)
            : new Color(0.7f, 0.92f, 1f, 0f);
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
