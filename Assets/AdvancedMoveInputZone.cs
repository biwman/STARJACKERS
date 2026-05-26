using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
public sealed class AdvancedMoveInputZone : MonoBehaviourPun
{
    static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>(16);

    PlayerMovement movement;
    Joystick moveJoystick;
    bool pointerHeld;
    bool waitForFreshPointerRelease = true;
    int pointerId = int.MinValue;
    int pointerDownFrame;
    Vector2 latestPointerScreenPosition;
    Camera pointerCamera;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        moveJoystick = FindMoveJoystick();
    }

    void Update()
    {
        if (!photonView.IsMine)
            return;

        moveJoystick = moveJoystick != null ? moveJoystick : FindMoveJoystick();
        RefreshState();
        RestoreMoveJoystickHomeIfIdle();
        UpdatePolledPointerInput();

        if (pointerHeld && Time.frameCount > pointerDownFrame && !IsTrackedPointerStillDown())
        {
            ReleaseCurrentPress();
        }
    }

    void OnDisable()
    {
        CancelCurrentPress(true);
    }

    void RefreshState()
    {
        if (movement != null && movement.CanUseAdvancedMoveJoystick())
            return;

        CancelCurrentPress(true);
        waitForFreshPointerRelease = true;
    }

    void UpdatePolledPointerInput()
    {
        if (movement == null || !movement.CanUseAdvancedMoveJoystick())
            return;

        if (waitForFreshPointerRelease)
        {
            if (IsAnyPointerCurrentlyDown())
                return;

            waitForFreshPointerRelease = false;
        }

        if (!pointerHeld)
        {
            if (moveJoystick != null && moveJoystick.IsPressed)
                return;

            TryBeginPolledPointerPress();
            return;
        }

        if (pointerId == -1)
        {
            if (TryGetMousePointer(out Vector2 mousePosition, out bool mousePressed, out _, out bool mouseReleased))
            {
                if (mousePressed)
                    UpdateCurrentPress(mousePosition, pointerCamera);

                if (mouseReleased)
                    ReleaseCurrentPress();
            }

            return;
        }

        if (pointerId < 0)
            return;

        if (TryGetTrackedTouch(pointerId, out Vector2 touchPosition, out bool touchActive, out bool touchReleased))
        {
            if (touchActive)
                UpdateCurrentPress(touchPosition, pointerCamera);

            if (touchReleased)
                ReleaseCurrentPress();
        }
    }

    void TryBeginPolledPointerPress()
    {
        if (TryGetNewTouchPressForMove(out int touchPointerId, out Vector2 touchPosition))
        {
            BeginPointerPress(touchPointerId, touchPosition, null);
            return;
        }

        if (!TryGetMousePointer(out Vector2 mousePosition, out _, out bool mousePressedThisFrame, out _) || !mousePressedThisFrame)
            return;

        if (!ShouldBeginMovePress(mousePosition))
            return;

        BeginPointerPress(-1, mousePosition, null);
    }

    void BeginPointerPress(int newPointerId, Vector2 screenPosition, Camera eventCamera)
    {
        moveJoystick = moveJoystick != null ? moveJoystick : FindMoveJoystick();
        if (moveJoystick == null)
            return;

        pointerHeld = true;
        pointerId = newPointerId;
        pointerDownFrame = Time.frameCount;
        latestPointerScreenPosition = screenPosition;
        pointerCamera = eventCamera;
        moveJoystick.BeginExternalControl(screenPosition, eventCamera, true);
    }

    void UpdateCurrentPress(Vector2 screenPosition, Camera eventCamera)
    {
        latestPointerScreenPosition = screenPosition;
        if (moveJoystick != null)
            moveJoystick.UpdateExternalControl(screenPosition, eventCamera);
    }

    void ReleaseCurrentPress()
    {
        CancelCurrentPress(true);
    }

    void CancelCurrentPress(bool restoreJoystick)
    {
        if (pointerHeld && moveJoystick != null)
            moveJoystick.EndExternalControl(restoreJoystick);

        pointerHeld = false;
        pointerId = int.MinValue;
        latestPointerScreenPosition = Vector2.zero;
    }

    void RestoreMoveJoystickHomeIfIdle()
    {
        moveJoystick = moveJoystick != null ? moveJoystick : FindMoveJoystick();
        if (moveJoystick == null || pointerHeld || moveJoystick.IsPressed || moveJoystick.IsExternalControlActive)
            return;

        if (!moveJoystick.IsAtDefaultBackgroundPosition())
            moveJoystick.RestoreDefaultBackgroundPosition();
    }

    bool IsTrackedPointerStillDown()
    {
        if (pointerId == -1)
            return TryGetMousePointer(out _, out bool mousePressed, out _, out _) && mousePressed;
        if (pointerId < 0)
            return IsAnyPointerCurrentlyDown();

        return TryGetTrackedTouch(pointerId, out _, out bool touchActive, out bool touchReleased) && touchActive && !touchReleased;
    }

    bool ShouldBeginMovePress(Vector2 screenPosition)
    {
        if (screenPosition.x > Screen.width * 0.5f)
            return false;

        return !IsPointerOverBlockedUi(screenPosition);
    }

    bool IsPointerOverBlockedUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        UiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, UiRaycastResults);

        for (int i = 0; i < UiRaycastResults.Count; i++)
        {
            GameObject target = UiRaycastResults[i].gameObject;
            if (target == null)
                continue;

            if (IsMoveJoystickTarget(target))
                continue;

            if (HasBlockingEventHandler(target))
                return true;
        }

        return false;
    }

    bool IsMoveJoystickTarget(GameObject target)
    {
        Joystick joystick = target != null ? target.GetComponentInParent<Joystick>() : null;
        if (joystick == null)
            return false;

        moveJoystick = moveJoystick != null ? moveJoystick : FindMoveJoystick();
        if (joystick == moveJoystick)
            return true;

        if (joystick.name == "JoystickBG")
            return true;

        return joystick.background != null && joystick.background.name == "JoystickBG";
    }

    static bool HasBlockingEventHandler(GameObject target)
    {
        MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour is Selectable ||
                behaviour is Joystick ||
                behaviour is IBeginDragHandler ||
                behaviour is IDragHandler ||
                behaviour is IEndDragHandler ||
                behaviour is IScrollHandler ||
                behaviour is IPointerClickHandler ||
                behaviour is IPointerDownHandler ||
                behaviour is IPointerUpHandler)
            {
                return true;
            }
        }

        return false;
    }

    static Joystick FindMoveJoystick()
    {
        GameObject moveJoystickObject = GameObject.Find("JoystickBG");
        Joystick joystick = moveJoystickObject != null ? moveJoystickObject.GetComponent<Joystick>() : null;
        if (joystick != null)
            return joystick;

        Joystick[] candidates = FindObjectsByType<Joystick>(FindObjectsInactive.Exclude);
        foreach (Joystick candidate in candidates)
        {
            if (candidate == null)
                continue;

            if (candidate.name == "JoystickBG")
                return candidate;

            if (candidate.background != null && candidate.background.name == "JoystickBG")
                return candidate;
        }

        return null;
    }

    static bool IsAnyPointerCurrentlyDown()
    {
        if (TryGetMousePointer(out _, out bool mousePressed, out _, out _) && mousePressed)
            return true;

        if (IsAnyTouchCurrentlyDown())
            return true;

        return false;
    }

    static bool TryGetMousePointer(out Vector2 position, out bool pressed, out bool pressedThisFrame, out bool releasedThisFrame)
    {
        position = Vector2.zero;
        pressed = false;
        pressedThisFrame = false;
        releasedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            pressed = mouse.leftButton.isPressed;
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
        pressed = Input.GetMouseButton(0);
        pressedThisFrame = Input.GetMouseButtonDown(0);
        releasedThisFrame = Input.GetMouseButtonUp(0);
        return true;
#else
        return false;
#endif
    }

    bool TryGetNewTouchPressForMove(out int touchPointerId, out Vector2 position)
    {
        touchPointerId = int.MinValue;
        position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;

                Vector2 touchPosition = touch.position.ReadValue();
                if (!ShouldBeginMovePress(touchPosition))
                    continue;

                touchPointerId = touch.touchId.ReadValue();
                position = touchPosition;
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            if (!ShouldBeginMovePress(touch.position))
                continue;

            touchPointerId = touch.fingerId;
            position = touch.position;
            return true;
        }
#endif

        return false;
    }

    static bool TryGetTrackedTouch(int trackedPointerId, out Vector2 position, out bool active, out bool released)
    {
        position = Vector2.zero;
        active = false;
        released = false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                if (touch.touchId.ReadValue() != trackedPointerId)
                    continue;

                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                position = touch.position.ReadValue();
                active = touch.press.isPressed || phase == UnityEngine.InputSystem.TouchPhase.Began || phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary;
                released = touch.press.wasReleasedThisFrame || phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != trackedPointerId)
                continue;

            position = touch.position;
            active = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }
#endif

        return false;
    }

    static bool IsAnyTouchCurrentlyDown()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                if (touch.press.isPressed || phase == UnityEngine.InputSystem.TouchPhase.Began || phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            TouchPhase phase = Input.GetTouch(i).phase;
            if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
                return true;
        }
#endif

        return false;
    }
}
