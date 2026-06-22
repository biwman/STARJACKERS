using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public sealed class AdvancedShootInputZone : MonoBehaviourPun
{
    const string ZoneObjectName = "AdvancedShootInputZone";
    const float HoldToFloatDelay = 0.24f;
    const float DragToFloatPixels = 16f;
    static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>(16);

    PlayerShooting shooting;
    Joystick shootJoystick;
    GameObject zoneObject;
    RectTransform zoneRect;
    Image zoneImage;
    AdvancedShootInputZoneSurface surface;
    bool pointerHeld;
    bool floatingJoystickActive;
    bool waitForFreshPointerRelease = true;
    int pointerId = int.MinValue;
    int pointerDownFrame;
    float pointerDownAt;
    Vector2 pointerDownScreenPosition;
    Vector2 latestPointerScreenPosition;
    Camera pointerCamera;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        EnsureZone();
        RefreshState();
    }

    void Update()
    {
        if (!photonView.IsMine)
            return;

        EnsureZone();
        RefreshState();
        RestoreShootJoystickHomeIfIdle();
        UpdatePolledPointerInput();

        if (pointerHeld && Time.frameCount > pointerDownFrame && !IsTrackedPointerStillDown())
        {
            HandleLostPointerRelease();
            return;
        }

        if (!pointerHeld || floatingJoystickActive || shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        if (Time.time - pointerDownAt >= HoldToFloatDelay)
            ActivateFloatingJoystick(pointerDownScreenPosition, latestPointerScreenPosition, pointerCamera);

        if (floatingJoystickActive && shootJoystick != null)
            shootJoystick.UpdateExternalControl(latestPointerScreenPosition, pointerCamera);
    }

    void OnDisable()
    {
        CancelCurrentPress(true);
    }

    void OnDestroy()
    {
        CancelCurrentPress(true);
        if (zoneObject != null)
            Destroy(zoneObject);
    }

    public void HandlePointerDown(PointerEventData eventData)
    {
        if (eventData == null || shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        BeginPointerPress(eventData.pointerId, eventData.position, eventData.pressEventCamera);
    }

    public void HandleDrag(PointerEventData eventData)
    {
        if (!IsMatchingPointer(eventData) || shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        latestPointerScreenPosition = eventData.position;

        if (!floatingJoystickActive)
        {
            if (Vector2.Distance(pointerDownScreenPosition, eventData.position) >= DragToFloatPixels)
                ActivateFloatingJoystick(pointerDownScreenPosition, latestPointerScreenPosition, pointerCamera);
            else
                return;
        }

        if (shootJoystick != null)
            shootJoystick.UpdateExternalControl(eventData.position, pointerCamera);
    }

    public void HandlePointerUp(PointerEventData eventData)
    {
        if (!IsMatchingPointer(eventData))
            return;

        ReleaseCurrentPress();
    }

    void ReleaseCurrentPress()
    {
        if (!pointerHeld)
            return;

        bool triggerTapShot = pointerHeld && !floatingJoystickActive;
        bool triggerFloatingShot = pointerHeld && floatingJoystickActive;
        bool floatingShotReleased = triggerFloatingShot && shooting != null && shooting.ReleaseAdvancedFloatingAim();
        CancelCurrentPress(true);

        if (triggerTapShot && shooting != null)
            shooting.TriggerAdvancedAutoAimShot();
        else if (triggerFloatingShot && !floatingShotReleased && shooting != null)
            shooting.TriggerAdvancedAutoAimShot();
    }

    void UpdatePolledPointerInput()
    {
        if (shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        if (waitForFreshPointerRelease)
        {
            if (IsAnyPointerCurrentlyDown())
                return;

            waitForFreshPointerRelease = false;
        }

        if (!pointerHeld)
        {
            TryBeginPolledPointerPress();
            return;
        }

        if (pointerId == -1)
        {
            if (TryGetMousePointer(out Vector2 mousePosition, out bool mousePressed, out _, out bool mouseReleased))
            {
                if (mousePressed)
                {
                    latestPointerScreenPosition = mousePosition;
                    if (floatingJoystickActive && shootJoystick != null)
                        shootJoystick.UpdateExternalControl(latestPointerScreenPosition, pointerCamera);
                }

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
            {
                latestPointerScreenPosition = touchPosition;
                if (floatingJoystickActive && shootJoystick != null)
                    shootJoystick.UpdateExternalControl(latestPointerScreenPosition, pointerCamera);
            }

            if (touchReleased)
                ReleaseCurrentPress();
        }
    }

    void TryBeginPolledPointerPress()
    {
        if (TryGetNewTouchPress(out int touchPointerId, out Vector2 touchPosition))
        {
            if (ShouldBeginPolledShootPress(touchPosition))
                BeginPointerPress(touchPointerId, touchPosition, null);

            return;
        }

        if (!TryGetMousePointer(out Vector2 mousePosition, out _, out bool mousePressedThisFrame, out _) || !mousePressedThisFrame)
            return;

        if (!ShouldBeginPolledShootPress(mousePosition))
            return;

        BeginPointerPress(-1, mousePosition, null);
    }

    void BeginPointerPress(int newPointerId, Vector2 screenPosition, Camera eventCamera)
    {
        pointerHeld = true;
        floatingJoystickActive = false;
        pointerId = newPointerId;
        pointerDownFrame = Time.frameCount;
        pointerDownAt = Time.time;
        pointerDownScreenPosition = screenPosition;
        latestPointerScreenPosition = screenPosition;
        pointerCamera = eventCamera;
    }

    void EnsureZone()
    {
        if (zoneObject != null && zoneRect != null && zoneImage != null && surface != null)
        {
            if (shootJoystick == null)
                shootJoystick = FindShootJoystick();
            zoneImage.raycastTarget = false;
            PlaceZoneBehindShootJoystick();
            return;
        }

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find(ZoneObjectName);
        if (existing != null)
            zoneObject = existing.gameObject;
        else
            zoneObject = new GameObject(ZoneObjectName, typeof(RectTransform), typeof(Image), typeof(AdvancedShootInputZoneSurface));

        if (zoneObject.transform.parent != canvas.transform)
            zoneObject.transform.SetParent(canvas.transform, false);

        zoneRect = zoneObject.GetComponent<RectTransform>();
        zoneRect.anchorMin = new Vector2(0.5f, 0f);
        zoneRect.anchorMax = new Vector2(1f, 1f);
        zoneRect.offsetMin = Vector2.zero;
        zoneRect.offsetMax = Vector2.zero;

        zoneImage = zoneObject.GetComponent<Image>();
        zoneImage.color = new Color(1f, 1f, 1f, 0.001f);
        zoneImage.raycastTarget = false;

        surface = zoneObject.GetComponent<AdvancedShootInputZoneSurface>();
        surface.Owner = this;
        shootJoystick = FindShootJoystick();
        PlaceZoneBehindShootJoystick();
    }

    void RefreshState()
    {
        if (zoneObject == null || shooting == null)
            return;

        bool active = shooting.CanUseAdvancedShootJoystick();
        if (zoneObject.activeSelf != active)
        {
            zoneObject.SetActive(active);
            if (active)
                waitForFreshPointerRelease = true;
        }

        if (!active)
        {
            CancelCurrentPress(true);
            waitForFreshPointerRelease = true;
        }
    }

    void ActivateFloatingJoystick(Vector2 baseScreenPosition, Vector2 controlScreenPosition, Camera eventCamera)
    {
        if (floatingJoystickActive)
            return;

        shootJoystick = shootJoystick != null ? shootJoystick : FindShootJoystick();
        if (shootJoystick == null)
            return;

        floatingJoystickActive = true;
        shootJoystick.BeginExternalControl(baseScreenPosition, eventCamera, true);
        shootJoystick.UpdateExternalControl(controlScreenPosition, eventCamera);
    }

    void CancelCurrentPress(bool restoreJoystick)
    {
        if (floatingJoystickActive && shootJoystick != null)
            shootJoystick.EndExternalControl(restoreJoystick);

        pointerHeld = false;
        floatingJoystickActive = false;
        pointerId = int.MinValue;
        latestPointerScreenPosition = Vector2.zero;
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

    static bool TryGetNewTouchPress(out int pointerId, out Vector2 position)
    {
        pointerId = int.MinValue;
        position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;

                pointerId = touch.touchId.ReadValue();
                position = touch.position.ReadValue();
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

            pointerId = touch.fingerId;
            position = touch.position;
            return true;
        }
#endif

        return false;
    }

    static bool TryGetTrackedTouch(int pointerId, out Vector2 position, out bool active, out bool released)
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
                if (touch.touchId.ReadValue() != pointerId)
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
            if (touch.fingerId != pointerId)
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

    void HandleLostPointerRelease()
    {
        ReleaseCurrentPress();
        RestoreShootJoystickHomeIfIdle();
    }

    void RestoreShootJoystickHomeIfIdle()
    {
        shootJoystick = shootJoystick != null ? shootJoystick : FindShootJoystick();
        if (shootJoystick == null || pointerHeld || floatingJoystickActive || shootJoystick.IsPressed || shootJoystick.IsExternalControlActive)
            return;

        if (!shootJoystick.IsAtDefaultBackgroundPosition())
            shootJoystick.RestoreDefaultBackgroundPosition();
    }

    bool IsTrackedPointerStillDown()
    {
        if (pointerId == -1)
            return TryGetMousePointer(out _, out bool mousePressed, out _, out _) && mousePressed;
        if (pointerId < 0)
            return IsAnyPointerCurrentlyDown();

        return TryGetTrackedTouch(pointerId, out _, out bool touchActive, out bool touchReleased) && touchActive && !touchReleased;
    }

    bool IsMatchingPointer(PointerEventData eventData)
    {
        return pointerHeld && eventData != null && eventData.pointerId == pointerId;
    }

    bool ShouldBeginPolledShootPress(Vector2 screenPosition)
    {
        if (screenPosition.x < Screen.width * 0.5f)
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

            if (target.GetComponentInParent<AdvancedShootInputZoneSurface>() != null)
                continue;

            Joystick joystick = target.GetComponentInParent<Joystick>();
            if (joystick != null)
            {
                if (IsShootJoystick(joystick))
                    return IsInsideJoystickCircle(joystick, screenPosition);

                return true;
            }

            if (target.GetComponentInParent<Selectable>() != null)
                return true;
        }

        return false;
    }

    bool IsShootJoystick(Joystick joystick)
    {
        if (joystick == null)
            return false;

        shootJoystick = shootJoystick != null ? shootJoystick : FindShootJoystick();
        if (joystick == shootJoystick)
            return true;

        if (joystick.name == "ShootJoystickBG")
            return true;

        return joystick.background != null && joystick.background.name == "ShootJoystickBG";
    }

    static bool IsInsideJoystickCircle(Joystick joystick, Vector2 screenPosition)
    {
        RectTransform rect = joystick != null ? joystick.background : null;
        if (rect == null)
            return false;

        Camera eventCamera = null;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
        return localPoint.sqrMagnitude <= radius * radius;
    }

    void PlaceZoneBehindShootJoystick()
    {
        if (zoneObject == null || zoneObject.transform.parent == null)
            return;

        RectTransform joystickRect = FindShootJoystickRectTransform();
        if (joystickRect == null || joystickRect.parent != zoneObject.transform.parent)
            return;

        int joystickIndex = joystickRect.GetSiblingIndex();
        zoneObject.transform.SetSiblingIndex(Mathf.Max(0, joystickIndex));
    }

    static RectTransform FindShootJoystickRectTransform()
    {
        Joystick joystick = FindShootJoystick();
        if (joystick == null)
            return null;

        if (joystick.background != null)
            return joystick.background;

        return joystick.GetComponent<RectTransform>();
    }

    static Joystick FindShootJoystick()
    {
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        Joystick joystick = shootJoystickObject != null ? shootJoystickObject.GetComponent<Joystick>() : null;
        if (joystick != null)
            return joystick;

        Joystick[] candidates = FindObjectsByType<Joystick>(FindObjectsInactive.Exclude);
        foreach (Joystick candidate in candidates)
        {
            if (candidate == null)
                continue;

            if (candidate.name == "ShootJoystickBG")
                return candidate;

            if (candidate.background != null && candidate.background.name == "ShootJoystickBG")
                return candidate;
        }

        return null;
    }
}
