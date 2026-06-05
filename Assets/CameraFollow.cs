using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float mapSizeX = 25f;
    public float mapSizeY = 25f;

    Camera cachedCamera;
    float camHalfWidth;
    float camHalfHeight;
    float baseOrthographicSize;
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
            camHalfWidth = camHalfHeight * Screen.width / Screen.height;
        }
        else if (cachedCamera != null)
        {
            camHalfHeight = cachedCamera.orthographicSize;
            camHalfWidth = camHalfHeight * Screen.width / Screen.height;
        }
    }

    void CaptureBaseCameraSize()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic && baseOrthographicSize <= 0f)
            baseOrthographicSize = cam.orthographicSize;
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
            baseOrthographicSize = cam.orthographicSize / Mathf.Max(0.001f, dynamicZoomMultiplier);

        Vector3 cameraBasePosition = hasSmoothedPosition ? smoothedPosition : transform.position;
        DynamicCameraZoomState zoomState = DynamicCameraZoomController.GetState(cameraBasePosition);
        float targetMultiplier = Mathf.Max(1f, zoomState.Multiplier);
        float speed = targetMultiplier > dynamicZoomMultiplier ? zoomState.ZoomInSpeed : zoomState.ZoomOutSpeed;
        float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, speed) * deltaTime);
        dynamicZoomMultiplier = Mathf.Lerp(dynamicZoomMultiplier, targetMultiplier, blend);
        if (Mathf.Abs(dynamicZoomMultiplier - 1f) < 0.001f && Mathf.Approximately(targetMultiplier, 1f))
            dynamicZoomMultiplier = 1f;

        cam.orthographicSize = baseOrthographicSize * dynamicZoomMultiplier;
        RefreshCameraExtents();
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
        float minX = -mapSizeX / 2f + camHalfWidth;
        float maxX = mapSizeX / 2f - camHalfWidth;
        float minY = -mapSizeY / 2f + camHalfHeight;
        float maxY = mapSizeY / 2f - camHalfHeight;

        float clampedX = minX <= maxX ? Mathf.Clamp(target.position.x, minX, maxX) : 0f;
        float clampedY = minY <= maxY ? Mathf.Clamp(target.position.y, minY, maxY) : 0f;

        return new Vector3(clampedX, clampedY, -10f);
    }
}
