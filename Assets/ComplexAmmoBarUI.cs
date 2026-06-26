using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerShooting))]
public sealed class ComplexAmmoBarUI : MonoBehaviourPun
{
    const string RootName = "ComplexAmmoBar";
    const float SegmentWidth = 24f;
    const float SegmentHeight = 7f;
    const float SegmentGap = 4f;
    const float ReloadProgressUiStep = 0.01f;
    static readonly Color LoadedFillColor = new Color(0.24f, 0.92f, 0.28f, 1f);
    static readonly Color ChargingFillColor = new Color(0.42f, 1f, 0.38f, 1f);
    static readonly Color EmptyBackgroundColor = new Color(0.03f, 0.05f, 0.08f, 0.82f);
    static readonly Color ChargingBackgroundColor = new Color(0.05f, 0.18f, 0.09f, 0.9f);
    static readonly Color UnloadedBackgroundColor = new Color(0.9f, 0.45f, 0.08f, 0.88f);

    sealed class Segment
    {
        public GameObject Root;
        public Image Background;
        public Image Fill;
        public RectTransform FillRect;
    }

    PlayerShooting shooting;
    RectTransform canvasRect;
    RectTransform rootRect;
    GameObject rootObject;
    Camera cachedCamera;
    readonly List<Segment> segments = new List<Segment>();
    int lastSegmentCount = -1;
    int lastDisplayedAmmo = int.MinValue;
    int lastReloadProgressStep = int.MinValue;
    bool forceSegmentRefresh;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        EnsureRoot();
    }

    void Update()
    {
        EnsureRoot();
        bool visible = RefreshVisibility();
        if (!visible)
            return;

        RefreshLayout();
        RefreshSegments();
    }

    void OnDestroy()
    {
        if (rootObject != null)
            Destroy(rootObject);
    }

    void EnsureRoot()
    {
        if (rootObject != null && rootRect != null && canvasRect != null)
            return;

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
            return;

        canvasRect = canvasObject.GetComponent<RectTransform>();
        if (canvasRect == null)
            return;

        rootObject = new GameObject(RootName, typeof(RectTransform));
        rootObject.transform.SetParent(canvasObject.transform, false);
        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
    }

    bool RefreshVisibility()
    {
        if (rootObject == null)
            return false;

        bool visible = shooting != null && shooting.IsComplexShootingActive && IsGameplayHudVisible();
        if (rootObject.activeSelf != visible)
        {
            rootObject.SetActive(visible);
            forceSegmentRefresh = true;
        }

        return visible;
    }

    void RefreshLayout()
    {
        Camera camera = ResolveCamera();
        if (rootRect == null || canvasRect == null || camera == null)
            return;

        Vector3 worldPosition = transform.position + Vector3.down * 0.72f;
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 canvasPosition))
            rootRect.anchoredPosition = canvasPosition;
    }

    Camera ResolveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        cachedCamera = Camera.main;
        return cachedCamera;
    }

    void RefreshSegments()
    {
        if (shooting == null || rootRect == null)
            return;

        int maxAmmo = Mathf.Max(0, shooting.MaxAmmo);
        if (maxAmmo != lastSegmentCount)
        {
            RebuildSegments(maxAmmo);
            forceSegmentRefresh = true;
        }

        int currentAmmo = Mathf.Clamp(shooting.CurrentAmmo, 0, maxAmmo);
        float reloadProgress = shooting.ComplexAmmoReloadProgress;
        int reloadProgressStep = Mathf.RoundToInt(Mathf.Clamp01(reloadProgress) / ReloadProgressUiStep);
        if (!forceSegmentRefresh &&
            currentAmmo == lastDisplayedAmmo &&
            reloadProgressStep == lastReloadProgressStep)
        {
            return;
        }

        lastDisplayedAmmo = currentAmmo;
        lastReloadProgressStep = reloadProgressStep;
        forceSegmentRefresh = false;

        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            if (segment == null || segment.Fill == null || segment.Background == null)
                continue;

            bool loaded = i < currentAmmo;
            bool charging = !loaded && i == currentAmmo && reloadProgress > 0f;
            float fill = Mathf.Clamp01(loaded ? 1f : charging ? reloadProgress : 0f);
            SetFillWidth(segment, fill);

            if (loaded)
            {
                segment.Fill.color = LoadedFillColor;
                segment.Background.color = EmptyBackgroundColor;
            }
            else if (charging)
            {
                float pulse = Mathf.Sin(Time.time * 9f) * 0.5f + 0.5f;
                segment.Fill.color = Color.Lerp(LoadedFillColor, ChargingFillColor, pulse);
                segment.Background.color = Color.Lerp(EmptyBackgroundColor, ChargingBackgroundColor, 0.55f + pulse * 0.35f);
            }
            else
            {
                segment.Fill.color = LoadedFillColor;
                segment.Background.color = UnloadedBackgroundColor;
            }
        }
    }

    void RebuildSegments(int count)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i]?.Root != null)
                Destroy(segments[i].Root);
        }

        segments.Clear();
        lastSegmentCount = count;

        float width = Mathf.Max(1, count) * SegmentWidth + Mathf.Max(0, count - 1) * SegmentGap;
        rootRect.sizeDelta = new Vector2(width, SegmentHeight);

        for (int i = 0; i < count; i++)
        {
            GameObject segmentObject = new GameObject("ComplexAmmoSegment_" + i, typeof(RectTransform), typeof(Image));
            segmentObject.transform.SetParent(rootObject.transform, false);

            RectTransform rect = segmentObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(SegmentWidth, SegmentHeight);
            rect.anchoredPosition = new Vector2(i * (SegmentWidth + SegmentGap), 0f);

            Image background = segmentObject.GetComponent<Image>();
            background.color = new Color(0.03f, 0.05f, 0.08f, 0.82f);
            background.type = Image.Type.Sliced;

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(segmentObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = new Vector2(0f, 0f);
            fillRect.sizeDelta = new Vector2(0f, 0f);

            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Simple;
            fill.raycastTarget = false;

            segments.Add(new Segment { Root = segmentObject, Background = background, Fill = fill, FillRect = fillRect });
        }
    }

    void SetFillWidth(Segment segment, float fill)
    {
        if (segment?.FillRect == null)
            return;

        segment.FillRect.sizeDelta = new Vector2(SegmentWidth * fill, 0f);
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               GameplayHudVisibility.IsGameplayHudVisible(started);
    }
}
