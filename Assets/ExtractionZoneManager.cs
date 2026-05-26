using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExtractionZoneManager : MonoBehaviourPunCallbacks
{
    const string GameStartedKey = "gameStarted";
    const string ExtractionCountKey = "extractionCount";
    const string ExtractionLayoutKey = "extractionLayout";
    const float Margin = 3.5f;
    const float MinZoneDistance = 8f;
    const float HideSceneZonesRetryInterval = 0.25f;
    const float RoundEndZoneCleanupDelay = 6f;
    const int HideSceneZonesRetryCount = 8;

    static ExtractionZoneManager instance;

    bool lastStartedState;
    float nextSceneZoneHideRetryTime;
    int sceneZoneHideRetriesRemaining;
    Coroutine runtimeZoneCleanupRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("ExtractionZoneManager");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<ExtractionZoneManager>();
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    void Start()
    {
        ScheduleSceneZoneHide();
    }

    void Update()
    {
        RunScheduledSceneZoneHide();

        if (!PhotonNetwork.InRoom)
            return;

        bool roundStarted = IsRoundStarted();
        if (!roundStarted)
        {
            if (lastStartedState && PhotonNetwork.IsMasterClient)
            {
                ScheduleRuntimeZoneCleanup();
            }

            lastStartedState = false;
            return;
        }

        CancelRuntimeZoneCleanup();
        lastStartedState = true;

        string layout = GetExtractionLayout();

        if (PhotonNetwork.IsMasterClient)
        {
            if (string.IsNullOrWhiteSpace(layout))
            {
                layout = BuildLayout(GetExtractionCount());
                Hashtable props = new Hashtable();
                props[ExtractionLayoutKey] = layout;
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }

            if (GetRuntimeZoneCount() == 0 && !string.IsNullOrWhiteSpace(layout))
            {
                SpawnZonesFromLayout(layout);
            }
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(GameStartedKey) && !IsRoundStarted() && PhotonNetwork.IsMasterClient)
        {
            ScheduleRuntimeZoneCleanup();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleSceneZoneHide();
    }

    void ScheduleSceneZoneHide()
    {
        HideSceneZones();
        sceneZoneHideRetriesRemaining = HideSceneZonesRetryCount;
        nextSceneZoneHideRetryTime = Time.unscaledTime + HideSceneZonesRetryInterval;
    }

    void RunScheduledSceneZoneHide()
    {
        if (sceneZoneHideRetriesRemaining <= 0 || Time.unscaledTime < nextSceneZoneHideRetryTime)
            return;

        HideSceneZones();
        sceneZoneHideRetriesRemaining--;
        nextSceneZoneHideRetryTime = Time.unscaledTime + HideSceneZonesRetryInterval;
    }

    void HideSceneZones()
    {
        foreach (ExtractionZone zone in Resources.FindObjectsOfTypeAll<ExtractionZone>())
        {
            if (zone == null || !zone.gameObject.scene.IsValid())
                continue;

            PhotonView view = zone.GetComponent<PhotonView>();
            if (view != null && view.IsRoomView && zone.gameObject.activeSelf)
            {
                zone.gameObject.SetActive(false);
            }
        }
    }

    void SpawnZonesFromLayout(string layout)
    {
        List<Vector2> positions = ParseLayout(layout);
        if (positions.Count == 0)
            return;

        foreach (Vector2 position in positions)
        {
            PhotonNetwork.Instantiate("ExtractionZone", new Vector3(position.x, position.y, 0f), Quaternion.identity);
        }

        GameVisualTheme.RequestRuntimeRefresh();
    }

    void DestroyRuntimeZones()
    {
        foreach (ExtractionZone zone in FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude))
        {
            if (zone == null)
                continue;

            PhotonView view = zone.GetComponent<PhotonView>();
            if (view != null && !view.IsRoomView)
            {
                PhotonNetwork.Destroy(zone.gameObject);
            }
        }
    }

    void ScheduleRuntimeZoneCleanup()
    {
        if (runtimeZoneCleanupRoutine != null)
            return;

        runtimeZoneCleanupRoutine = StartCoroutine(DestroyRuntimeZonesAfterRoundEnd());
    }

    void CancelRuntimeZoneCleanup()
    {
        if (runtimeZoneCleanupRoutine == null)
            return;

        StopCoroutine(runtimeZoneCleanupRoutine);
        runtimeZoneCleanupRoutine = null;
    }

    System.Collections.IEnumerator DestroyRuntimeZonesAfterRoundEnd()
    {
        yield return new WaitForSecondsRealtime(RoundEndZoneCleanupDelay);
        runtimeZoneCleanupRoutine = null;

        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || IsRoundStarted())
            yield break;

        DestroyRuntimeZones();
    }

    int GetRuntimeZoneCount()
    {
        int count = 0;

        foreach (ExtractionZone zone in FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude))
        {
            if (zone == null)
                continue;

            PhotonView view = zone.GetComponent<PhotonView>();
            if (view != null && !view.IsRoomView)
            {
                count++;
            }
        }

        return count;
    }

    string BuildLayout(int count)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        List<Vector2> positions = new List<Vector2>();
        int attempts = 0;

        while (positions.Count < count && attempts < 400)
        {
            attempts++;

            float x = Random.Range(-mapSize.x / 2f + Margin, mapSize.x / 2f - Margin);
            float y = Random.Range(-mapSize.y / 2f + Margin, mapSize.y / 2f - Margin);
            Vector2 candidate = new Vector2(x, y);

            bool farEnough = true;
            for (int i = 0; i < positions.Count; i++)
            {
                if (Vector2.Distance(candidate, positions[i]) < MinZoneDistance)
                {
                    farEnough = false;
                    break;
                }
            }

            if (farEnough)
            {
                positions.Add(candidate);
            }
        }

        if (positions.Count == 0)
        {
            positions.Add(Vector2.zero);
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < positions.Count; i++)
        {
            if (i > 0)
                builder.Append(';');

            builder.Append(positions[i].x.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(positions[i].y.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    List<Vector2> ParseLayout(string layout)
    {
        List<Vector2> positions = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(layout))
            return positions;

        string[] entries = layout.Split(';');
        foreach (string entry in entries)
        {
            string[] parts = entry.Split(',');
            if (parts.Length != 2)
                continue;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                continue;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                continue;

            positions.Add(new Vector2(x, y));
        }

        return positions;
    }

    bool IsRoundStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GameStartedKey, out object value) && value is bool started)
        {
            return started;
        }

        return false;
    }

    int GetExtractionCount()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ExtractionCountKey, out object value))
        {
            if (value is int intValue)
                return Mathf.Clamp(intValue, 1, 4);

            if (value is float floatValue)
                return Mathf.Clamp(Mathf.RoundToInt(floatValue), 1, 4);
        }

        return 3;
    }

    string GetExtractionLayout()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ExtractionLayoutKey, out object value) &&
            value is string layout)
        {
            return layout;
        }

        return string.Empty;
    }
}
