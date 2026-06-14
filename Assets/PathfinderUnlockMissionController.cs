using System;
using Photon.Pun;
using UnityEngine;

public sealed class PathfinderUnlockMissionController : MonoBehaviour
{
    const float ScanInterval = 0.35f;

    static PathfinderUnlockMissionController instance;

    double announcedStartTime = double.MinValue;
    float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("PathfinderUnlockMissionController");
        instance = root.AddComponent<PathfinderUnlockMissionController>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            announcedStartTime = double.MinValue;
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextScanTime)
            return;

        nextScanTime = now + ScanInterval;
        TickAnnouncement();
    }

    void TickAnnouncement()
    {
        if (!TryGetRoundStartTime(out double currentStartTime))
        {
            announcedStartTime = double.MinValue;
            return;
        }

        if (Math.Abs(currentStartTime - announcedStartTime) <= 0.001d)
            return;

        if (!PlayerProfileService.HasInstance || !PlayerProfileService.Instance.IsInitialized)
            return;

        announcedStartTime = currentStartTime;
        if (PlayerProfileService.Instance.ShouldShowPathfinderHackOpportunity())
            RoundAnnouncementUI.Show("There is a chance to obtain new ship blueprints from hacked enemies", 4f);
    }

    static bool TryGetRoundStartTime(out double currentStartTime)
    {
        currentStartTime = 0d;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) ||
            !(startedValue is bool started) ||
            !started)
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startValue))
            TryConvertToDouble(startValue, out currentStartTime);

        return true;
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        try
        {
            switch (value)
            {
                case double d:
                    result = d;
                    return true;
                case float f:
                    result = f;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                default:
                    result = 0d;
                    return false;
            }
        }
        catch (Exception)
        {
            result = 0d;
            return false;
        }
    }
}
