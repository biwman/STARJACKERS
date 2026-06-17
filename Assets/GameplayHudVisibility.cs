using System.Collections.Generic;
using UnityEngine;

public sealed class GameplayHudVisibility : MonoBehaviour
{
    const string RootName = "GameplayHudVisibility";
    const float SuppressionRescanInterval = 0.75f;

    static readonly string[] SuppressedObjectNames =
    {
        "JoystickBG",
        "MovementJoystickBoosterFuelRing",
        "MovementJoystickBoosterRing",
        "ShootJoystickBG",
        "SuperAttackJoystickBG",
        "CollectButton",
        "WeaponSwitchButton",
        "GadgetButtonsRoot",
        "RoundChatButton",
        "RoundChatMenu",
        "RoundChatBubble",
        "RoundChatRemoteFeed",
        "AstronautKillMeButton",
        "ShipInventoryButton",
        "ShipInventoryPanel",
        "ShipInventoryDragVisual",
        "ComplexAmmoBar",
        "HP_Bar",
        "Shield_Bar",
        "VitalsIconHud",
        "Booster_Bar",
        "RoundPilotHud",
        "RoundPilotHudTimerBadge",
        "ScoreText",
        "TimerText"
    };

    static GameplayHudVisibility instance;
    static bool extractionCinematicSuppressed;
    static bool roundSummarySuppressed;

    readonly Dictionary<GameObject, bool> capturedActiveState = new Dictionary<GameObject, bool>();
    readonly List<GameObject> suppressedObjects = new List<GameObject>(32);
    readonly HashSet<GameObject> suppressedObjectSet = new HashSet<GameObject>();
    float nextSuppressionRescanTime;

    public static bool IsExtractionCinematicSuppressed => extractionCinematicSuppressed;

    public static bool IsGameplayHudVisible(bool normallyVisible)
    {
        return normallyVisible && !IsHudSuppressed;
    }

    public static void SuppressForExtractionCinematic()
    {
        extractionCinematicSuppressed = true;
        EnsureInstance().BeginSuppression();
    }

    public static void SuppressForRoundSummary()
    {
        extractionCinematicSuppressed = false;
        roundSummarySuppressed = true;
        EnsureInstance().BeginSuppression();
    }

    public static void ResetSuppression()
    {
        extractionCinematicSuppressed = false;
        roundSummarySuppressed = false;
        if (instance != null)
            instance.RestoreCapturedObjects();
    }

    static bool IsHudSuppressed => extractionCinematicSuppressed || roundSummarySuppressed;

    static GameplayHudVisibility EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject existing = GameObject.Find(RootName);
        if (existing == null)
            existing = new GameObject(RootName);

        if (Application.isPlaying && existing.transform.parent == null)
            DontDestroyOnLoad(existing);

        instance = existing.GetComponent<GameplayHudVisibility>();
        if (instance == null)
            instance = existing.AddComponent<GameplayHudVisibility>();

        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void OnEnable()
    {
        Canvas.willRenderCanvases -= ApplySuppressionBeforeCanvasRender;
        Canvas.willRenderCanvases += ApplySuppressionBeforeCanvasRender;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= ApplySuppressionBeforeCanvasRender;
    }

    void Update()
    {
        if (IsHudSuppressed)
            ApplySuppression();
    }

    void LateUpdate()
    {
        if (IsHudSuppressed)
            ApplySuppression();
    }

    void ApplySuppressionBeforeCanvasRender()
    {
        if (IsHudSuppressed)
            ApplySuppression();
    }

    void BeginSuppression()
    {
        RebuildSuppressedObjectCache();
        ApplySuppression();
    }

    void ApplySuppression()
    {
        if (Time.unscaledTime >= nextSuppressionRescanTime)
            RebuildSuppressedObjectCache();

        for (int i = suppressedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = suppressedObjects[i];
            if (obj == null || !obj.scene.IsValid())
            {
                suppressedObjectSet.Remove(obj);
                suppressedObjects.RemoveAt(i);
                continue;
            }

            if (!capturedActiveState.ContainsKey(obj))
                capturedActiveState[obj] = obj.activeSelf;

            if (obj.activeSelf)
                obj.SetActive(false);
        }
    }

    void RebuildSuppressedObjectCache()
    {
        nextSuppressionRescanTime = Time.unscaledTime + SuppressionRescanInterval;
        suppressedObjects.Clear();
        suppressedObjectSet.Clear();

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
        {
            Canvas canvas = canvases[canvasIndex];
            if (canvas == null || !canvas.gameObject.scene.IsValid())
                continue;

            Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null)
                    TryAddSuppressedObject(child.gameObject);
            }
        }

        for (int i = 0; i < SuppressedObjectNames.Length; i++)
        {
            GameObject activeObject = GameObject.Find(SuppressedObjectNames[i]);
            TryAddSuppressedObject(activeObject);
        }
    }

    void TryAddSuppressedObject(GameObject obj)
    {
        if (obj == null || !obj.scene.IsValid() || !IsSuppressedObjectName(obj.name) || suppressedObjectSet.Contains(obj))
            return;

        suppressedObjectSet.Add(obj);
        suppressedObjects.Add(obj);
    }

    void RestoreCapturedObjects()
    {
        if (capturedActiveState.Count > 0)
        {
            foreach (KeyValuePair<GameObject, bool> entry in capturedActiveState)
            {
                GameObject obj = entry.Key;
                if (obj == null || !obj.scene.IsValid())
                    continue;

                if (obj.activeSelf != entry.Value)
                    obj.SetActive(entry.Value);
            }
        }

        capturedActiveState.Clear();
        suppressedObjects.Clear();
        suppressedObjectSet.Clear();
    }

    static bool IsSuppressedObjectName(string objectName)
    {
        for (int i = 0; i < SuppressedObjectNames.Length; i++)
        {
            if (objectName == SuppressedObjectNames[i])
                return true;
        }

        return false;
    }
}
