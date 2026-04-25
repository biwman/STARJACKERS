using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PhotonView))]
public class PlayerNicknameUI : MonoBehaviourPun
{
    const string OverlayName = "PlayerNicknameOverlay";

    GameObject rootObject;
    RectTransform rootRect;
    TextMeshProUGUI textLabel;
    Image backgroundImage;
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
        RefreshLabel();
    }

    void Update()
    {
        RefreshLabel();
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

        if (photonView.IsMine || GetComponent<EnemyBot>() != null || GetComponent<AstronautSurvivor>() != null)
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
        rootRect.sizeDelta = new Vector2(180f, 26f);

        backgroundImage = rootObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.03f, 0.05f, 0.08f, 0.48f);

        GameObject textObject = new GameObject("NicknameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rootObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        textLabel = textObject.GetComponent<TextMeshProUGUI>();
        textLabel.fontSize = 15f;
        textLabel.alignment = TextAlignmentOptions.Center;
        textLabel.color = Color.white;
        textLabel.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            textLabel.font = referenceText.font;
            textLabel.fontSharedMaterial = referenceText.fontSharedMaterial;
        }
    }

    void RefreshLabel()
    {
        if (textLabel == null)
            return;

        string nickname = photonView.Owner != null && !string.IsNullOrWhiteSpace(photonView.Owner.NickName)
            ? photonView.Owner.NickName.Trim()
            : "Player " + photonView.OwnerActorNr;

        textLabel.text = nickname;
    }

    void UpdateOverlay()
    {
        EnsureOverlay();
        if (rootObject == null || rootRect == null || canvasRect == null)
            return;

        bool shouldShow = !photonView.IsMine &&
                          GetComponent<EnemyBot>() == null &&
                          GetComponent<AstronautSurvivor>() == null &&
                          health != null &&
                          !health.IsWreck &&
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
            ? new Vector3(spriteRenderer.bounds.center.x, spriteRenderer.bounds.max.y + 0.35f, transform.position.z)
            : transform.position + Vector3.up * 0.8f;

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
