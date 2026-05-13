using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoundStartCurtainUI : MonoBehaviour
{
    const string RootName = "RoundStartCurtain";
    const float MinHoldSeconds = 0.12f;
    const float MaxHoldSeconds = 4f;
    const float FadeSeconds = 0.22f;
    const int MinReadyFrames = 2;

    static RoundStartCurtainUI instance;

    CanvasGroup canvasGroup;
    Coroutine routine;

    public static void ShowForRoundStart()
    {
        RoundStartCurtainUI curtain = EnsureInstance();
        if (curtain != null)
            curtain.Show();
    }

    public static void HideImmediate()
    {
        if (instance == null)
            return;

        instance.HideNow();
    }

    static RoundStartCurtainUI EnsureInstance()
    {
        if (instance != null && instance.gameObject.scene.IsValid())
            return instance;

        GameObject existing = GameObject.Find(RootName);
        if (existing != null && existing.TryGetComponent(out RoundStartCurtainUI existingCurtain))
        {
            instance = existingCurtain;
            instance.EnsureVisuals();
            return instance;
        }

        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RoundStartCurtainUI));
        instance = root.GetComponent<RoundStartCurtainUI>();
        instance.EnsureVisuals();
        return instance;
    }

    void Awake()
    {
        instance = this;
        EnsureVisuals();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void EnsureVisuals()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        canvasGroup = GetComponent<CanvasGroup>();

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        Transform panelTransform = transform.Find("Panel");
        GameObject panelObject = panelTransform != null ? panelTransform.gameObject : new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panel = panelObject.GetComponent<Image>();
        panel.color = Color.black;
        panel.raycastTarget = true;
    }

    void Show()
    {
        EnsureVisuals();

        if (routine != null)
            StopCoroutine(routine);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        routine = StartCoroutine(HoldThenFade());
    }

    void HideNow()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    IEnumerator HoldThenFade()
    {
        float startTime = Time.unscaledTime;
        int readyFrames = 0;

        while (Time.unscaledTime - startTime < MaxHoldSeconds)
        {
            bool minHoldElapsed = Time.unscaledTime - startTime >= MinHoldSeconds;
            bool cameraReady = TrySnapLocalCameraToPlayer();

            if (minHoldElapsed && cameraReady)
            {
                readyFrames++;
                if (readyFrames >= MinReadyFrames)
                    break;
            }
            else
            {
                readyFrames = 0;
            }

            yield return null;
        }

        float fadeStart = Time.unscaledTime;
        while (Time.unscaledTime - fadeStart < FadeSeconds)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / FadeSeconds);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - t;
            yield return null;
        }

        HideNow();
    }

    bool TrySnapLocalCameraToPlayer()
    {
        CameraFollow cameraFollow = FindAnyObjectByType<CameraFollow>();
        if (cameraFollow == null)
            return false;

        Transform localPlayer = ResolveLocalPlayerTransform();
        if (localPlayer == null)
            return false;

        if (cameraFollow.target != localPlayer)
            cameraFollow.target = localPlayer;

        return cameraFollow.SnapToTarget();
    }

    Transform ResolveLocalPlayerTransform()
    {
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.TagObject is GameObject taggedObject && taggedObject != null && taggedObject.scene.IsValid())
            return taggedObject.transform;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null ||
                player.IsWreck ||
                player.IsBotControlled ||
                player.photonView == null ||
                !player.photonView.IsMine)
            {
                continue;
            }

            PhotonNetwork.LocalPlayer.TagObject = player.gameObject;
            return player.transform;
        }

        return null;
    }
}
