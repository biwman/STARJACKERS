using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundPilotHudUI : MonoBehaviourPun
{
    const string RootName = "RoundPilotHud";
    const string PilotPanelName = "RoundPilotHudPilotPanel";
    const string PilotPortraitName = "RoundPilotHudPilotPortrait";
    const string TimerBadgeName = "RoundPilotHudTimerBadge";
    const float LeftInset = 24f;
    const float TopInset = 18f;
    const float PilotPanelSize = 222f;
    const float ScoreGap = 18f;

    static Sprite cachedFallbackPortrait;

    Canvas canvas;
    CanvasGroup rootGroup;
    Image pilotPanelImage;
    Image pilotPortraitImage;
    TMP_Text scoreText;
    TMP_Text timerText;
    Image timerBadge;
    string displayedPilotId = string.Empty;

    void Start()
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null)
        {
            enabled = false;
            return;
        }

        ResolveReferences();
        RefreshPilotPortrait(true);
        ApplyLayout();
        UpdateVisibility();
    }

    void Update()
    {
        if (canvas == null || rootGroup == null || scoreText == null || timerText == null)
            ResolveReferences();

        RefreshPilotPortrait(false);
        ApplyLayout();
        UpdateVisibility();
    }

    void OnDisable()
    {
        if (photonView != null && photonView.IsMine)
            HideAllRuntimeObjects();
    }

    void OnDestroy()
    {
        if (photonView != null && photonView.IsMine)
            DestroyAllRuntimeObjects();
    }

    public static void HideAllRuntimeObjects()
    {
        GameObject rootObject = FindSceneObject(RootName);
        if (rootObject != null)
        {
            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 0f;

            Image[] images = rootObject.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                    images[i].enabled = false;
            }
        }

        GameObject timerBadgeObject = FindSceneObject(TimerBadgeName);
        Image timerBadgeImage = timerBadgeObject != null ? timerBadgeObject.GetComponent<Image>() : null;
        if (timerBadgeImage != null)
            timerBadgeImage.enabled = false;
    }

    public static void DestroyAllRuntimeObjects()
    {
        DestroySceneObjects(RootName);
        DestroySceneObjects(TimerBadgeName);
    }

    void ResolveReferences()
    {
        scoreText = FindText("ScoreText");
        timerText = FindText("TimerText");
        canvas = ResolveCanvas();

        if (canvas == null)
            return;

        EnsurePilotPanel();
        EnsureTimerBadge();
    }

    Canvas ResolveCanvas()
    {
        if (scoreText != null)
        {
            Canvas scoreCanvas = scoreText.GetComponentInParent<Canvas>();
            if (scoreCanvas != null)
                return scoreCanvas;
        }

        if (timerText != null)
        {
            Canvas timerCanvas = timerText.GetComponentInParent<Canvas>();
            if (timerCanvas != null)
                return timerCanvas;
        }

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            Canvas namedCanvas = canvasObject.GetComponent<Canvas>();
            if (namedCanvas != null)
                return namedCanvas;
        }

        return FindAnyObjectByType<Canvas>();
    }

    void EnsurePilotPanel()
    {
        Transform rootTransform = canvas.transform.Find(RootName);
        GameObject rootObject;
        if (rootTransform != null)
        {
            rootObject = rootTransform.gameObject;
        }
        else
        {
            rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(canvas.transform, false);
        }

        rootGroup = rootObject.GetComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(LeftInset, -TopInset);
        rootRect.sizeDelta = new Vector2(PilotPanelSize, PilotPanelSize);

        Transform panelTransform = rootObject.transform.Find(PilotPanelName);
        GameObject panelObject;
        if (panelTransform != null)
        {
            panelObject = panelTransform.gameObject;
        }
        else
        {
            panelObject = new GameObject(PilotPanelName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(rootObject.transform, false);
        }

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        pilotPanelImage = panelObject.GetComponent<Image>();
        pilotPanelImage.color = new Color(0.06f, 0.1f, 0.14f, 0.86f);
        pilotPanelImage.raycastTarget = false;

        Transform portraitTransform = panelObject.transform.Find(PilotPortraitName);
        GameObject portraitObject;
        if (portraitTransform != null)
        {
            portraitObject = portraitTransform.gameObject;
        }
        else
        {
            portraitObject = new GameObject(PilotPortraitName, typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(panelObject.transform, false);
        }

        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.pivot = new Vector2(0.5f, 0.5f);
        portraitRect.offsetMin = new Vector2(5f, 5f);
        portraitRect.offsetMax = new Vector2(-5f, -5f);

        pilotPortraitImage = portraitObject.GetComponent<Image>();
        pilotPortraitImage.color = pilotPortraitImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        pilotPortraitImage.preserveAspect = true;
        pilotPortraitImage.raycastTarget = false;

        rootObject.transform.SetAsLastSibling();
    }

    void EnsureTimerBadge()
    {
        if (timerText == null || timerText.transform.parent == null)
            return;

        Transform existing = timerText.transform.parent.Find(TimerBadgeName);
        GameObject badgeObject;
        if (existing != null)
        {
            badgeObject = existing.gameObject;
        }
        else
        {
            badgeObject = new GameObject(TimerBadgeName, typeof(RectTransform), typeof(Image));
            badgeObject.transform.SetParent(timerText.transform.parent, false);
        }

        RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(1f, 1f);
        badgeRect.anchoredPosition = new Vector2(-18f, -16f);
        badgeRect.sizeDelta = new Vector2(148f, 50f);

        timerBadge = badgeObject.GetComponent<Image>();
        timerBadge.color = new Color(0.06f, 0.09f, 0.13f, 0.76f);
        timerBadge.raycastTarget = false;

        badgeObject.transform.SetSiblingIndex(Mathf.Max(0, timerText.transform.GetSiblingIndex()));
        timerText.transform.SetAsLastSibling();
    }

    void ApplyLayout()
    {
        StyleScoreText();
        StyleTimerText();
    }

    void StyleScoreText()
    {
        if (scoreText == null)
            return;

        if (scoreText.text.StartsWith("Score"))
            scoreText.text = "XP: 0";

        RectTransform rect = scoreText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(LeftInset + PilotPanelSize + ScoreGap, -TopInset - 10f);
        rect.sizeDelta = new Vector2(210f, 42f);

        scoreText.fontSize = 24f;
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.color = new Color(1f, 0.96f, 0.82f, 1f);
        scoreText.alignment = TextAlignmentOptions.Left;
        scoreText.textWrappingMode = TextWrappingModes.NoWrap;
        scoreText.characterSpacing = 0f;
        scoreText.margin = new Vector4(10f, 4f, 10f, 4f);
        scoreText.raycastTarget = false;

        ConfigureTextShadow(scoreText.gameObject, new Vector2(2f, -2f), new Color(0f, 0f, 0f, 0.55f));
    }

    void StyleTimerText()
    {
        if (timerText == null)
            return;

        RectTransform rect = timerText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -17f);
        rect.sizeDelta = new Vector2(142f, 46f);

        timerText.fontSize = 32f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = new Color(0.92f, 0.98f, 1f, 1f);
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.textWrappingMode = TextWrappingModes.NoWrap;
        timerText.characterSpacing = 0f;
        timerText.margin = new Vector4(8f, 4f, 8f, 4f);
        timerText.raycastTarget = false;

        ConfigureTextShadow(timerText.gameObject, new Vector2(2f, -2f), new Color(0f, 0f, 0f, 0.6f));
    }

    void UpdateVisibility()
    {
        bool visible = IsGameplayHudVisible();

        if (rootGroup != null)
            rootGroup.alpha = visible ? 1f : 0f;

        SetTextVisible(scoreText, visible);
        SetTextVisible(timerText, visible);

        if (pilotPanelImage != null)
            pilotPanelImage.enabled = visible;

        if (pilotPortraitImage != null)
        {
            bool hasPortrait = pilotPortraitImage.sprite != null;
            pilotPortraitImage.enabled = visible && hasPortrait;
            pilotPortraitImage.color = hasPortrait ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (timerBadge != null)
            timerBadge.enabled = visible;
    }

    void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text == null)
            return;

        text.enabled = visible;

        Image[] childImages = text.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < childImages.Length; i++)
        {
            if (childImages[i] != null)
                childImages[i].enabled = visible;
        }
    }

    void RefreshPilotPortrait(bool force)
    {
        if (pilotPortraitImage == null)
            return;

        string pilotId = GetPilotId();
        if (!force && displayedPilotId == pilotId)
            return;

        PilotDefinition definition = PilotCatalog.GetDefinition(pilotId);
        Sprite portrait = LoadPilotPortraitSprite(definition);
        if (portrait == null)
        {
            displayedPilotId = string.Empty;
            if (pilotPortraitImage.sprite == null)
                pilotPortraitImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        displayedPilotId = pilotId;
        pilotPortraitImage.sprite = portrait;
        pilotPortraitImage.color = Color.white;
    }

    string GetPilotId()
    {
        string fallback = PilotCatalog.JakeId;
        if (PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null)
            fallback = PlayerProfileService.Instance.CurrentProfile.SelectedPilotId;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return RoomSettings.GetPlayerPilotId(owner, fallback);
    }

    Sprite LoadPilotPortraitSprite(PilotDefinition definition)
    {
        if (definition == null)
            definition = PilotCatalog.GetDefinition(PilotCatalog.JakeId);

        Sprite sprite = LoadSpriteFromResources(definition.PortraitResourcePath);
        if (sprite != null)
            return sprite;

        if (cachedFallbackPortrait == null)
            cachedFallbackPortrait = LoadSpriteFromResources(PilotCatalog.GetDefinition(PilotCatalog.JakeId).PortraitResourcePath);

        return cachedFallbackPortrait;
    }

    Sprite LoadSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
            return null;

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

    TMP_Text FindText(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        return obj != null ? obj.GetComponent<TMP_Text>() : null;
    }

    void ConfigureTextShadow(GameObject target, Vector2 distance, Color color)
    {
        if (target == null)
            return;

        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
            shadow = target.AddComponent<Shadow>();

        shadow.effectDistance = distance;
        shadow.effectColor = color;
        shadow.useGraphicAlpha = true;
    }

    static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj != null && obj.scene.IsValid() && obj.name == objectName)
                return obj;
        }

        return null;
    }

    static void DestroySceneObjects(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj != null && obj.scene.IsValid() && obj.name == objectName)
                Destroy(obj);
        }
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return GameplayHudVisibility.IsGameplayHudVisible(started);
        }

        return false;
    }
}
