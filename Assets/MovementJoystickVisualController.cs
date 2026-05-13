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
    RectTransform handleRect;
    Vector3 handleBaseScale = Vector3.one;
    float intensity;

    public void Configure(Joystick targetJoystick, Image background, Image handle, Image glow, Image ring, Image inner)
    {
        joystick = targetJoystick;
        backgroundImage = background;
        handleImage = handle;
        glowImage = glow;
        ringImage = ring;
        innerImage = inner;
        handleRect = handleImage != null ? handleImage.rectTransform : null;
        if (handleRect != null)
            handleBaseScale = handleRect.localScale;

        ApplyVisualState(0f);
    }

    void OnDisable()
    {
        intensity = 0f;
        ApplyVisualState(0f);
    }

    void LateUpdate()
    {
        float targetIntensity = 0f;
        if (joystick != null && joystick.IsPressed)
            targetIntensity = Mathf.Clamp01(Mathf.Max(joystick.inputVector.magnitude, joystick.rawInputVector.magnitude * 0.4f));

        intensity = Mathf.MoveTowards(intensity, targetIntensity, Time.unscaledDeltaTime * 8f);
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
    }
}
