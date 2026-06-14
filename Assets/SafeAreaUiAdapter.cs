using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SafeAreaUiAdapter : MonoBehaviour
{
    const float EdgeThreshold = 0.001f;

    static SafeAreaUiAdapter instance;

    readonly Dictionary<RectTransform, AppliedPadding> appliedPaddingByRect = new Dictionary<RectTransform, AppliedPadding>();
    readonly List<RectTransform> staleRects = new List<RectTransform>();

    Rect lastSafeArea;
    int lastScreenWidth;
    int lastScreenHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null || !Application.isPlaying)
            return;

        GameObject root = new GameObject("SafeAreaUiAdapter");
        instance = root.AddComponent<SafeAreaUiAdapter>();
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
    }

    void LateUpdate()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;
        bool unchanged = lastScreenWidth == Screen.width &&
                         lastScreenHeight == Screen.height &&
                         Approximately(lastSafeArea, safeArea);

        if (unchanged && appliedPaddingByRect.Count == 0)
            return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = safeArea;

        ApplyToOverlayCanvasRoots(safeArea);
        RemoveStaleEntries();
    }

    static Vector4 GetCanvasSpacePadding(Rect safeArea, CanvasScaler scaler)
    {
        Vector2 referenceResolution = scaler != null ? scaler.referenceResolution : new Vector2(Screen.width, Screen.height);
        float widthScale = Mathf.Max(1f, referenceResolution.x) / Mathf.Max(1f, Screen.width);
        float heightScale = Mathf.Max(1f, referenceResolution.y) / Mathf.Max(1f, Screen.height);

        float left = Mathf.Max(0f, safeArea.xMin) * widthScale;
        float right = Mathf.Max(0f, Screen.width - safeArea.xMax) * widthScale;
        float bottom = Mathf.Max(0f, safeArea.yMin) * heightScale;
        float top = Mathf.Max(0f, Screen.height - safeArea.yMax) * heightScale;

        return new Vector4(left, bottom, right, top);
    }

    void ApplyToOverlayCanvasRoots(Rect safeArea)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                continue;

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
                continue;

            Vector4 padding = GetCanvasSpacePadding(safeArea, scaler);
            for (int childIndex = 0; childIndex < canvasRect.childCount; childIndex++)
            {
                RectTransform child = canvasRect.GetChild(childIndex) as RectTransform;
                if (child != null)
                    ApplyPadding(child, padding);
            }
        }
    }

    void ApplyPadding(RectTransform rect, Vector4 padding)
    {
        AppliedPadding previous;
        if (appliedPaddingByRect.TryGetValue(rect, out previous))
            RemovePadding(rect, previous);

        AppliedPadding next = CalculatePadding(rect, padding);
        AddPadding(rect, next);
        appliedPaddingByRect[rect] = next;
    }

    static AppliedPadding CalculatePadding(RectTransform rect, Vector4 padding)
    {
        AppliedPadding result = default;

        if (IsLeftAnchored(rect))
            result.AnchoredPosition.x += padding.x;
        else if (IsRightAnchored(rect))
            result.AnchoredPosition.x -= padding.z;
        else if (IsHorizontallyStretched(rect))
        {
            result.OffsetMin.x += padding.x;
            result.OffsetMax.x -= padding.z;
        }

        if (IsBottomAnchored(rect))
            result.AnchoredPosition.y += padding.y;
        else if (IsTopAnchored(rect))
            result.AnchoredPosition.y -= padding.w;
        else if (IsVerticallyStretched(rect))
        {
            result.OffsetMin.y += padding.y;
            result.OffsetMax.y -= padding.w;
        }

        return result;
    }

    static void AddPadding(RectTransform rect, AppliedPadding padding)
    {
        rect.anchoredPosition += padding.AnchoredPosition;
        rect.offsetMin += padding.OffsetMin;
        rect.offsetMax += padding.OffsetMax;
    }

    static void RemovePadding(RectTransform rect, AppliedPadding padding)
    {
        rect.anchoredPosition -= padding.AnchoredPosition;
        rect.offsetMin -= padding.OffsetMin;
        rect.offsetMax -= padding.OffsetMax;
    }

    void RemoveStaleEntries()
    {
        staleRects.Clear();
        foreach (KeyValuePair<RectTransform, AppliedPadding> entry in appliedPaddingByRect)
        {
            if (entry.Key == null)
                staleRects.Add(entry.Key);
        }

        for (int i = 0; i < staleRects.Count; i++)
            appliedPaddingByRect.Remove(staleRects[i]);
    }

    static bool IsLeftAnchored(RectTransform rect)
    {
        return rect.anchorMin.x <= EdgeThreshold && rect.anchorMax.x <= EdgeThreshold;
    }

    static bool IsRightAnchored(RectTransform rect)
    {
        return rect.anchorMin.x >= 1f - EdgeThreshold && rect.anchorMax.x >= 1f - EdgeThreshold;
    }

    static bool IsHorizontallyStretched(RectTransform rect)
    {
        return rect.anchorMin.x <= EdgeThreshold && rect.anchorMax.x >= 1f - EdgeThreshold;
    }

    static bool IsBottomAnchored(RectTransform rect)
    {
        return rect.anchorMin.y <= EdgeThreshold && rect.anchorMax.y <= EdgeThreshold;
    }

    static bool IsTopAnchored(RectTransform rect)
    {
        return rect.anchorMin.y >= 1f - EdgeThreshold && rect.anchorMax.y >= 1f - EdgeThreshold;
    }

    static bool IsVerticallyStretched(RectTransform rect)
    {
        return rect.anchorMin.y <= EdgeThreshold && rect.anchorMax.y >= 1f - EdgeThreshold;
    }

    static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.width, b.width) &&
               Mathf.Approximately(a.height, b.height);
    }

    struct AppliedPadding
    {
        public Vector2 AnchoredPosition;
        public Vector2 OffsetMin;
        public Vector2 OffsetMax;
    }
}
