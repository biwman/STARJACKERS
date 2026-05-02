using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public float deadZone = 0.15f;
    public RectTransform background;
    public RectTransform handle;

    public Vector2 inputVector;
    public Vector2 rawInputVector { get; private set; }
    public bool IsPressed { get; private set; }
    public bool IsExternalControlActive { get; private set; }
    public Vector2 DefaultAnchoredPosition
    {
        get
        {
            CaptureDefaultBackgroundPosition();
            return defaultBackgroundAnchoredPosition;
        }
    }

    Vector2 defaultBackgroundAnchoredPosition;
    Vector3 defaultBackgroundWorldPosition;
    bool defaultPositionCaptured;

    void Awake()
    {
        CaptureDefaultBackgroundPosition();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        IsExternalControlActive = false;
        ApplyPointer(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsPressed)
            return;

        ApplyPointer(eventData.position, eventData.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetJoystick();
    }

    void OnDisable()
    {
        ResetJoystick();
    }

    public void BeginExternalControl(Vector2 screenPoint, Camera eventCamera, bool relocateBackground)
    {
        CaptureDefaultBackgroundPosition();

        if (relocateBackground)
            SetBackgroundScreenPosition(screenPoint, eventCamera);

        IsPressed = true;
        IsExternalControlActive = true;
        ApplyPointer(screenPoint, eventCamera);
    }

    public void UpdateExternalControl(Vector2 screenPoint, Camera eventCamera)
    {
        if (!IsPressed)
            return;

        IsExternalControlActive = true;
        ApplyPointer(screenPoint, eventCamera);
    }

    public void EndExternalControl(bool restoreDefaultPosition)
    {
        if (restoreDefaultPosition)
            RestoreDefaultBackgroundPosition();

        ResetJoystick();
    }

    public void RestoreDefaultBackgroundPosition()
    {
        CaptureDefaultBackgroundPosition();
        if (background != null)
        {
            background.anchoredPosition = defaultBackgroundAnchoredPosition;
            background.position = defaultBackgroundWorldPosition;
        }
    }

    void ResetJoystick()
    {
        IsPressed = false;
        IsExternalControlActive = false;
        inputVector = Vector2.zero;
        rawInputVector = Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }
    }

    void ApplyPointer(Vector2 screenPoint, Camera eventCamera)
    {
        if (background == null)
            return;

        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            screenPoint,
            eventCamera,
            out pos);

        if (pos.magnitude > background.sizeDelta.x / 2f)
            pos = pos.normalized * (background.sizeDelta.x / 2f);

        pos.x = pos.x / (background.sizeDelta.x / 2f);
        pos.y = pos.y / (background.sizeDelta.y / 2f);

        rawInputVector = new Vector2(pos.x, pos.y);
        inputVector = rawInputVector.magnitude > 1.0f ? rawInputVector.normalized : rawInputVector;
        if (inputVector.magnitude < deadZone)
            inputVector = Vector2.zero;

        if (handle == null)
            return;

        float radius = (background.sizeDelta.x - handle.sizeDelta.x) / 2f;
        handle.anchoredPosition = new Vector2(inputVector.x * radius, inputVector.y * radius);
    }

    void CaptureDefaultBackgroundPosition()
    {
        if (defaultPositionCaptured || background == null)
            return;

        defaultBackgroundAnchoredPosition = background.anchoredPosition;
        defaultBackgroundWorldPosition = background.position;
        defaultPositionCaptured = true;
    }

    void SetBackgroundScreenPosition(Vector2 screenPoint, Camera eventCamera)
    {
        if (background == null)
            return;

        Vector3 worldPoint;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(background, screenPoint, eventCamera, out worldPoint))
            return;

        background.position = worldPoint;
    }
}
