using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerShooting))]
public sealed class WeaponSwitchButtonUI : MonoBehaviourPun
{
    public const string RootName = "WeaponSwitchButton";
    static readonly Vector2 OffsetFromShootJoystick = new Vector2(-126f, -154f);

    static Sprite circularSprite;

    PlayerShooting shooting;
    GameObject buttonObject;
    RectTransform buttonRect;
    Image backgroundImage;
    Image innerBackgroundImage;
    Image nextWeaponIconImage;
    Button button;
    RectTransform shootJoystickRect;
    Joystick shootJoystick;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        EnsureButton();
    }

    void Update()
    {
        EnsureButton();
        Refresh();
    }

    void OnDestroy()
    {
        if (buttonObject != null)
            Destroy(buttonObject);
    }

    void EnsureButton()
    {
        if (buttonObject != null && buttonRect != null && button != null && innerBackgroundImage != null && nextWeaponIconImage != null)
            return;

        GameObject canvas = GameObject.Find("Canvas");
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (canvas == null || shootJoystickObject == null)
            return;

        shootJoystickRect = shootJoystickObject.GetComponent<RectTransform>();
        shootJoystick = shootJoystickObject.GetComponent<Joystick>();
        if (shootJoystickRect == null)
            return;

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Destroy(existing);

        buttonObject = new GameObject(RootName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);
        buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = shootJoystickRect.anchorMin;
        buttonRect.anchorMax = shootJoystickRect.anchorMax;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = shootJoystickRect.anchoredPosition + OffsetFromShootJoystick;
        buttonRect.sizeDelta = new Vector2(96f, 96f);

        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.sprite = GetCircularSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = new Color(1f, 0.1f, 0.48f, 0.98f);

        button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = backgroundImage;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClicked);

        GameObject innerObject = new GameObject("WeaponSwitchInnerBackground", typeof(RectTransform), typeof(Image));
        innerObject.transform.SetParent(buttonObject.transform, false);
        RectTransform innerRect = innerObject.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(10f, 10f);
        innerRect.offsetMax = new Vector2(-10f, -10f);

        innerBackgroundImage = innerObject.GetComponent<Image>();
        innerBackgroundImage.sprite = GetCircularSprite();
        innerBackgroundImage.type = Image.Type.Simple;
        innerBackgroundImage.raycastTarget = false;
        innerBackgroundImage.color = new Color(0.29f, 0.03f, 0.1f, 0.98f);

        GameObject iconObject = new GameObject("WeaponSwitchNextIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(18f, 18f);
        iconRect.offsetMax = new Vector2(-18f, -18f);

        nextWeaponIconImage = iconObject.GetComponent<Image>();
        nextWeaponIconImage.preserveAspect = true;
        nextWeaponIconImage.raycastTarget = false;
        nextWeaponIconImage.color = new Color(1f, 1f, 1f, 0.92f);
    }

    void Refresh()
    {
        if (buttonObject == null || shooting == null)
            return;

        bool visible = shooting.IsComplexShootingActive &&
                       shooting.ComplexWeaponCount > 1 &&
                       IsGameplayHudVisible();
        if (buttonObject.activeSelf != visible)
            buttonObject.SetActive(visible);

        if (!visible)
            return;

        bool lockedByDamage = shooting.IsComplexWeaponSwitchLockedByDamage;
        if (button != null)
            button.interactable = !lockedByDamage;

        if (backgroundImage != null)
            backgroundImage.color = lockedByDamage ? new Color(0.48f, 0.08f, 0.08f, 0.92f) : new Color(1f, 0.1f, 0.48f, 0.98f);

        if (innerBackgroundImage != null)
            innerBackgroundImage.color = lockedByDamage ? new Color(0.12f, 0.02f, 0.02f, 0.98f) : new Color(0.29f, 0.03f, 0.1f, 0.98f);

        ResolveShootJoystickReference();
        if (shootJoystickRect != null && buttonRect != null)
        {
            buttonRect.anchorMin = shootJoystickRect.anchorMin;
            buttonRect.anchorMax = shootJoystickRect.anchorMax;
            Vector2 basePosition = shootJoystick != null ? shootJoystick.DefaultAnchoredPosition : shootJoystickRect.anchoredPosition;
            buttonRect.anchoredPosition = basePosition + OffsetFromShootJoystick;
        }

        RefreshNextWeaponIcon();
    }

    void ResolveShootJoystickReference()
    {
        if (shootJoystickRect != null)
            return;

        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        shootJoystickRect = shootJoystickObject != null ? shootJoystickObject.GetComponent<RectTransform>() : null;
        shootJoystick = shootJoystickObject != null ? shootJoystickObject.GetComponent<Joystick>() : null;
    }

    void RefreshNextWeaponIcon()
    {
        if (nextWeaponIconImage == null || shooting == null)
            return;

        Sprite icon = shooting.NextComplexWeaponIcon;
        nextWeaponIconImage.sprite = icon;
        nextWeaponIconImage.enabled = icon != null;
        if (icon != null)
            nextWeaponIconImage.color = shooting.IsComplexWeaponSwitchLockedByDamage
                ? new Color(1f, 0.25f, 0.2f, 0.52f)
                : new Color(1f, 1f, 1f, 0.92f);
    }

    void HandleClicked()
    {
        if (shooting != null)
            shooting.SwitchComplexWeapon();
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               GameplayHudVisibility.IsGameplayHudVisible(started);
    }

    static Sprite GetCircularSprite()
    {
        if (circularSprite != null)
            return circularSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 1.4f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        circularSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circularSprite;
    }
}
