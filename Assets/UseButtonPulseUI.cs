using UnityEngine;
using UnityEngine.UI;

public sealed class UseButtonPulseUI : MonoBehaviour
{
    Button button;
    Image glow;
    Image core;
    Color accentColor;
    Color coreBaseColor;
    Vector3 coreBaseScale = Vector3.one;
    RectTransform glowRect;
    RectTransform coreRect;
    bool configured;

    public void Configure(Button targetButton, Image glowImage, Image coreImage, Color accent)
    {
        button = targetButton;
        glow = glowImage;
        core = coreImage;
        accentColor = accent;
        glowRect = glow != null ? glow.rectTransform : null;
        coreRect = core != null ? core.rectTransform : null;

        if (core != null)
            coreBaseColor = core.color;
        if (coreRect != null)
            coreBaseScale = coreRect.localScale;

        configured = true;
        enabled = true;
    }

    void Update()
    {
        if (!configured || button == null)
            return;

        bool active = button.isActiveAndEnabled && button.interactable;
        float pulse = active ? (Mathf.Sin(Time.unscaledTime * 5.6f) + 1f) * 0.5f : 0f;

        if (glow != null)
        {
            float alpha = active ? Mathf.Lerp(0.08f, 0.16f, pulse) : 0.04f;
            glow.color = new Color(accentColor.r, accentColor.g, accentColor.b, alpha);
        }

        if (glowRect != null)
        {
            float scale = active ? Mathf.Lerp(1f, 1.018f, pulse) : 1f;
            glowRect.localScale = new Vector3(scale, scale, 1f);
        }

        if (core != null)
        {
            core.color = active
                ? Color.Lerp(coreBaseColor, new Color(1f, 0.86f, 0.22f, 1f), pulse * 0.22f)
                : new Color(0.43f, 0.36f, 0.22f, 0.58f);
        }

        if (coreRect != null)
        {
            float scale = active ? Mathf.Lerp(1f, 1.008f, pulse) : 1f;
            coreRect.localScale = new Vector3(coreBaseScale.x * scale, coreBaseScale.y * scale, 1f);
        }
    }
}
