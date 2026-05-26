using System.Collections.Generic;
using UnityEngine;

public sealed class GameplayHudVisibility : MonoBehaviour
{
    const string RootName = "GameplayHudVisibility";
    const float SuppressionRescanInterval = 0.35f;

    static readonly string[] SuppressedObjectNames =
    {
        "JoystickBG",
        "ShootJoystickBG",
        "SuperAttackJoystickBG",
        "CollectButton",
        "ReloadButton",
        "WeaponSwitchButton",
        "GadgetButtonsRoot",
        "RoundChatButton",
        "RoundChatMenu",
        "RoundChatBubble",
        "RoundChatRemoteFeed",
        "ShipInventoryButton",
        "ShipInventoryPanel",
        "ShipInventoryDragVisual",
        "AmmoCounter",
        "ComplexAmmoBar",
        "HP_Bar",
        "Shield_Bar",
        "Booster_Bar",
        "RoundPilotHud",
        "RoundPilotHudTimerBadge",
        "ScoreText",
        "TimerText"
    };

    static GameplayHudVisibility instance;
    static bool extractionCinematicSuppressed;

    readonly Dictionary<GameObject, bool> capturedActiveState = new Dictionary<GameObject, bool>();
    readonly List<GameObject> suppressedObjects = new List<GameObject>(32);
    float nextSuppressionRescanTime;

    public static bool IsExtractionCinematicSuppressed => extractionCinematicSuppressed;

    public static bool IsGameplayHudVisible(bool normallyVisible)
    {
        return normallyVisible && !extractionCinematicSuppressed;
    }

    public static void SuppressForExtractionCinematic()
    {
        extractionCinematicSuppressed = true;
        EnsureInstance().BeginSuppression();
    }

    public static void ResetSuppression()
    {
        extractionCinematicSuppressed = false;
        if (instance != null)
            instance.RestoreCapturedObjects();
    }

    static GameplayHudVisibility EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject existing = GameObject.Find(RootName);
        if (existing == null)
            existing = new GameObject(RootName);

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
        if (extractionCinematicSuppressed)
            ApplySuppression();
    }

    void LateUpdate()
    {
        if (extractionCinematicSuppressed)
            ApplySuppression();
    }

    void ApplySuppressionBeforeCanvasRender()
    {
        if (extractionCinematicSuppressed)
            ApplySuppression();
    }

    void BeginSuppression()
    {
        RebuildSuppressedObjectCache();
        ApplySuppression();
    }

    void ApplySuppression()
    {
        if (suppressedObjects.Count == 0 || Time.unscaledTime >= nextSuppressionRescanTime)
            RebuildSuppressedObjectCache();

        for (int i = suppressedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = suppressedObjects[i];
            if (obj == null || !obj.scene.IsValid())
            {
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

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null || !obj.scene.IsValid() || !IsSuppressedObjectName(obj.name))
                continue;

            suppressedObjects.Add(obj);
        }
    }

    void RestoreCapturedObjects()
    {
        if (capturedActiveState.Count <= 0)
            return;

        foreach (KeyValuePair<GameObject, bool> entry in capturedActiveState)
        {
            GameObject obj = entry.Key;
            if (obj == null || !obj.scene.IsValid())
                continue;

            if (obj.activeSelf != entry.Value)
                obj.SetActive(entry.Value);
        }

        capturedActiveState.Clear();
        suppressedObjects.Clear();
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
