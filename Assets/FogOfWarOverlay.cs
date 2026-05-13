using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class FogOfWarOverlay : MonoBehaviour
{
    const string GameStartedKey = "gameStarted";
    const string FogCanvasName = "FogCanvas";
    const string FogGraphicName = "FogGraphic";
    const int FogCanvasSortingOrder = -1000;
    const float MinShipRevealRadius = 0.95f;
    const float ShipRevealPadding = 0.46f;
    const float ConeStartRadiusFactor = 0.58f;
    const float ConeNearWidthFactor = 0.18f;
    const float ConeHalfAngleDegrees = 18f;
    const float MinViewLength = 9f;
    const float ViewLengthMultiplier = 0.82f;
    const float FeatherWidth = 1.2f;
    const int FogGridColumns = 136;
    const int FogGridRows = 84;
    static readonly Color32 SolidFogColor = new Color(0.045f, 0.052f, 0.07f, 0.97f);
    static readonly Color32 DeepFogColor = new Color(0.018f, 0.022f, 0.034f, 0.992f);
    static readonly Color32 ClearFogColor = new Color(0.18f, 0.24f, 0.32f, 0.005f);
    static readonly Color32 EdgeFogColor = new Color(0.08f, 0.16f, 0.28f, 0.68f);

    static FogOfWarOverlay instance;

    Canvas fogCanvas;
    FogOfWarGraphic fogGraphic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
        {
            instance.EnsureCanvas();
            return;
        }

        GameObject existing = GameObject.Find("FogOfWarOverlay");
        if (existing != null && existing.TryGetComponent(out FogOfWarOverlay overlay))
        {
            instance = overlay;
            overlay.EnsureCanvas();
            return;
        }

        GameObject overlayObject = new GameObject("FogOfWarOverlay");
        overlayObject.AddComponent<FogOfWarOverlay>();
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
        EnsureCanvas();
    }

    void Update()
    {
        EnsureCanvas();

        Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        bool active = IsFogActive() && camera != null;
        Transform target = active ? ResolveLocalPlayerTransform() : null;

        if (fogCanvas != null)
        {
            fogCanvas.enabled = active;
            fogCanvas.sortingOrder = FogCanvasSortingOrder;
        }

        if (fogGraphic != null)
            fogGraphic.SetContext(active ? camera : null, active ? target : null, active);

        if (active)
            KeepHudCanvasesAboveFog();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void EnsureCanvas()
    {
        Canvas legacyCanvas = gameObject.GetComponent<Canvas>();
        if (legacyCanvas != null && legacyCanvas != fogCanvas)
            legacyCanvas.enabled = false;

        Transform canvasTransform = transform.Find(FogCanvasName);
        GameObject canvasObject = canvasTransform != null ? canvasTransform.gameObject : new GameObject(FogCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one;
        }

        fogCanvas = canvasObject.GetComponent<Canvas>();
        if (fogCanvas == null)
            fogCanvas = canvasObject.AddComponent<Canvas>();

        fogCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fogCanvas.overrideSorting = true;
        fogCanvas.sortingOrder = FogCanvasSortingOrder;
        fogCanvas.pixelPerfect = false;
        fogCanvas.enabled = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        Transform existingGraphic = canvasObject.transform.Find(FogGraphicName);
        GameObject graphicObject = existingGraphic != null ? existingGraphic.gameObject : new GameObject(FogGraphicName, typeof(RectTransform), typeof(CanvasRenderer));
        graphicObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = graphicObject.GetComponent<RectTransform>();
        if (rect == null)
            rect = graphicObject.AddComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        fogGraphic = graphicObject.GetComponent<FogOfWarGraphic>();
        if (fogGraphic == null)
            fogGraphic = graphicObject.AddComponent<FogOfWarGraphic>();

        fogGraphic.raycastTarget = false;
        fogGraphic.color = Color.white;
    }

    void KeepHudCanvasesAboveFog()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas == fogCanvas || !canvas.gameObject.scene.IsValid())
                continue;

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            if (canvas.sortingOrder > FogCanvasSortingOrder)
                continue;

            canvas.overrideSorting = true;
            canvas.sortingOrder = FogCanvasSortingOrder + 100;
        }
    }

    bool IsFogActive()
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GameStartedKey, out object value) ||
            value is not bool started ||
            !started)
        {
            return false;
        }

        if (RoomSettings.IsFogOfWarActive())
            return true;

        double nowUtcMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return RoomSettings.ShouldMapEffectActivate(RoomSettings.FogOfWarModeKey, RoomSettings.FogOfWarStartUtcMsKey, nowUtcMs);
    }

    Transform ResolveLocalPlayerTransform()
    {
        if (PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.TagObject is GameObject taggedObject &&
            taggedObject != null &&
            taggedObject.scene.IsValid())
        {
            PlayerHealth taggedHealth = taggedObject.GetComponent<PlayerHealth>();
            if (taggedHealth == null || (!taggedHealth.IsWreck && !taggedHealth.IsBotControlled))
                return taggedObject.transform;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player != null &&
                !player.IsWreck &&
                !player.IsBotControlled &&
                player.photonView != null &&
                player.photonView.IsMine)
            {
                if (PhotonNetwork.LocalPlayer != null)
                    PhotonNetwork.LocalPlayer.TagObject = player.gameObject;
                return player.transform;
            }
        }

        return null;
    }

    sealed class FogOfWarGraphic : MaskableGraphic
    {
        Camera targetCamera;
        Transform target;
        bool fogActive;

        public void SetContext(Camera camera, Transform playerTarget, bool active)
        {
            targetCamera = camera;
            target = playerTarget;
            fogActive = active;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            RectTransform rect = transform as RectTransform;
            if (!fogActive || rect == null)
                return;

            if (targetCamera == null || target == null)
            {
                AddFullScreenFog(vh, rect);
                return;
            }

            AddVisibilityGrid(vh, rect);
        }

        void AddFullScreenFog(VertexHelper vh, RectTransform rect)
        {
            Rect bounds = rect.rect;
            AddLocalQuad(vh, bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax, DeepFogColor, SolidFogColor, SolidFogColor, DeepFogColor);
        }

        void AddVisibilityGrid(VertexHelper vh, RectTransform rect)
        {
            Rect bounds = rect.rect;
            if (bounds.width <= 1f || bounds.height <= 1f)
                return;

            Vector2 forward = target.up;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector2.up;
            forward.Normalize();

            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 center = target.position;
            Vector2[] cameraCorners = GetCameraWorldCorners(targetCamera, target.position.z);
            float diagonal = Vector2.Distance(cameraCorners[0], cameraCorners[2]);
            float revealLength = Mathf.Max(MinViewLength, diagonal * ViewLengthMultiplier);
            float shipRevealRadius = GetShipRevealRadius();
            float coneStart = shipRevealRadius * ConeStartRadiusFactor;
            float coneNearHalfWidth = Mathf.Max(0.14f, shipRevealRadius * ConeNearWidthFactor);
            float coneSlope = Mathf.Tan(ConeHalfAngleDegrees * Mathf.Deg2Rad);
            float depth = Mathf.Abs(targetCamera.transform.position.z - target.position.z);
            int firstVertex = vh.currentVertCount;

            for (int row = 0; row <= FogGridRows; row++)
            {
                float v = row / (float)FogGridRows;
                float localY = Mathf.Lerp(bounds.yMin, bounds.yMax, v);
                float screenY = v * Screen.height;

                for (int column = 0; column <= FogGridColumns; column++)
                {
                    float u = column / (float)FogGridColumns;
                    float localX = Mathf.Lerp(bounds.xMin, bounds.xMax, u);
                    float screenX = u * Screen.width;
                    Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenX, screenY, depth));
                    Color32 fogColor = ResolveFogColor(world, center, right, forward, shipRevealRadius, coneStart, coneNearHalfWidth, coneSlope, revealLength, u, v);
                    vh.AddVert(new Vector3(localX, localY, 0f), fogColor, Vector2.zero);
                }
            }

            int stride = FogGridColumns + 1;
            for (int row = 0; row < FogGridRows; row++)
            {
                for (int column = 0; column < FogGridColumns; column++)
                {
                    int index = firstVertex + row * stride + column;
                    vh.AddTriangle(index, index + stride, index + stride + 1);
                    vh.AddTriangle(index, index + stride + 1, index + 1);
                }
            }
        }

        Color32 ResolveFogColor(Vector2 world, Vector2 center, Vector2 right, Vector2 forward, float shipRevealRadius, float coneStart, float coneNearHalfWidth, float coneSlope, float revealLength, float screenU, float screenV)
        {
            Vector2 rel = world - center;
            float localX = Vector2.Dot(rel, right);
            float localY = Vector2.Dot(rel, forward);
            float absX = Mathf.Abs(localX);

            float bodyDistance = rel.magnitude - shipRevealRadius;
            float bodyFog = Mathf.SmoothStep(0f, FeatherWidth * 0.72f, bodyDistance);

            float coneY = localY - coneStart;
            float coneHalfWidth = coneNearHalfWidth + Mathf.Max(0f, coneY) * coneSlope;
            float sideDistance = absX - coneHalfWidth;
            float backDistance = -coneY;
            float endDistance = coneY - revealLength;
            float coneDistance = Mathf.Max(sideDistance, Mathf.Max(backDistance, endDistance));
            float coneFog = Mathf.SmoothStep(0f, FeatherWidth, coneDistance);

            float fogAmount = Mathf.Min(bodyFog, coneFog);

            float dx = Mathf.Abs(screenU - 0.5f) * 2f;
            float dy = Mathf.Abs(screenV - 0.5f) * 2f;
            float vignette = Mathf.Clamp01((dx * dx + dy * dy) * 0.24f);
            fogAmount = Mathf.Clamp01(fogAmount + vignette * fogAmount * 0.2f);

            Color color = Color.Lerp(ClearFogColor, SolidFogColor, fogAmount);
            float edge = 1f - Mathf.Abs((fogAmount * 2f) - 1f);
            color = Color.Lerp(color, EdgeFogColor, edge * 0.22f);
            color = Color.Lerp(color, DeepFogColor, fogAmount * fogAmount * 0.42f);
            return color;
        }

        float GetShipRevealRadius()
        {
            if (target == null)
                return MinShipRevealRadius;

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = target.GetComponentInChildren<SpriteRenderer>();

            if (renderer == null)
                return MinShipRevealRadius;

            Vector3 extents = renderer.bounds.extents;
            float boundsRadius = Mathf.Sqrt((extents.x * extents.x) + (extents.y * extents.y));
            return Mathf.Max(MinShipRevealRadius, boundsRadius + ShipRevealPadding);
        }

        static Vector2[] GetCameraWorldCorners(Camera camera, float targetZ)
        {
            float depth = Mathf.Abs(camera.transform.position.z - targetZ);
            return new[]
            {
                (Vector2)camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth)),
                (Vector2)camera.ViewportToWorldPoint(new Vector3(1f, 0f, depth)),
                (Vector2)camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth)),
                (Vector2)camera.ViewportToWorldPoint(new Vector3(0f, 1f, depth))
            };
        }

        void AddLocalQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color32 bottomLeft, Color32 bottomRight, Color32 topRight, Color32 topLeft)
        {
            int index = vh.currentVertCount;
            vh.AddVert(new Vector3(xMin, yMin, 0f), bottomLeft, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yMin, 0f), bottomRight, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yMax, 0f), topRight, Vector2.zero);
            vh.AddVert(new Vector3(xMin, yMax, 0f), topLeft, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
