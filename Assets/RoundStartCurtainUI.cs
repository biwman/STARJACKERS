using System.Collections;
using TMPro;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoundStartCurtainUI : MonoBehaviour
{
    const string RootName = "RoundStartCurtain";
    const string BackgroundResourcesPath = "UI/RoundWarmup";
    const float MinHoldSeconds = 0.12f;
    const float MaxHoldSeconds = 8f;
    const float FadeSeconds = 0.22f;
    const int MinReadyFrames = 2;

    static readonly string[] WarmupTips =
    {
        "Wanna get rich? Collect resources and sell them to traders.",
        "Lure Beacon can save your life in dense and hostile environments.",
        "Dense nebulas hide danger, but they can also hide you.",
        "Extraction zones are safer when your cargo hold is already sorted.",
        "Some enemies are easier to outrun than to outgun.",
        "A damaged ship is still a payday if you know when to extract.",
        "Traders pay better when you bring rare resources home.",
        "Use deployables before the fight turns ugly.",
        "Lure Beacon pulls hostile attention away from your ship. Drop it before you get trapped.",
        "Robur doubles Lure Beacon charges, making decoys much stronger in hostile maps.",
        "Auto Turret anchors behind your ship and fires at the nearest enemy.",
        "Gadget Mine works best on chase routes, extraction paths, and narrow asteroid lanes.",
        "Space Bomb drops behind you, arms, then flies forward into a heavy blast.",
        "Space Trap arms nearby loot with a boosted mine explosion when someone collects it.",
        "Space Drill needs a lootable asteroid and free cargo space before it can launch.",
        "Dropbot sends the marked DROP cargo slot to extraction, even if your ship is later lost.",
        "SAFE, ASTRO, and DROP slots matter. Sort cargo before panic decides for you.",
        "Magnetic Beam pulls nearby asteroids and resources toward your ship for a short burst.",
        "Tractor Beam locks one collectible and tows it behind you while held.",
        "Battery restores shields over time. Use it before the last hit lands.",
        "Aegis Battery doubles the shield restored by Battery gadgets.",
        "Regenerative Shield Matrix rebuilds shields only after a quiet moment.",
        "Phase Shield gives 6 seconds of invulnerability when hull integrity drops low.",
        "Strong Plating halves nebula and environmental damage, but slows the ship slightly.",
        "Bulwark Projector reduces laser and autocannon damage by 25%.",
        "Cargo Bay Extension adds two ship inventory slots and extra shield capacity.",
        "Treasure Scanner pings faster as you get closer to Hidden Treasure.",
        "Short Scanner reveals ships hidden inside nebulas, fire nebulas, and clouds.",
        "Cloak Device hides you from players and enemies, but collisions still count.",
        "Guidance System points toward extraction, valuable loot, and the nearest hostile contact.",
        "Super Booster launches you forward without spending normal booster charge.",
        "Afterburner Stabilizer helps control sharp boost turns and high-drift escapes.",
        "Rocket Launcher locks on after holding aim; keep the target inside the lock angle.",
        "Vector locks rockets faster and makes Guidance System last longer.",
        "Rail Gun pierces targets. Line up enemies before spending the shot.",
        "Pulse Disruptor is tuned for shields, and its super releases a wide EMP wave.",
        "Astro Cutter carves obstacles hard, especially when you need an escape route.",
        "Artillery Gun damages the landing area. Aim where the enemy will be.",
        "Firing Friend fires short-range bursts, so stay close enough for it to help.",
        "Looting Friend collects nearby loot while you keep flying.",
        "Salvage Magnet Array doubles collection range for wreck loot and random salvage.",
        "Space Factory accepts Containers. Do not sell every container before checking the map.",
        "Science Station turns Blueprint Scrap into blueprint rewards.",
        "Kinetic Dampener and Robur both make Space Mine routes less punishing.",
        "Container Haulers can drop mines. Chasing directly behind them is expensive.",
        "Radar Ships mark incoming strikes. Leave the warning circle before impact.",
        "Hunter Lance locks before firing. Break the line or dodge during the warning.",
        "Gravity Squid tethers, pulls, and chips damage. Break distance fast.",
        "Space Manta charges after a windup. Dodge sideways, not straight back.",
        "Cosmic Worm gets nastier at lower HP. Expect dash and swallow attacks later.",
        "Cosmic Worm's swallow cone pulls ships and loose objects toward its maw.",
        "Rescue Ships heal damaged enemies. Remove the support before the frontline.",
        "Pirate Base steals valuable collectibles with a beam and drops stored cargo on death.",
        "Damaging Pirate Base can launch Elite and Ace fighters. Be ready for the counterattack.",
        "Charlie \"Smart\" is ignored by Pirate Fighters, Elites, and Aces until he attacks first.",
        "Atlas can auto-drop the least valuable round-gained cargo when a better item appears.",
        "Covax can upgrade collected asteroids by one rarity before they enter cargo.",
        "Sir Nowitzky's Breach Protocol empowers the next 5 salvos against bosses and obstacles."
    };

    static RoundStartCurtainUI instance;
    static Sprite[] cachedBackgroundSprites;
    static bool backgroundSpritesLoaded;

    CanvasGroup canvasGroup;
    Image backgroundImage;
    TextMeshProUGUI tipText;
    Coroutine routine;
    string displayedContentToken = string.Empty;

    public static void ShowForRoundStart()
    {
        ShowForRoundStart(RoundWarmupService.GetCurrentDisplayToken());
    }

    public static void ShowForRoundStart(string contentToken)
    {
        RoundStartCurtainUI curtain = EnsureInstance();
        if (curtain != null)
            curtain.Show(contentToken);
    }

    public static void HideImmediate()
    {
        if (instance == null)
            return;

        instance.HideNow();
    }

    static RoundStartCurtainUI EnsureInstance()
    {
        if (instance != null && instance.gameObject.scene.IsValid())
            return instance;

        GameObject existing = GameObject.Find(RootName);
        if (existing != null && existing.TryGetComponent(out RoundStartCurtainUI existingCurtain))
        {
            instance = existingCurtain;
            instance.EnsureVisuals();
            return instance;
        }

        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RoundStartCurtainUI));
        instance = root.GetComponent<RoundStartCurtainUI>();
        instance.EnsureVisuals();
        return instance;
    }

    void Awake()
    {
        instance = this;
        EnsureVisuals();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void EnsureVisuals()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        canvasGroup = GetComponent<CanvasGroup>();

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        GameObject panelObject = EnsureChild("Panel", typeof(RectTransform), typeof(Image));

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        StretchToFill(panelRect);

        Image panel = panelObject.GetComponent<Image>();
        panel.color = Color.black;
        panel.raycastTarget = true;
        panelObject.transform.SetAsFirstSibling();

        GameObject backgroundObject = EnsureChild("Background", typeof(RectTransform), typeof(Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        StretchToFill(backgroundRect);

        backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = Color.white;
        backgroundImage.preserveAspect = true;
        backgroundImage.raycastTarget = false;
        backgroundObject.transform.SetSiblingIndex(1);

        GameObject overlayObject = EnsureChild("Overlay", typeof(RectTransform), typeof(Image));
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        StretchToFill(overlayRect);

        Image overlay = overlayObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.42f);
        overlay.raycastTarget = false;
        overlayObject.transform.SetSiblingIndex(2);

        GameObject tipObject = EnsureChild("Tip", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        RectTransform tipRect = tipObject.GetComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0f, 0f);
        tipRect.anchorMax = new Vector2(1f, 0f);
        tipRect.pivot = new Vector2(0.5f, 0f);
        tipRect.offsetMin = new Vector2(140f, 54f);
        tipRect.offsetMax = new Vector2(-140f, 126f);

        tipText = tipObject.GetComponent<TextMeshProUGUI>();
        tipText.alignment = TextAlignmentOptions.Center;
        tipText.color = new Color(0.93f, 0.96f, 1f, 0.96f);
        tipText.fontSize = 30f;
        tipText.fontStyle = FontStyles.Normal;
        tipText.textWrappingMode = TextWrappingModes.Normal;
        tipText.overflowMode = TextOverflowModes.Ellipsis;
        tipText.raycastTarget = false;

        Shadow shadow = tipObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;
        tipObject.transform.SetAsLastSibling();
    }

    void Show(string contentToken)
    {
        EnsureVisuals();

        if (routine != null)
            StopCoroutine(routine);

        ApplyRandomWarmupContentIfNeeded(contentToken);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        routine = StartCoroutine(HoldThenFade());
    }

    GameObject EnsureChild(string childName, params System.Type[] components)
    {
        Transform child = transform.Find(childName);
        if (child != null)
            return child.gameObject;

        GameObject childObject = new GameObject(childName, components);
        childObject.transform.SetParent(transform, false);
        return childObject;
    }

    static void StretchToFill(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void ApplyRandomWarmupContentIfNeeded(string contentToken)
    {
        contentToken = string.IsNullOrWhiteSpace(contentToken) ? "fallback" : contentToken;

        if (string.Equals(displayedContentToken, contentToken, System.StringComparison.Ordinal))
            return;

        displayedContentToken = contentToken;
        ApplyRandomWarmupContent();
    }

    void ApplyRandomWarmupContent()
    {
        Sprite[] backgrounds = LoadBackgroundSprites();
        if (backgroundImage != null)
        {
            if (backgrounds.Length > 0)
            {
                backgroundImage.sprite = backgrounds[Random.Range(0, backgrounds.Length)];
                backgroundImage.enabled = true;
            }
            else
            {
                backgroundImage.sprite = null;
                backgroundImage.enabled = false;
            }
        }

        if (tipText != null && WarmupTips.Length > 0)
            tipText.text = WarmupTips[Random.Range(0, WarmupTips.Length)];
    }

    static Sprite[] LoadBackgroundSprites()
    {
        if (backgroundSpritesLoaded)
            return cachedBackgroundSprites ?? System.Array.Empty<Sprite>();

        backgroundSpritesLoaded = true;

        Sprite[] sprites = Resources.LoadAll<Sprite>(BackgroundResourcesPath);
        if (sprites != null && sprites.Length > 0)
        {
            cachedBackgroundSprites = sprites;
            return cachedBackgroundSprites;
        }

        Texture2D[] textures = Resources.LoadAll<Texture2D>(BackgroundResourcesPath);
        if (textures == null || textures.Length == 0)
        {
            cachedBackgroundSprites = System.Array.Empty<Sprite>();
            return cachedBackgroundSprites;
        }

        cachedBackgroundSprites = new Sprite[textures.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture == null)
                continue;

            cachedBackgroundSprites[i] = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        return cachedBackgroundSprites;
    }

    void HideNow()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
        displayedContentToken = string.Empty;
    }

    IEnumerator HoldThenFade()
    {
        float startTime = Time.unscaledTime;
        int readyFrames = 0;

        while (Time.unscaledTime - startTime < MaxHoldSeconds)
        {
            bool minHoldElapsed = Time.unscaledTime - startTime >= MinHoldSeconds;
            bool roundPreparing = RoundWarmupService.IsRoundPreparing();
            bool warmupReady = !RoundWarmupService.HasWarmupRoundContext() || RoundWarmupService.IsReadyForCurrentRound();
            bool cameraReady = RoundWarmupService.IsRoundStarted() && TrySnapLocalCameraToPlayer();

            if (minHoldElapsed && !roundPreparing && warmupReady && cameraReady)
            {
                readyFrames++;
                if (readyFrames >= MinReadyFrames)
                    break;
            }
            else
            {
                readyFrames = 0;
            }

            yield return null;
        }

        float fadeStart = Time.unscaledTime;
        while (Time.unscaledTime - fadeStart < FadeSeconds)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / FadeSeconds);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - t;
            yield return null;
        }

        HideNow();
    }

    bool TrySnapLocalCameraToPlayer()
    {
        CameraFollow cameraFollow = FindAnyObjectByType<CameraFollow>();
        if (cameraFollow == null)
            return false;

        Transform localPlayer = ResolveLocalPlayerTransform();
        if (localPlayer == null)
            return false;

        if (cameraFollow.target != localPlayer)
            cameraFollow.target = localPlayer;

        return cameraFollow.SnapToTarget();
    }

    Transform ResolveLocalPlayerTransform()
    {
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.TagObject is GameObject taggedObject && taggedObject != null && taggedObject.scene.IsValid())
        {
            PlayerHealth taggedHealth = taggedObject.GetComponent<PlayerHealth>();
            if (taggedHealth != null && ActorIdentity.IsLocalHumanPlayerActor(taggedHealth))
                return taggedObject.transform;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (!ActorIdentity.IsLocalHumanPlayerActor(player))
            {
                continue;
            }

            PhotonNetwork.LocalPlayer.TagObject = player.gameObject;
            return player.transform;
        }

        return null;
    }
}
