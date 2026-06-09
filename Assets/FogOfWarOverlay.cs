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
    const int FogGridColumns = 76;
    const int FogGridRows = 48;
    const float FogMeshRefreshInterval = 1f / 15f;
    const float SceneLookupRefreshInterval = 0.35f;
    const float HudCanvasRefreshInterval = 1f;
    const float FogTargetMoveThresholdSqr = 0.0025f;
    const float FogCameraMoveThresholdSqr = 0.0025f;
    const float FogRotationThresholdDegrees = 0.3f;
    const float FogOrthoSizeThreshold = 0.02f;
    static readonly Color32 SolidFogColor = new Color(0.045f, 0.052f, 0.07f, 0.97f);
    static readonly Color32 DeepFogColor = new Color(0.018f, 0.022f, 0.034f, 0.992f);
    static readonly Color32 ClearFogColor = new Color(0.18f, 0.24f, 0.32f, 0.005f);
    static readonly Color32 EdgeFogColor = new Color(0.08f, 0.16f, 0.28f, 0.68f);

    static FogOfWarOverlay instance;

    Canvas fogCanvas;
    FogOfWarGraphic fogGraphic;
    Camera cachedCamera;
    Transform cachedTarget;
    float nextCameraLookupTime;
    float nextTargetLookupTime;
    float nextFogMeshRefreshTime;
    float nextHudCanvasRefreshTime;
    bool lastFogActive;
    Camera lastFogCamera;
    Transform lastFogTarget;
    Vector3 lastFogCameraPosition;
    Vector3 lastFogTargetPosition;
    float lastFogCameraOrthographicSize;
    float lastFogTargetRotationZ;
    int lastFogScreenWidth;
    int lastFogScreenHeight;

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
        if (fogCanvas == null || fogGraphic == null)
            EnsureCanvas();

        Camera camera = ResolveCamera();
        bool active = IsFogActive() && camera != null;
        Transform target = active ? ResolveLocalPlayerTransform() : null;

        if (fogCanvas != null)
        {
            fogCanvas.enabled = active;
            fogCanvas.sortingOrder = FogCanvasSortingOrder;
        }

        if (fogGraphic != null)
        {
            bool forceRefresh = ShouldRefreshFogGraphic(active, camera, target);
            fogGraphic.SetContext(active ? camera : null, active ? target : null, active, forceRefresh);
        }

        if (active && Time.unscaledTime >= nextHudCanvasRefreshTime)
        {
            nextHudCanvasRefreshTime = Time.unscaledTime + HudCanvasRefreshInterval;
            KeepHudCanvasesAboveFog();
        }
    }

    Camera ResolveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        cachedCamera = null;
        if (Time.unscaledTime < nextCameraLookupTime)
            return null;

        nextCameraLookupTime = Time.unscaledTime + SceneLookupRefreshInterval;
        cachedCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        return cachedCamera;
    }

    bool ShouldRefreshFogGraphic(bool active, Camera camera, Transform target)
    {
        if (!active)
        {
            bool changed = lastFogActive;
            lastFogActive = false;
            lastFogCamera = null;
            lastFogTarget = null;
            return changed;
        }

        bool contextChanged = !lastFogActive || camera != lastFogCamera || target != lastFogTarget;
        if (contextChanged)
        {
            lastFogActive = true;
            nextFogMeshRefreshTime = Time.unscaledTime + FogMeshRefreshInterval;
            CaptureFogViewState(camera, target);
            return true;
        }

        if (Time.unscaledTime < nextFogMeshRefreshTime || !HasFogViewChanged(camera, target))
            return false;

        nextFogMeshRefreshTime = Time.unscaledTime + FogMeshRefreshInterval;
        CaptureFogViewState(camera, target);
        return true;
    }

    bool HasFogViewChanged(Camera camera, Transform target)
    {
        if (camera != lastFogCamera || target != lastFogTarget)
            return true;

        if (Screen.width != lastFogScreenWidth || Screen.height != lastFogScreenHeight)
            return true;

        if (camera != null)
        {
            if ((camera.transform.position - lastFogCameraPosition).sqrMagnitude > FogCameraMoveThresholdSqr)
                return true;

            if (Mathf.Abs(camera.orthographicSize - lastFogCameraOrthographicSize) > FogOrthoSizeThreshold)
                return true;
        }

        if (target != null)
        {
            if ((target.position - lastFogTargetPosition).sqrMagnitude > FogTargetMoveThresholdSqr)
                return true;

            if (Mathf.Abs(Mathf.DeltaAngle(target.eulerAngles.z, lastFogTargetRotationZ)) > FogRotationThresholdDegrees)
                return true;
        }

        return false;
    }

    void CaptureFogViewState(Camera camera, Transform target)
    {
        lastFogCamera = camera;
        lastFogTarget = target;
        lastFogCameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        lastFogCameraOrthographicSize = camera != null ? camera.orthographicSize : 0f;
        lastFogTargetPosition = target != null ? target.position : Vector3.zero;
        lastFogTargetRotationZ = target != null ? target.eulerAngles.z : 0f;
        lastFogScreenWidth = Screen.width;
        lastFogScreenHeight = Screen.height;
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

        if (RoomSettings.GetMapEffectMode(RoomSettings.FogOfWarModeKey) != RoomSettings.MapEffectModeUtcStart)
            return false;

        double nowUtcMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return RoomSettings.ShouldMapEffectActivate(RoomSettings.FogOfWarModeKey, RoomSettings.FogOfWarStartUtcMsKey, nowUtcMs);
    }

    Transform ResolveLocalPlayerTransform()
    {
        if (IsValidLocalTarget(cachedTarget))
            return cachedTarget;

        cachedTarget = null;
        if (Time.unscaledTime < nextTargetLookupTime)
            return null;

        nextTargetLookupTime = Time.unscaledTime + SceneLookupRefreshInterval;

        if (PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.TagObject is GameObject taggedObject &&
            taggedObject != null &&
            taggedObject.scene.IsValid())
        {
            cachedTarget = taggedObject.transform;
            if (IsValidLocalTarget(cachedTarget))
                return cachedTarget;

            cachedTarget = null;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (ActorIdentity.IsLocalHumanPlayerActor(player))
            {
                if (PhotonNetwork.LocalPlayer != null)
                    PhotonNetwork.LocalPlayer.TagObject = player.gameObject;

                cachedTarget = player.transform;
                return cachedTarget;
            }
        }

        return null;
    }

    bool IsValidLocalTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.scene.IsValid())
            return false;

        PlayerHealth health = candidate.GetComponent<PlayerHealth>();
        if (health == null)
            return true;

        return ActorIdentity.IsLocalHumanPlayerActor(health);
    }

    sealed class FogOfWarGraphic : MaskableGraphic
    {
        Camera targetCamera;
        Transform target;
        Transform rendererTarget;
        SpriteRenderer cachedTargetRenderer;
        bool fogActive;

        public void SetContext(Camera camera, Transform playerTarget, bool active, bool forceRefresh)
        {
            bool contextChanged = targetCamera != camera || target != playerTarget || fogActive != active;
            if (target != playerTarget)
            {
                rendererTarget = null;
                cachedTargetRenderer = null;
            }

            targetCamera = camera;
            target = playerTarget;
            fogActive = active;

            if (contextChanged || forceRefresh)
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
            float diagonal = GetCameraWorldDiagonal(targetCamera, target.position.z);
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

            if (rendererTarget != target)
            {
                rendererTarget = target;
                cachedTargetRenderer = target.GetComponent<SpriteRenderer>();
                if (cachedTargetRenderer == null)
                    cachedTargetRenderer = target.GetComponentInChildren<SpriteRenderer>();
            }

            if (cachedTargetRenderer == null)
                return MinShipRevealRadius;

            Vector3 extents = cachedTargetRenderer.bounds.extents;
            float boundsRadius = Mathf.Sqrt((extents.x * extents.x) + (extents.y * extents.y));
            return Mathf.Max(MinShipRevealRadius, boundsRadius + ShipRevealPadding);
        }

        static float GetCameraWorldDiagonal(Camera camera, float targetZ)
        {
            float depth = Mathf.Abs(camera.transform.position.z - targetZ);
            Vector2 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector2 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            return Vector2.Distance(bottomLeft, topRight);
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
