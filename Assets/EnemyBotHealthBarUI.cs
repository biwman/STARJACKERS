using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(EnemyBot))]
[RequireComponent(typeof(PlayerHealth))]
public class EnemyBotHealthBarUI : MonoBehaviourPun
{
    const string OverlayName = "EnemyBotHealthOverlay";

    GameObject rootObject;
    RectTransform rootRect;
    Image backgroundImage;
    Image fillImage;
    RectTransform fillRect;
    Image shieldFillImage;
    RectTransform shieldFillRect;
    Canvas parentCanvas;
    RectTransform canvasRect;
    Camera cachedCamera;
    PlayerHealth health;
    SpriteRenderer spriteRenderer;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureOverlay();
        RefreshBar();
    }

    void Update()
    {
        RefreshBar();
        UpdateOverlay();
    }

    void OnDestroy()
    {
        if (rootObject != null)
            Destroy(rootObject);
    }

    void EnsureOverlay()
    {
        if (rootObject != null)
            return;

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
            return;

        parentCanvas = canvasObject.GetComponent<Canvas>();
        canvasRect = canvasObject.GetComponent<RectTransform>();
        if (parentCanvas == null || canvasRect == null)
            return;

        rootObject = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(canvasObject.transform, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(78f, 18f);

        backgroundImage = rootObject.GetComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0f);

        CreateBar("Shield", 4.5f, new Color(0.02f, 0.08f, 0.14f, 0.82f), new Color(0.24f, 0.78f, 1f, 0.96f), out shieldFillRect, out shieldFillImage);
        CreateBar("Health", -4.5f, new Color(0.1f, 0.03f, 0.03f, 0.82f), new Color(0.95f, 0.15f, 0.15f, 0.95f), out fillRect, out fillImage);
    }

    void CreateBar(string name, float y, Color backgroundColor, Color fillColor, out RectTransform resultFillRect, out Image resultFillImage)
    {
        GameObject backgroundObject = new GameObject(name + "Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(rootObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(0f, y);
        backgroundRect.sizeDelta = new Vector2(78f, 7f);

        Image barBackground = backgroundObject.GetComponent<Image>();
        barBackground.color = backgroundColor;

        GameObject fillObject = new GameObject(name + "Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);
        resultFillRect = fillObject.GetComponent<RectTransform>();
        resultFillRect.anchorMin = new Vector2(0f, 0f);
        resultFillRect.anchorMax = new Vector2(0f, 1f);
        resultFillRect.pivot = new Vector2(0f, 0.5f);
        resultFillRect.offsetMin = new Vector2(1f, 1f);
        resultFillRect.offsetMax = new Vector2(0f, -1f);

        resultFillImage = fillObject.GetComponent<Image>();
        resultFillImage.color = fillColor;
    }

    void RefreshBar()
    {
        if (health == null || fillRect == null || rootRect == null)
            return;

        float normalized = health.maxHP > 0 ? Mathf.Clamp01((float)health.CurrentHP / health.maxHP) : 0f;
        float width = Mathf.Max(0f, (rootRect.sizeDelta.x - 2f) * normalized);
        fillRect.sizeDelta = new Vector2(width, -2f);

        if (shieldFillRect != null)
        {
            float shieldNormalized = health.MaxShield > 0 ? Mathf.Clamp01((float)health.CurrentShield / health.MaxShield) : 0f;
            float shieldWidth = Mathf.Max(0f, (rootRect.sizeDelta.x - 2f) * shieldNormalized);
            shieldFillRect.sizeDelta = new Vector2(shieldWidth, -2f);
        }
    }

    void UpdateOverlay()
    {
        EnsureOverlay();
        if (rootObject == null || rootRect == null || canvasRect == null || health == null)
            return;

        bool healthDamaged = health.CurrentHP > 0 && health.CurrentHP < health.maxHP;
        bool shieldDamaged = health.MaxShield > 0 && health.CurrentShield < health.MaxShield;
        bool shouldShow = !health.IsWreck &&
                          (healthDamaged || shieldDamaged) &&
                          spriteRenderer != null &&
                          spriteRenderer.enabled;

        if (cachedCamera == null)
            cachedCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        if (cachedCamera == null)
        {
            rootObject.SetActive(false);
            return;
        }

        Vector3 worldAnchor = spriteRenderer != null
            ? new Vector3(spriteRenderer.bounds.center.x, spriteRenderer.bounds.max.y + 0.24f, transform.position.z)
            : transform.position + Vector3.up * 0.75f;

        Vector3 screenPoint = cachedCamera.WorldToScreenPoint(worldAnchor);
        if (!shouldShow || screenPoint.z <= 0f)
        {
            rootObject.SetActive(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cachedCamera,
            out Vector2 localPoint);

        rootRect.anchoredPosition = localPoint;
        rootObject.SetActive(true);
    }
}
