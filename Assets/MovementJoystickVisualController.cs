using UnityEngine;
using UnityEngine.UI;

public sealed class MovementJoystickVisualController : MonoBehaviour
{
    Joystick joystick;
    Image backgroundImage;
    Image handleImage;
    Image glowImage;
    Image ringImage;
    Image innerImage;
    Graphic boosterRingGraphic;
    JoystickBoosterRingGraphic boosterRingArcGraphic;
    RectTransform handleRect;
    RectTransform joystickRect;
    RectTransform boosterRingRect;
    Vector3 handleBaseScale = Vector3.one;
    float intensity;
    float boosterRingIntensity;

    public void Configure(Joystick targetJoystick, Image background, Image handle, Image glow, Image ring, Image inner, Graphic boosterRing)
    {
        joystick = targetJoystick;
        backgroundImage = background;
        handleImage = handle;
        glowImage = glow;
        ringImage = ring;
        innerImage = inner;
        boosterRingGraphic = boosterRing;
        boosterRingArcGraphic = boosterRing as JoystickBoosterRingGraphic;
        joystickRect = joystick != null && joystick.background != null
            ? joystick.background
            : backgroundImage != null ? backgroundImage.rectTransform : null;
        boosterRingRect = boosterRingGraphic != null ? boosterRingGraphic.rectTransform : null;
        handleRect = handleImage != null ? handleImage.rectTransform : null;
        if (handleRect != null)
            handleBaseScale = handleRect.localScale;

        ApplyVisualState(0f);
    }

    void OnDisable()
    {
        intensity = 0f;
        boosterRingIntensity = 0f;
        ApplyVisualState(0f);
    }

    void LateUpdate()
    {
        SyncBoosterRingTransform();

        float targetIntensity = 0f;
        if (joystick != null && joystick.IsPressed)
            targetIntensity = Mathf.Clamp01(Mathf.Max(joystick.inputVector.magnitude, joystick.rawInputVector.magnitude * 0.4f));

        intensity = Mathf.MoveTowards(intensity, targetIntensity, Time.unscaledDeltaTime * 8f);
        float boosterTarget = 0f;
        bool advancedBoosterEnabled = IsAdvancedBoosterVisualEnabled();
        if (advancedBoosterEnabled)
        {
            float rawMagnitude = GetCurrentRawMagnitude();
            if (rawMagnitude > PlayerMovement.AdvancedBoosterVisualThreshold)
            {
                float outerProgress = Mathf.InverseLerp(
                    PlayerMovement.AdvancedBoosterVisualThreshold,
                    PlayerMovement.AdvancedBoosterOuterInputLimit,
                    rawMagnitude);
                bool boosterActive = PlayerMovement.LocalAdvancedBoosterActive &&
                                     rawMagnitude >= PlayerMovement.AdvancedBoosterActivationThreshold;
                boosterTarget = boosterActive
                    ? Mathf.Lerp(0.78f, 1f, outerProgress)
                    : Mathf.Lerp(0.28f, 0.52f, outerProgress);
            }
        }

        boosterRingIntensity = Mathf.MoveTowards(boosterRingIntensity, boosterTarget, Time.unscaledDeltaTime * 10f);
        ApplyVisualState(intensity);
    }

    void ApplyVisualState(float value)
    {
        Color inactiveFrame = new Color(0.035f, 0.07f, 0.1f, 0.42f);
        Color activeFrame = new Color(0.07f, 0.18f, 0.15f, 0.54f);
        Color inactiveGlow = new Color(0.35f, 0.82f, 1f, 0.05f);
        Color activeGlow = new Color(0.42f, 0.9f, 0.58f, 0.2f);
        Color inactiveRing = new Color(0.32f, 0.62f, 0.78f, 0.22f);
        Color activeRing = new Color(0.42f, 0.9f, 0.58f, 0.55f);
        Color inactiveInner = new Color(0.02f, 0.04f, 0.065f, 0.3f);
        Color activeInner = new Color(0.025f, 0.075f, 0.045f, 0.38f);
        Color inactiveHandle = new Color(0.11f, 0.38f, 0.21f, 0.9f);
        Color activeHandle = new Color(0.42f, 0.9f, 0.58f, 0.98f);

        if (backgroundImage != null)
            backgroundImage.color = Color.Lerp(inactiveFrame, activeFrame, value);

        if (glowImage != null)
            glowImage.color = Color.Lerp(inactiveGlow, activeGlow, value);

        if (ringImage != null)
            ringImage.color = Color.Lerp(inactiveRing, activeRing, value);

        if (innerImage != null)
            innerImage.color = Color.Lerp(inactiveInner, activeInner, value);

        if (handleImage != null)
            handleImage.color = Color.Lerp(inactiveHandle, activeHandle, value);

        if (handleRect != null)
        {
            float scale = Mathf.Lerp(1f, 1.08f, value);
            handleRect.localScale = new Vector3(handleBaseScale.x * scale, handleBaseScale.y * scale, handleBaseScale.z);
        }

        ApplyBoosterRingState();
    }

    void SyncBoosterRingTransform()
    {
        if (boosterRingRect == null || joystickRect == null)
            return;

        if (boosterRingRect.parent == joystickRect)
        {
            boosterRingRect.anchorMin = new Vector2(0.5f, 0.5f);
            boosterRingRect.anchorMax = new Vector2(0.5f, 0.5f);
            boosterRingRect.pivot = new Vector2(0.5f, 0.5f);
            boosterRingRect.anchoredPosition = Vector2.zero;
            boosterRingRect.localRotation = Quaternion.identity;
            boosterRingRect.localScale = Vector3.one;
        }
        else if (boosterRingRect.parent == joystickRect.parent)
        {
            boosterRingRect.anchorMin = joystickRect.anchorMin;
            boosterRingRect.anchorMax = joystickRect.anchorMax;
            boosterRingRect.pivot = joystickRect.pivot;
            boosterRingRect.anchoredPosition = joystickRect.anchoredPosition;
            boosterRingRect.localRotation = joystickRect.localRotation;
            boosterRingRect.localScale = joystickRect.localScale;
        }

        Vector2 baseSize = joystickRect.sizeDelta;
        if (baseSize.x > 0.1f && baseSize.y > 0.1f)
            boosterRingRect.sizeDelta = baseSize * PlayerMovement.AdvancedBoosterOuterInputLimit;
    }

    void ApplyBoosterRingState()
    {
        if (boosterRingGraphic == null)
            return;

        bool advancedBoosterEnabled = IsAdvancedBoosterVisualEnabled();
        float rawMagnitude = GetCurrentRawMagnitude();
        if (!advancedBoosterEnabled || rawMagnitude <= PlayerMovement.AdvancedBoosterVisualThreshold || boosterRingIntensity <= 0.001f)
        {
            boosterRingGraphic.color = new Color(1f, 0.04f, 0.02f, 0f);
            SetBoosterRingArcVisible(false);
            return;
        }

        float outerProgress = Mathf.InverseLerp(
            PlayerMovement.AdvancedBoosterVisualThreshold,
            PlayerMovement.AdvancedBoosterOuterInputLimit,
            rawMagnitude);
        float boostBlend = Mathf.InverseLerp(
            PlayerMovement.AdvancedBoosterActivationThreshold,
            PlayerMovement.AdvancedBoosterOuterInputLimit,
            rawMagnitude);
        bool boosterActive = PlayerMovement.LocalAdvancedBoosterActive &&
                             rawMagnitude >= PlayerMovement.AdvancedBoosterActivationThreshold;
        float pulse = boosterActive
            ? 0.74f + Mathf.Sin(Time.unscaledTime * 18f) * 0.26f
            : 0f;
        float bufferBreath = boosterActive
            ? 1f
            : 0.94f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.06f;
        Color bufferColor = new Color(0.46f, 0.12f, 0.045f, 0.16f);
        Color activeColor = Color.Lerp(
            new Color(0.98f, 0.05f, 0.02f, 0.64f),
            new Color(1f, 0.46f, 0.16f, 0.9f),
            Mathf.Clamp01(pulse));
        Color depletedColor = new Color(0.34f, 0.06f, 0.025f, 0.2f);
        Color targetColor = boosterActive
            ? activeColor
            : PlayerMovement.LocalAdvancedBoosterAvailable
                ? Color.Lerp(bufferColor, new Color(0.62f, 0.11f, 0.04f, 0.24f), boostBlend)
                : Color.Lerp(bufferColor, depletedColor, boostBlend);

        targetColor.a *= boosterRingIntensity * bufferBreath;
        UpdateBoosterRingArc(rawMagnitude, outerProgress, boosterActive);
        boosterRingGraphic.color = targetColor;
    }

    float GetCurrentRawMagnitude()
    {
        float joystickMagnitude = joystick != null && joystick.IsPressed ? joystick.rawInputVector.magnitude : 0f;
        return Mathf.Max(joystickMagnitude, PlayerMovement.LocalAdvancedBoosterInputRatio);
    }

    void UpdateBoosterRingArc(float rawMagnitude, float outerProgress, bool boosterActive)
    {
        if (boosterRingArcGraphic == null)
            return;

        Vector2 direction = joystick != null && joystick.IsPressed && joystick.rawInputVector.sqrMagnitude > 0.0001f
            ? joystick.rawInputVector
            : Vector2.zero;

        if (direction == Vector2.zero || rawMagnitude <= PlayerMovement.AdvancedBoosterVisualThreshold)
        {
            SetBoosterRingArcVisible(false);
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float thickness = boosterActive
            ? Mathf.Lerp(0.72f, 1f, outerProgress)
            : Mathf.Lerp(0.34f, 0.68f, outerProgress);
        float highlight = boosterActive
            ? Mathf.Lerp(0.72f, 1f, outerProgress)
            : Mathf.Lerp(0.24f, 0.52f, outerProgress);
        float arcWidth = boosterActive ? 78f : 72f;
        boosterRingArcGraphic.SetArc(angle, arcWidth, true, thickness, highlight);
    }

    void SetBoosterRingArcVisible(bool visible)
    {
        if (boosterRingArcGraphic != null)
            boosterRingArcGraphic.SetArc(0f, 72f, visible);
    }

    bool IsAdvancedBoosterVisualEnabled()
    {
        return PlayerMovement.LocalAdvancedBoosterEnabled ||
               (joystick != null && joystick.maxRawInputMagnitude > 1.001f);
    }
}
