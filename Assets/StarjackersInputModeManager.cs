using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public enum StarjackersInputMode
{
    Touch,
    KeyboardMouse,
    Gamepad
}

public sealed class StarjackersInputModeManager : MonoBehaviour
{
    const string RootName = "StarjackersInputMode";
    const float MoveStickDeadZone = 0.18f;
    const float AimStickDeadZone = 0.22f;
    const int CrosshairCursorSize = 32;

    static StarjackersInputModeManager instance;
    static Texture2D crosshairCursorTexture;

    StarjackersInputMode currentMode;
    bool customCursorApplied;

    public static event Action<StarjackersInputMode> InputModeChanged;

    public static StarjackersInputMode CurrentMode => EnsureInstance().currentMode;
    public static bool PcTouchJoystickTestModeActive => !Application.isMobilePlatform && DeveloperInputSettings.PcTouchJoystickTestModeEnabled;
    public static bool TouchControlsActive => CurrentMode == StarjackersInputMode.Touch || PcTouchJoystickTestModeActive;
    public static bool DirectGameplayInputActive => CurrentMode != StarjackersInputMode.Touch || PcTouchJoystickTestModeActive;
    public static bool DirectShootingInputActive => DirectGameplayInputActive && !PcTouchJoystickTestModeActive;
    public static bool DesktopHudLayoutActive => !Application.isMobilePlatform && CurrentMode != StarjackersInputMode.Touch && !PcTouchJoystickTestModeActive;
    public static bool IsGamepadMode => CurrentMode == StarjackersInputMode.Gamepad;
    public static bool IsKeyboardMouseMode => CurrentMode == StarjackersInputMode.KeyboardMouse;
    public static int GadgetHotkeyCount => 4;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static StarjackersInputModeManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<StarjackersInputModeManager>();
        if (instance != null)
            return instance;

        GameObject root = new GameObject(RootName);
        if (Application.isPlaying)
            DontDestroyOnLoad(root);

        instance = root.AddComponent<StarjackersInputModeManager>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        currentMode = Application.isMobilePlatform ? StarjackersInputMode.Touch : StarjackersInputMode.KeyboardMouse;
        if (Application.isPlaying && transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        RefreshInputMode();
        RefreshCursor();
    }

    void OnDisable()
    {
        if (customCursorApplied)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            customCursorApplied = false;
        }
    }

    void RefreshInputMode()
    {
        if (HasTouchActivity())
            SetMode(StarjackersInputMode.Touch);

        if (HasKeyboardMouseActivity())
            SetMode(StarjackersInputMode.KeyboardMouse);

        if (HasGamepadActivity())
            SetMode(StarjackersInputMode.Gamepad);
    }

    void SetMode(StarjackersInputMode mode)
    {
        if (currentMode == mode)
            return;

        currentMode = mode;
        InputModeChanged?.Invoke(currentMode);
    }

    void RefreshCursor()
    {
        bool shouldUseCrosshair = !Application.isMobilePlatform && DirectShootingInputActive;
        if (shouldUseCrosshair == customCursorApplied)
            return;

        customCursorApplied = shouldUseCrosshair;
        if (customCursorApplied)
            Cursor.SetCursor(GetCrosshairCursorTexture(), new Vector2(CrosshairCursorSize * 0.5f, CrosshairCursorSize * 0.5f), CursorMode.Auto);
        else
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public static Vector2 ReadMoveVector()
    {
        Vector2 keyboardMove = ReadKeyboardMoveVector();
        Vector2 gamepadMove = ReadGamepadMoveVector();

        if (gamepadMove.sqrMagnitude > 0.0001f && (IsGamepadMode || keyboardMove.sqrMagnitude <= 0.0001f))
            return ClampMoveVector(gamepadMove);

        if (keyboardMove.sqrMagnitude > 0.0001f)
            return ClampMoveVector(keyboardMove);

        return Vector2.zero;
    }

    public static bool IsBoostHeld()
    {
        return IsKeyboardBoostHeld() || IsGamepadBoostHeld();
    }

    public static bool IsDirectAimHeld()
    {
        if (IsTypingInInputField())
            return false;

        return IsMouseLeftHeld() ||
               IsMouseRightHeld() ||
               IsKeyboardFireHeld() ||
               IsGamepadFireHeld() ||
               IsGamepadSuperHeld() ||
               ReadGamepadAimVector().sqrMagnitude > 0.0001f;
    }

    public static bool TryReadAimDirection(Vector3 worldOrigin, out Vector2 direction, out Vector2 worldPoint, out bool hasWorldPoint)
    {
        direction = Vector2.zero;
        worldPoint = default;
        hasWorldPoint = false;

        Vector2 gamepadAim = ReadGamepadAimVector();
        if (gamepadAim.sqrMagnitude > 0.0001f && IsGamepadMode)
        {
            direction = gamepadAim.normalized;
            worldPoint = (Vector2)worldOrigin + direction;
            return true;
        }

        if (TryReadMouseWorldPoint(worldOrigin, out worldPoint))
        {
            Vector2 toPointer = worldPoint - (Vector2)worldOrigin;
            if (toPointer.sqrMagnitude > 0.0001f)
            {
                direction = toPointer.normalized;
                hasWorldPoint = true;
                return true;
            }
        }

        if (gamepadAim.sqrMagnitude > 0.0001f)
        {
            direction = gamepadAim.normalized;
            worldPoint = (Vector2)worldOrigin + direction;
            return true;
        }

        return false;
    }

    public static bool WasFirePressedThisFrame()
    {
        if (IsTypingInInputField())
            return false;

        return WasMouseLeftPressedThisFrame() ||
               WasKeyboardFirePressedThisFrame() ||
               WasGamepadFirePressedThisFrame();
    }

    public static bool IsFireHeld()
    {
        if (IsTypingInInputField())
            return false;

        return IsMouseLeftHeld() ||
               IsKeyboardFireHeld() ||
               IsGamepadFireHeld();
    }

    public static bool WasFireReleasedThisFrame()
    {
        return WasMouseLeftReleasedThisFrame() ||
               WasKeyboardFireReleasedThisFrame() ||
               WasGamepadFireReleasedThisFrame();
    }

    public static bool WasSuperPressedThisFrame()
    {
        if (IsTypingInInputField())
            return false;

        return WasMouseRightPressedThisFrame() || WasGamepadSuperPressedThisFrame();
    }

    public static bool IsSuperHeld()
    {
        if (IsTypingInInputField())
            return false;

        return IsMouseRightHeld() || IsGamepadSuperHeld();
    }

    public static bool WasSuperReleasedThisFrame()
    {
        return WasMouseRightReleasedThisFrame() || WasGamepadSuperReleasedThisFrame();
    }

    public static bool WasUsePressedThisFrame()
    {
        if (IsTypingInInputField())
            return false;

        return WasKeyboardUsePressedThisFrame() || WasGamepadUsePressedThisFrame();
    }

    public static bool WasWeaponSwitchPressedThisFrame()
    {
        if (IsTypingInInputField())
            return false;

        return WasKeyboardWeaponSwitchPressedThisFrame() || WasGamepadWeaponSwitchPressedThisFrame();
    }

    public static bool WasCargoTogglePressedThisFrame()
    {
        if (IsTypingInInputField())
            return false;

        return WasKeyboardCargoTogglePressedThisFrame();
    }

    public static bool WasReloadPressedThisFrame()
    {
        if (IsTypingInInputField())
            return false;

        return WasKeyboardReloadPressedThisFrame() || WasGamepadReloadPressedThisFrame();
    }

    public static bool WasGadgetPressedThisFrame(int index)
    {
        if (IsTypingInInputField())
            return false;

        return WasKeyboardGadgetPressedThisFrame(index);
    }

    public static bool IsGadgetHeld(int index)
    {
        if (IsTypingInInputField())
            return false;

        return IsKeyboardGadgetHeld(index);
    }

    public static bool WasGadgetReleasedThisFrame(int index)
    {
        return WasKeyboardGadgetReleasedThisFrame(index);
    }

    public static bool IsTypingInInputField()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponentInParent<TMP_InputField>() != null ||
               selected.GetComponent<UnityEngine.UI.InputField>() != null ||
               selected.GetComponentInParent<UnityEngine.UI.InputField>() != null;
    }

    static Vector2 ClampMoveVector(Vector2 vector)
    {
        return vector.sqrMagnitude > 1f ? vector.normalized : vector;
    }

    static Texture2D GetCrosshairCursorTexture()
    {
        if (crosshairCursorTexture != null)
            return crosshairCursorTexture;

        Texture2D texture = new Texture2D(CrosshairCursorSize, CrosshairCursorSize, TextureFormat.RGBA32, false);
        texture.name = "StarjackersCrosshairCursor";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;

        Vector2 center = new Vector2((CrosshairCursorSize - 1) * 0.5f, (CrosshairCursorSize - 1) * 0.5f);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color outlineColor = new Color(0.02f, 0.08f, 0.12f, 0.92f);
        Color reticleColor = new Color(0.34f, 0.95f, 1f, 0.98f);
        Color centerColor = new Color(1f, 0.78f, 0.22f, 0.98f);

        for (int y = 0; y < CrosshairCursorSize; y++)
        {
            for (int x = 0; x < CrosshairCursorSize; x++)
            {
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool ring = distance >= 8.2f && distance <= 9.8f;
                bool ringOutline = distance >= 7.2f && distance <= 10.8f;
                bool horizontal = dy <= 0.6f && dx >= 4f && dx <= 11f;
                bool vertical = dx <= 0.6f && dy >= 4f && dy <= 11f;
                bool crossOutline = (dy <= 1.4f && dx >= 3f && dx <= 12f) ||
                                    (dx <= 1.4f && dy >= 3f && dy <= 12f);
                bool centerDot = distance <= 1.6f;

                Color pixel = clear;
                if (ringOutline || crossOutline)
                    pixel = outlineColor;
                if (ring || horizontal || vertical)
                    pixel = reticleColor;
                if (centerDot)
                    pixel = centerColor;

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        crosshairCursorTexture = texture;
        return crosshairCursorTexture;
    }

    static bool HasTouchActivity()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null && (touchscreen.primaryTouch.press.wasPressedThisFrame || touchscreen.primaryTouch.press.isPressed))
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
            return true;
#endif

        return false;
    }

    static bool HasKeyboardMouseActivity()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && (keyboard.anyKey.wasPressedThisFrame || ReadKeyboardMoveVector().sqrMagnitude > 0.0001f))
            return true;

        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame ||
                mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame ||
                mouse.scroll.ReadValue().sqrMagnitude > 0.01f ||
                mouse.delta.ReadValue().sqrMagnitude > 0.25f)
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.anyKeyDown || ReadKeyboardMoveVector().sqrMagnitude > 0.0001f)
            return true;

        if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.01f ||
            Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.01f ||
            Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
        {
            return true;
        }
#endif

        return false;
    }

    static bool HasGamepadActivity()
    {
        if (ReadGamepadMoveVector().sqrMagnitude > 0.0001f || ReadGamepadAimVector().sqrMagnitude > 0.0001f)
            return true;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad == null)
            return false;

        return gamepad.buttonSouth.wasPressedThisFrame ||
               gamepad.buttonNorth.wasPressedThisFrame ||
               gamepad.buttonWest.wasPressedThisFrame ||
               gamepad.buttonEast.wasPressedThisFrame ||
               gamepad.leftShoulder.wasPressedThisFrame ||
               gamepad.rightShoulder.wasPressedThisFrame ||
               gamepad.leftTrigger.ReadValue() > 0.2f ||
               gamepad.rightTrigger.ReadValue() > 0.2f ||
               gamepad.dpad.ReadValue().sqrMagnitude > 0.2f;
#else
        return false;
#endif
    }

    static Vector2 ReadKeyboardMoveVector()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            input.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            input.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            input.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            input.y += 1f;
#endif

        return ClampMoveVector(input);
    }

    static Vector2 ReadGamepadMoveVector()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad == null)
            return Vector2.zero;

        Vector2 value = gamepad.leftStick.ReadValue();
        return value.magnitude >= MoveStickDeadZone ? value : Vector2.zero;
#else
        return Vector2.zero;
#endif
    }

    static Vector2 ReadGamepadAimVector()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad == null)
            return Vector2.zero;

        Vector2 value = gamepad.rightStick.ReadValue();
        return value.magnitude >= AimStickDeadZone ? value : Vector2.zero;
#else
        return Vector2.zero;
#endif
    }

    static bool IsKeyboardBoostHeld()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            return true;
#endif

        return false;
    }

    static bool IsGamepadBoostHeld()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.leftStickButton.isPressed || gamepad.leftShoulder.isPressed);
#else
        return false;
#endif
    }

    static bool TryReadMouseWorldPoint(Vector3 worldOrigin, out Vector2 worldPoint)
    {
        worldPoint = default;
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector2 screenPosition = Vector2.zero;
        bool hasMouse = false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            hasMouse = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!hasMouse)
        {
            screenPosition = Input.mousePosition;
            hasMouse = true;
        }
#endif

        if (!hasMouse)
            return false;

        float depth = Mathf.Abs(camera.transform.position.z - worldOrigin.z);
        Vector3 resolved = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPoint = resolved;
        return true;
    }

    static bool IsMousePointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }

    static bool WasMouseLeftPressedThisFrame()
    {
        if (IsMousePointerOverUi())
            return false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
            return true;
#endif

        return false;
    }

    static bool IsMouseLeftHeld()
    {
        if (IsMousePointerOverUi())
            return false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(0))
            return true;
#endif

        return false;
    }

    static bool WasMouseLeftReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonUp(0))
            return true;
#endif

        return false;
    }

    static bool WasMouseRightPressedThisFrame()
    {
        if (IsMousePointerOverUi())
            return false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1))
            return true;
#endif

        return false;
    }

    static bool IsMouseRightHeld()
    {
        if (IsMousePointerOverUi())
            return false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(1))
            return true;
#endif

        return false;
    }

    static bool WasMouseRightReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.rightButton.wasReleasedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonUp(1))
            return true;
#endif

        return false;
    }

    static bool WasKeyboardFirePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.leftCtrlKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.LeftControl))
            return true;
#endif

        return false;
    }

    static bool IsKeyboardFireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.leftCtrlKey.isPressed)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftControl))
            return true;
#endif

        return false;
    }

    static bool WasKeyboardFireReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.leftCtrlKey.wasReleasedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyUp(KeyCode.LeftControl))
            return true;
#endif

        return false;
    }

    static bool WasKeyboardUsePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            return true;
#endif

        return false;
    }

    static bool WasKeyboardWeaponSwitchPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Q))
            return true;
#endif

        return false;
    }

    static bool WasKeyboardCargoTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Tab))
            return true;
#endif

        return false;
    }

    static bool WasKeyboardReloadPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R))
            return true;
#endif

        return false;
    }

    static bool WasKeyboardGadgetPressedThisFrame(int index)
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            switch (index)
            {
                case 0: return keyboard.digit1Key.wasPressedThisFrame;
                case 1: return keyboard.digit2Key.wasPressedThisFrame;
                case 2: return keyboard.digit3Key.wasPressedThisFrame;
                case 3: return keyboard.digit4Key.wasPressedThisFrame;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        switch (index)
        {
            case 0: return Input.GetKeyDown(KeyCode.Alpha1);
            case 1: return Input.GetKeyDown(KeyCode.Alpha2);
            case 2: return Input.GetKeyDown(KeyCode.Alpha3);
            case 3: return Input.GetKeyDown(KeyCode.Alpha4);
        }
#endif

        return false;
    }

    static bool IsKeyboardGadgetHeld(int index)
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            switch (index)
            {
                case 0: return keyboard.digit1Key.isPressed;
                case 1: return keyboard.digit2Key.isPressed;
                case 2: return keyboard.digit3Key.isPressed;
                case 3: return keyboard.digit4Key.isPressed;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        switch (index)
        {
            case 0: return Input.GetKey(KeyCode.Alpha1);
            case 1: return Input.GetKey(KeyCode.Alpha2);
            case 2: return Input.GetKey(KeyCode.Alpha3);
            case 3: return Input.GetKey(KeyCode.Alpha4);
        }
#endif

        return false;
    }

    static bool WasKeyboardGadgetReleasedThisFrame(int index)
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            switch (index)
            {
                case 0: return keyboard.digit1Key.wasReleasedThisFrame;
                case 1: return keyboard.digit2Key.wasReleasedThisFrame;
                case 2: return keyboard.digit3Key.wasReleasedThisFrame;
                case 3: return keyboard.digit4Key.wasReleasedThisFrame;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        switch (index)
        {
            case 0: return Input.GetKeyUp(KeyCode.Alpha1);
            case 1: return Input.GetKeyUp(KeyCode.Alpha2);
            case 2: return Input.GetKeyUp(KeyCode.Alpha3);
            case 3: return Input.GetKeyUp(KeyCode.Alpha4);
        }
#endif

        return false;
    }

    static bool WasGamepadFirePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.rightTrigger.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame);
#else
        return false;
#endif
    }

    static bool IsGamepadFireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.rightTrigger.isPressed || gamepad.rightShoulder.isPressed);
#else
        return false;
#endif
    }

    static bool WasGamepadFireReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.rightTrigger.wasReleasedThisFrame || gamepad.rightShoulder.wasReleasedThisFrame);
#else
        return false;
#endif
    }

    static bool WasGamepadSuperPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.leftTrigger.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame);
#else
        return false;
#endif
    }

    static bool IsGamepadSuperHeld()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.leftTrigger.isPressed || gamepad.leftShoulder.isPressed);
#else
        return false;
#endif
    }

    static bool WasGamepadSuperReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.leftTrigger.wasReleasedThisFrame || gamepad.leftShoulder.wasReleasedThisFrame);
#else
        return false;
#endif
    }

    static bool WasGamepadUsePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame);
#else
        return false;
#endif
    }

    static bool WasGamepadWeaponSwitchPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && gamepad.dpad.right.wasPressedThisFrame;
#else
        return false;
#endif
    }

    static bool WasGamepadReloadPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
#else
        return false;
#endif
    }
}

public sealed class StarjackersTouchControlsVisibility : MonoBehaviour
{
    const string RootName = "StarjackersTouchControlsVisibility";
    const float RefreshInterval = 0.2f;

    static readonly string[] TouchOnlyObjectNames =
    {
        "JoystickBG",
        "MovementJoystickBoosterRing",
        "ShootJoystickBG",
        "AdvancedShootInputZone"
    };

    static StarjackersTouchControlsVisibility instance;

    readonly Dictionary<GameObject, CanvasGroup> canvasGroups = new Dictionary<GameObject, CanvasGroup>();
    float nextRefreshTime;
    bool lastTouchControlsActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static StarjackersTouchControlsVisibility EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<StarjackersTouchControlsVisibility>();
        if (instance != null)
            return instance;

        GameObject root = new GameObject(RootName);
        if (Application.isPlaying)
            DontDestroyOnLoad(root);

        instance = root.AddComponent<StarjackersTouchControlsVisibility>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        lastTouchControlsActive = StarjackersInputModeManager.TouchControlsActive;
        if (Application.isPlaying && transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        StarjackersInputModeManager.InputModeChanged += HandleInputModeChanged;
    }

    void OnDisable()
    {
        StarjackersInputModeManager.InputModeChanged -= HandleInputModeChanged;
    }

    void Update()
    {
        bool touchControlsActive = StarjackersInputModeManager.TouchControlsActive;
        if (touchControlsActive != lastTouchControlsActive || Time.unscaledTime >= nextRefreshTime)
        {
            lastTouchControlsActive = touchControlsActive;
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            Refresh(touchControlsActive);
        }
    }

    void HandleInputModeChanged(StarjackersInputMode mode)
    {
        lastTouchControlsActive = StarjackersInputModeManager.TouchControlsActive;
        Refresh(lastTouchControlsActive);
    }

    void Refresh(bool visible)
    {
        for (int i = 0; i < TouchOnlyObjectNames.Length; i++)
        {
            GameObject touchControl = GameObject.Find(TouchOnlyObjectNames[i]);
            if (touchControl == null)
                continue;

            ApplyVisibility(touchControl, visible);
        }
    }

    void ApplyVisibility(GameObject target, bool visible)
    {
        CanvasGroup group = GetCanvasGroup(target);
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        if (visible)
            return;

        Joystick joystick = target.GetComponent<Joystick>();
        if (joystick != null && joystick.IsPressed)
            joystick.EndExternalControl(true);
    }

    CanvasGroup GetCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        if (canvasGroups.TryGetValue(target, out CanvasGroup group) && group != null)
            return group;

        group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        canvasGroups[target] = group;
        return group;
    }
}
