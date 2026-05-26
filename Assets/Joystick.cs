using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public float deadZone = 0.15f;
    public bool rescaleInputAfterDeadZone;
    public float responseExponent = 1f;
    public bool recenterOnPointerDown;
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
    Vector2 defaultBackgroundAnchorMin;
    Vector2 defaultBackgroundAnchorMax;
    Vector2 defaultBackgroundPivot;
    bool defaultPositionCaptured;

    void Awake()
    {
        CaptureDefaultBackgroundPosition();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        IsExternalControlActive = recenterOnPointerDown;

        if (recenterOnPointerDown)
            SetBackgroundScreenPosition(eventData.position, eventData.pressEventCamera);

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
        if (IsExternalControlActive)
        {
            EndExternalControl(true);
            return;
        }

        ResetJoystick();
    }

    void OnDisable()
    {
        if (IsExternalControlActive)
            EndExternalControl(true);
        else
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
        ResetJoystick();

        if (restoreDefaultPosition)
            RestoreDefaultBackgroundPosition();
    }

    public void RestoreDefaultBackgroundPosition()
    {
        CaptureDefaultBackgroundPosition();
        if (background != null)
        {
            background.anchorMin = defaultBackgroundAnchorMin;
            background.anchorMax = defaultBackgroundAnchorMax;
            background.pivot = defaultBackgroundPivot;
            background.anchoredPosition = defaultBackgroundAnchoredPosition;
        }
    }

    public bool IsAtDefaultBackgroundPosition(float tolerance = 0.5f)
    {
        CaptureDefaultBackgroundPosition();
        if (background == null)
            return true;

        return Vector2.Distance(background.anchoredPosition, defaultBackgroundAnchoredPosition) <= tolerance &&
               Vector2.Distance(background.anchorMin, defaultBackgroundAnchorMin) <= 0.001f &&
               Vector2.Distance(background.anchorMax, defaultBackgroundAnchorMax) <= 0.001f;
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
        float inputMagnitude = inputVector.magnitude;
        if (inputMagnitude < deadZone)
        {
            inputVector = Vector2.zero;
        }
        else if (rescaleInputAfterDeadZone)
        {
            float liveZone = Mathf.Max(0.001f, 1f - deadZone);
            float remappedMagnitude = Mathf.Clamp01((inputMagnitude - deadZone) / liveZone);
            if (!Mathf.Approximately(responseExponent, 1f))
                remappedMagnitude = Mathf.Pow(remappedMagnitude, Mathf.Max(0.1f, responseExponent));

            inputVector = inputVector.normalized * remappedMagnitude;
        }

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
        defaultBackgroundAnchorMin = background.anchorMin;
        defaultBackgroundAnchorMax = background.anchorMax;
        defaultBackgroundPivot = background.pivot;
        defaultPositionCaptured = true;
    }

    void SetBackgroundScreenPosition(Vector2 screenPoint, Camera eventCamera)
    {
        if (background == null)
            return;

        RectTransform parentRect = background.parent as RectTransform;
        if (parentRect == null)
            return;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, eventCamera, out localPoint))
            return;

        Vector2 parentSize = parentRect.rect.size;
        Vector2 parentPivotOffset = new Vector2(parentSize.x * parentRect.pivot.x, parentSize.y * parentRect.pivot.y);
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(background.anchorMin.x, background.anchorMax.x, background.pivot.x) * parentSize.x,
            Mathf.Lerp(background.anchorMin.y, background.anchorMax.y, background.pivot.y) * parentSize.y);

        background.anchoredPosition = localPoint + parentPivotOffset - anchorReference;
    }
}
