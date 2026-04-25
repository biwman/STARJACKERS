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
        rootRect.sizeDelta = new Vector2(76f, 10f);

        backgroundImage = rootObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.1f, 0.03f, 0.03f, 0.82f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(rootObject.transform, false);
        fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(0f, -1f);

        fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.95f, 0.15f, 0.15f, 0.95f);
    }

    void RefreshBar()
    {
        if (health == null || fillRect == null || rootRect == null)
            return;

        float normalized = health.maxHP > 0 ? Mathf.Clamp01((float)health.CurrentHP / health.maxHP) : 0f;
        float width = Mathf.Max(0f, (rootRect.sizeDelta.x - 2f) * normalized);
        fillRect.sizeDelta = new Vector2(width, -2f);
    }

    void UpdateOverlay()
    {
        EnsureOverlay();
        if (rootObject == null || rootRect == null || canvasRect == null || health == null)
            return;

        bool shouldShow = !health.IsWreck &&
                          health.CurrentHP > 0 &&
                          health.CurrentHP < health.maxHP &&
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
            ? new Vector3(spriteRenderer.bounds.center.x, spriteRenderer.bounds.max.y + 0.18f, transform.position.z)
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
