using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float mapSizeX = 25f;
    public float mapSizeY = 25f;

    Camera cachedCamera;
    float camHalfWidth;
    float camHalfHeight;

    void Awake()
    {
        RefreshCameraExtents();
    }

    void Start()
    {
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
        transform.position = GetTargetCameraPosition();
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

    void LateUpdate()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        mapSizeX = mapSize.x;
        mapSizeY = mapSize.y;

        if (target == null)
            return;

        Vector3 targetPos = GetTargetCameraPosition();
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);
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
