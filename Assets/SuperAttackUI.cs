using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerShooting))]
public sealed class SuperAttackUI : MonoBehaviourPun
{
    public const string RootName = "SuperAttackJoystickBG";
    const float OffsetFromShootJoystickX = -340f;
    const float RootSize = 130f;
    const float FillSize = 98f;
    const float HandleSize = 43f;

    PlayerShooting shooting;
    GameObject rootObject;
    RectTransform rootRect;
    Image backgroundImage;
    Image fillImage;
    RectTransform handleRect;
    Joystick joystick;
    RectTransform shootJoystickRect;
    Joystick shootJoystick;
    Sprite circleSprite;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        EnsureUi();
    }

    void Update()
    {
        EnsureUi();
        Refresh();
    }

    void OnDestroy()
    {
        if (rootObject != null)
            Destroy(rootObject);

        if (circleSprite != null && circleSprite.texture != null)
            Destroy(circleSprite.texture);
    }

    void EnsureUi()
    {
        if (rootObject != null && rootRect != null && joystick != null)
            return;

        GameObject canvas = GameObject.Find("Canvas");
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (canvas == null || shootJoystickObject == null)
            return;

        shootJoystickRect = shootJoystickObject.GetComponent<RectTransform>();
        shootJoystick = shootJoystickObject.GetComponent<Joystick>();
        if (shootJoystickRect == null)
            return;

        circleSprite = CreateCircleSprite();
        rootObject = new GameObject(RootName, typeof(RectTransform), typeof(Image), typeof(Joystick));
        rootObject.transform.SetParent(canvas.transform, false);
        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = shootJoystickRect.anchorMin;
        rootRect.anchorMax = shootJoystickRect.anchorMax;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = shootJoystickRect.anchoredPosition + new Vector2(OffsetFromShootJoystickX, 0f);
        rootRect.sizeDelta = new Vector2(RootSize, RootSize);

        backgroundImage = rootObject.GetComponent<Image>();
        backgroundImage.sprite = circleSprite;
        backgroundImage.color = new Color(0.08f, 0.13f, 0.19f, 0.82f);

        GameObject fillObject = new GameObject("SuperAttackFill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(rootObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(FillSize, FillSize);

        fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = circleSprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Bottom;
        fillImage.raycastTarget = false;

        GameObject handleObject = new GameObject("SuperAttackHandle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(rootObject.transform, false);
        handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(HandleSize, HandleSize);
        handleRect.anchoredPosition = Vector2.zero;

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.sprite = circleSprite;
        handleImage.color = new Color(1f, 0.72f, 0.16f, 0.95f);
        handleImage.raycastTarget = false;

        joystick = rootObject.GetComponent<Joystick>();
        joystick.background = rootRect;
        joystick.handle = handleRect;
        joystick.deadZone = 0.08f;
    }

    void Refresh()
    {
        if (rootObject == null || shooting == null)
            return;

        bool visible = shooting.IsComplexShootingActive && RoomSettings.IsSuperAttackToggleEnabled() && IsGameplayHudVisible();
        rootObject.SetActive(visible);
        if (!visible)
            return;

        if (rootRect != null)
        {
            ResolveShootJoystickReference();
            if (shootJoystickRect != null)
            {
                rootRect.anchorMin = shootJoystickRect.anchorMin;
                rootRect.anchorMax = shootJoystickRect.anchorMax;
                Vector2 basePosition = shootJoystick != null ? shootJoystick.DefaultAnchoredPosition : shootJoystickRect.anchoredPosition;
                rootRect.anchoredPosition = basePosition + new Vector2(OffsetFromShootJoystickX, 0f);
                rootRect.sizeDelta = new Vector2(RootSize, RootSize);
            }
        }

        bool ready = shooting.IsSuperAttackReady;
        if (fillImage != null)
        {
            fillImage.fillAmount = Mathf.Clamp01(shooting.SuperChargeNormalized);
            fillImage.color = ready
                ? new Color(1f, 0.56f, 0.08f, 0.92f)
                : new Color(0.18f, 0.55f, 0.95f, 0.7f);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = ready
                ? new Color(0.34f, 0.18f, 0.05f, 0.9f)
                : new Color(0.08f, 0.13f, 0.19f, 0.82f);
        }

        if (handleRect != null)
            handleRect.gameObject.SetActive(ready);

        if (joystick != null)
            joystick.enabled = ready;
    }

    void ResolveShootJoystickReference()
    {
        if (shootJoystickRect != null)
            return;

        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        shootJoystickRect = shootJoystickObject != null ? shootJoystickObject.GetComponent<RectTransform>() : null;
        shootJoystick = shootJoystickObject != null ? shootJoystickObject.GetComponent<Joystick>() : null;
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               GameplayHudVisibility.IsGameplayHudVisible(started);
    }

    Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 1.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
