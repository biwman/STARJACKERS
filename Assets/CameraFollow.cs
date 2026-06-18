using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    const float ReferenceAspect = 16f / 9f;

    public Transform target;
    public float mapSizeX = 25f;
    public float mapSizeY = 25f;

    Camera cachedCamera;
    float camHalfWidth;
    float camHalfHeight;
    float baseOrthographicSize;
    float baseHorizontalHalfSize;
    float dynamicZoomMultiplier = 1f;
    Vector3 smoothedPosition;
    bool hasSmoothedPosition;

    void Awake()
    {
        CaptureBaseCameraSize();
        RefreshCameraExtents();
    }

    void Start()
    {
        CaptureBaseCameraSize();
        RefreshCameraExtents();
    }

    public void SetTargetAndSnap(Transform newTarget)
    {
        target = newTarget;
        SnapToTarget();
    }

    public bool SnapToTarget()
    {
        if (target == null)
            return false;

        RefreshCameraExtents();
        smoothedPosition = GetTargetCameraPosition();
        hasSmoothedPosition = true;
        transform.position = smoothedPosition;
        return true;
    }

    void RefreshCameraExtents()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cachedCamera = cam;
            camHalfHeight = cam.orthographicSize;
            camHalfWidth = camHalfHeight * GetCameraAspect(cam);
        }
        else if (cachedCamera != null)
        {
            camHalfHeight = cachedCamera.orthographicSize;
            camHalfWidth = camHalfHeight * GetCameraAspect(cachedCamera);
        }
    }

    void CaptureBaseCameraSize()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic && baseOrthographicSize <= 0f)
        {
            baseOrthographicSize = cam.orthographicSize;
            baseHorizontalHalfSize = baseOrthographicSize * ReferenceAspect;
        }
    }

    void LateUpdate()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        mapSizeX = mapSize.x;
        mapSizeY = mapSize.y;
        UpdateDynamicZoom();

        if (target == null)
            return;

        Vector3 targetPos = GetTargetCameraPosition();
        if (!hasSmoothedPosition)
        {
            smoothedPosition = targetPos;
            hasSmoothedPosition = true;
        }
        else
        {
            smoothedPosition = Vector3.Lerp(smoothedPosition, targetPos, Time.deltaTime * 5f);
        }

        transform.position = smoothedPosition + ScreenShakeController.GetOffset(smoothedPosition);
    }

    void UpdateDynamicZoom()
    {
        Camera cam = Camera.main != null ? Camera.main : cachedCamera;
        if (cam == null || !cam.orthographic)
            return;

        if (baseOrthographicSize <= 0f)
        {
            baseOrthographicSize = cam.orthographicSize / Mathf.Max(0.001f, dynamicZoomMultiplier);
            baseHorizontalHalfSize = baseOrthographicSize * ReferenceAspect;
        }

        Vector3 cameraBasePosition = hasSmoothedPosition ? smoothedPosition : transform.position;
        DynamicCameraZoomState zoomState = DynamicCameraZoomController.GetState(cameraBasePosition);
        float targetMultiplier = Mathf.Max(1f, zoomState.Multiplier);
        float speed = targetMultiplier > dynamicZoomMultiplier ? zoomState.ZoomInSpeed : zoomState.ZoomOutSpeed;
        float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, speed) * deltaTime);
        dynamicZoomMultiplier = Mathf.Lerp(dynamicZoomMultiplier, targetMultiplier, blend);
        if (Mathf.Abs(dynamicZoomMultiplier - 1f) < 0.001f && Mathf.Approximately(targetMultiplier, 1f))
            dynamicZoomMultiplier = 1f;

        float aspect = GetCameraAspect(cam);
        float verticalFitSize = baseOrthographicSize;
        float horizontalFitSize = baseHorizontalHalfSize / Mathf.Max(0.01f, aspect);
        cam.orthographicSize = Mathf.Max(verticalFitSize, horizontalFitSize) * dynamicZoomMultiplier;
        RefreshCameraExtents();
    }

    static float GetCameraAspect(Camera cam)
    {
        if (cam != null && cam.aspect > 0.01f)
            return cam.aspect;

        return Screen.height > 0 ? (float)Screen.width / Screen.height : ReferenceAspect;
    }

    void OnDisable()
    {
        RestoreBaseCameraSize();
    }

    void OnDestroy()
    {
        RestoreBaseCameraSize();
    }

    void RestoreBaseCameraSize()
    {
        Camera cam = cachedCamera != null ? cachedCamera : Camera.main;
        if (cam != null && cam.orthographic && baseOrthographicSize > 0f)
            cam.orthographicSize = baseOrthographicSize;
    }

    Vector3 GetTargetCameraPosition()
    {
        Vector2 center = Vector2.zero;
        Vector2 size = new Vector2(mapSizeX, mapSizeY);
        if (target != null && MapInstanceService.TryGetBoundsForWorldPosition(target.position, out MapInstanceService.BoundsInfo bounds))
        {
            center = bounds.Center;
            size = bounds.Size;
        }

        float minX = center.x - size.x / 2f + camHalfWidth;
        float maxX = center.x + size.x / 2f - camHalfWidth;
        float minY = center.y - size.y / 2f + camHalfHeight;
        float maxY = center.y + size.y / 2f - camHalfHeight;

        float clampedX = minX <= maxX ? Mathf.Clamp(target.position.x, minX, maxX) : center.x;
        float clampedY = minY <= maxY ? Mathf.Clamp(target.position.y, minY, maxY) : center.y;

        return new Vector3(clampedX, clampedY, -10f);
    }
}
