using UnityEngine;
using UnityEngine.UI;

public partial class PlayerProfilePanelUI
{
    static readonly bool ProfileHangarSceneVfxEnabled = true;

    GameObject profileHangarSceneVfxObject;
    ProfileHangarSceneVfx profileHangarSceneVfx;

    void RestoreProfileHangarSceneVfxTargets()
    {
        if (profileHangarSceneVfx != null)
            profileHangarSceneVfx.RestoreAnimatedTargets();
    }

    void SetProfileHangarSceneVfxActive(bool active)
    {
        if (profileHangarSceneVfxObject == null)
            return;

        if (!active)
            RestoreProfileHangarSceneVfxTargets();

        profileHangarSceneVfxObject.SetActive(active);
    }

    void ApplyProfileHangarSceneBaseBackground(bool showLargeHangar, bool showInventory)
    {
        if (!ProfileHangarSceneVfxEnabled || panelObject == null || (!showLargeHangar && !showInventory))
            return;

        Image background = panelObject.GetComponent<Image>();
        if (background == null)
            return;

        background.sprite = null;
        background.color = new Color(0.006f, 0.008f, 0.014f, 1f);
        background.type = Image.Type.Simple;
    }

    void RefreshProfileHangarSceneVfxLayout(bool showLargeHangar, bool showInventory)
    {
        if (!ProfileHangarSceneVfxEnabled || (!showLargeHangar && !showInventory))
        {
            SetProfileHangarSceneVfxActive(false);
            return;
        }

        EnsureProfileHangarSceneVfx();
        if (profileHangarSceneVfxObject == null || profileHangarSceneVfx == null)
            return;

        profileHangarSceneVfxObject.SetActive(true);
        profileHangarSceneVfxObject.transform.SetAsFirstSibling();
        profileHangarSceneVfx.Configure(
            showInventory ? ProfileHangarSceneVfx.DisplayMode.Inventory : ProfileHangarSceneVfx.DisplayMode.Home,
            shipPreviewRootRect,
            shipPreviewImage != null ? shipPreviewImage.rectTransform : null);
    }

    void EnsureProfileHangarSceneVfx()
    {
        if (panelObject == null)
            return;

        if (profileHangarSceneVfxObject == null)
        {
            profileHangarSceneVfxObject = new GameObject("ProfileHangarSceneVfx", typeof(RectTransform));
            profileHangarSceneVfxObject.transform.SetParent(panelObject.transform, false);

            RectTransform rect = profileHangarSceneVfxObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            profileHangarSceneVfx = profileHangarSceneVfxObject.AddComponent<ProfileHangarSceneVfx>();
        }
        else if (profileHangarSceneVfxObject.transform.parent != panelObject.transform)
        {
            profileHangarSceneVfxObject.transform.SetParent(panelObject.transform, false);
        }

        if (profileHangarSceneVfx == null)
            profileHangarSceneVfx = profileHangarSceneVfxObject.GetComponent<ProfileHangarSceneVfx>();
    }
}
